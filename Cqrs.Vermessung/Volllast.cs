using System.Diagnostics;

namespace Cqrs.Vermessung;

/// <summary>Ein Vorgang der Last: vom Eingang (Start) bis zum fachlich sichtbaren Ergebnis.</summary>
internal sealed class Vorgang
{
    public required string Schluessel { get; init; }
    public required string Phase { get; init; }
    public DateTimeOffset Start { get; set; }
    private long _sichtbarTicks;
    /// <summary>Wann das Ergebnis sichtbar wurde (vom Beobachter gesetzt; der erste Wert gilt).</summary>
    public DateTimeOffset? Sichtbar
    {
        get { var t = Interlocked.Read(ref _sichtbarTicks); return t == 0 ? null : new DateTimeOffset(t, TimeSpan.Zero); }
        set { if (value is { } v) Interlocked.CompareExchange(ref _sichtbarTicks, v.UtcTicks, 0); }
    }
    public string? Fehler { get; set; }
    public double LatenzMs => Sichtbar is { } s ? (s - Start).TotalMilliseconds : double.NaN;
}

/// <summary>Was ein Szenario für die Last bereitstellt: einen Vorgang über den echten Eingang starten.</summary>
internal interface ILast
{
    /// <summary>Startet den Vorgang (setzt <see cref="Vorgang.Start"/>), meldet ihn beim eigenen Beobachter an und kehrt zurück, wenn der Eingang
    /// ihn angenommen hat (Command beantwortet bzw. Datei im Verzeichnis).</summary>
    Task StarteAsync(Vorgang v, CancellationToken ct);
}

/// <summary>Das Ergebnis eines Volllast-Blocks.</summary>
internal sealed record Block(string Phase, int Sender, int Angenommen, int Fehler, int SichtbarImFenster, int Sichtbar, double FensterS,
    double AbbauS, bool Abgebaut, double P50, double P95, double P99, DateTimeOffset Von, DateTimeOffset Bis)
{
    public double AngenommenJeS => Angenommen / FensterS;
    public double SichtbarJeS => SichtbarImFenster / FensterS;
    public int Rueckstand => Angenommen - SichtbarImFenster;
    /// <summary>Durchsatz bis „alles sichtbar“: alle sichtbaren Vorgänge durch die Zeit vom Start bis zum letzten sichtbaren.</summary>
    public double DurchsatzJeS => Sichtbar / Math.Max(0.001, FensterS + AbbauS);
}

/// <summary>
/// Höchstlast (docs/12 §12.5): <c>sender</c> gleichzeitige Sender schicken so schnell, wie der Eingang annimmt,
/// höchstens <c>fenster</c> lang (bzw. bis <c>maxAnzahl</c> Vorgänge gestartet sind) — danach hört die Last auf, und es zählt, wie lange der
/// Rückstand bis „alles sichtbar“ braucht. Die Last ist zeitlich begrenzt: Dauerüberlast an einem heißen Aggregat kippt in den Retry-Sturm
/// (PF-9); ob der Rückstand danach sauber abgebaut wird, ist selbst ein Ergebnis.
/// </summary>
internal static class Volllast
{
    /// <param name="ohneFortschritt">
    /// Gesetzt (Störungslauf): der Abbau wartet, solange in dieser Zeit noch ein Vorgang sichtbar wurde — höchstens <paramref name="abbauFrist"/>.
    /// Geprüft wird dort Exactly-once und Lebendigkeit nach den Störungen, nicht eine feste Abbauzeit; ein Rückstand ohne Fortschritt ist ein Hänger.
    /// Ohne (Höchstlast): feste Frist, damit die Spalten vergleichbar bleiben.
    /// </param>
    public static async Task<(Block Block, List<Vorgang> Vorgaenge)> LaufeAsync(ILast last, string phase, int sender, TimeSpan fenster,
        TimeSpan abbauFrist, int maxAnzahl = int.MaxValue, TimeSpan? ohneFortschritt = null)
    {
        var vorgaenge = new List<Vorgang>();
        var von = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        var gestartet = 0;
        async Task SenderAsync()
        {
            while (sw.Elapsed < fenster && Interlocked.Increment(ref gestartet) <= maxAnzahl)
            {
                var v = new Vorgang { Schluessel = Guid.NewGuid().ToString("N")[..12], Phase = phase };
                lock (vorgaenge) vorgaenge.Add(v);
                try { await last.StarteAsync(v, default); }
                catch (Exception e) { v.Fehler = e.Message; }
            }
        }
        await Task.WhenAll(Enumerable.Range(0, sender).Select(_ => Task.Run(SenderAsync)));
        var fensterEnde = DateTimeOffset.UtcNow;
        var fensterS = Math.Max(0.001, (fensterEnde - von).TotalSeconds);
        var imFenster = vorgaenge.Count(v => v.Sichtbar is { } s && s <= fensterEnde);

        var abbau = Stopwatch.StartNew();
        var zuletztSichtbar = -1;
        var fortschritt = Stopwatch.StartNew();
        while (abbau.Elapsed < abbauFrist && vorgaenge.Any(v => v.Fehler == null && v.Sichtbar == null))
        {
            if (ohneFortschritt is { } still)
            {
                var sichtbar = vorgaenge.Count(v => v.Sichtbar != null);
                if (sichtbar != zuletztSichtbar) { zuletztSichtbar = sichtbar; fortschritt.Restart(); }
                else if (fortschritt.Elapsed > still) break;   // kein Vorgang mehr fertig geworden — ein Hänger
            }
            await Task.Delay(100);
        }
        var abgebaut = vorgaenge.All(v => v.Fehler != null || v.Sichtbar != null);
        var letztes = vorgaenge.Where(v => v.Sichtbar != null).Select(v => v.Sichtbar!.Value).DefaultIfEmpty(fensterEnde).Max();
        var abbauS = Math.Max(0, (letztes - fensterEnde).TotalSeconds);
        // Nicht sichtbare Vorgänge zählen als unendlich lang — die Perzentile über ihnen sind dann ∞ (null in der Ablage).
        var lat = vorgaenge.Where(v => v.Fehler == null).Select(v => double.IsNaN(v.LatenzMs) ? double.PositiveInfinity : v.LatenzMs).ToList();
        var block = new Block(phase, sender, vorgaenge.Count(v => v.Fehler == null), vorgaenge.Count(v => v.Fehler != null), imFenster,
            vorgaenge.Count(v => v.Sichtbar != null), fensterS, abbauS, abgebaut,
            Statistik.Perzentil(lat, 50), Statistik.Perzentil(lat, 95), Statistik.Perzentil(lat, 99), von, DateTimeOffset.UtcNow);
        Console.WriteLine($"    {phase}: {block.Angenommen} angenommen ({block.AngenommenJeS:F1}/s) · {block.SichtbarJeS:F1}/s sichtbar im Fenster · " +
                          $"Rückstand {block.Rueckstand} · Abbau {abbauS:F1} s{(abgebaut ? "" : " (NICHT abgebaut)")} · p50 {Ms(block.P50)} p99 {Ms(block.P99)}" +
                          (block.Fehler > 0 ? $" · {block.Fehler} Fehler ({vorgaenge.First(v => v.Fehler != null).Fehler})" : ""));
        return (block, vorgaenge);
    }

    public static string Ms(double ms) => double.IsInfinity(ms) || double.IsNaN(ms) ? "∞" : $"{ms:F0} ms";

    /// <summary>Wartet, bis alle Vorgänge sichtbar sind (oder gescheitert).</summary>
    public static async Task<bool> LeerlaufenAsync(IEnumerable<Vorgang> vorgaenge, TimeSpan frist)
    {
        var liste = vorgaenge.ToList();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            if (liste.All(v => v.Sichtbar != null || v.Fehler != null)) return true;
            await Task.Delay(200);
        }
        return false;
    }
}
