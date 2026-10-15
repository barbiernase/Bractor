using System.Diagnostics;

namespace Cqrs.Vermessung;

/// <summary>Ein Vorgang der Last: vom Eingang (Start) bis zum fachlich sichtbaren Ergebnis.</summary>
internal sealed class Vorgang
{
    public required string Schluessel { get; init; }
    public required string Phase { get; init; }
    public required double Rate { get; init; }
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

/// <summary>Was ein Szenario für die Rampe bereitstellt: einen Vorgang über den echten Eingang starten.</summary>
internal interface ILast
{
    /// <summary>Startet den Vorgang (setzt <see cref="Vorgang.Start"/>) und meldet ihn beim eigenen Beobachter an.</summary>
    Task StarteAsync(Vorgang v, CancellationToken ct);
}

internal sealed record Stufe(string Phase, double Rate, int Anzahl, int Sichtbar, int Fehler, double P50, double P95, double P99,
    bool Bestanden, string Grund, DateTimeOffset Von, DateTimeOffset Bis);

/// <summary>
/// Die Rampe (docs/plan-garantien-und-durchsatz.md §0.1): die Rate stufenweise steigern, bis die p99-Latenz über die Grenze steigt oder
/// der Rückstand wächst (nicht alle Vorgänge einer Stufe kurz nach ihrem Ende sichtbar). Sättigung = die höchste bestandene Rate; danach
/// der Latenzlauf bei 50 % der Sättigung. Offene Last: die Vorgänge starten nach Fahrplan, unabhängig davon, wie schnell der Host ist.
/// </summary>
internal sealed class Rampe
{
    public sealed record Parameter(double StartRate, double Faktor, TimeSpan StufenDauer, TimeSpan Nachlauf, double P99GrenzeMs,
        TimeSpan LatenzDauer, double MaxRate, int MaxOffen);

    private readonly ILast _last;
    private readonly Parameter _p;
    private readonly List<Vorgang> _alle = new();
    private long _nummer;

    public Rampe(ILast last, Parameter p) { _last = last; _p = p; }

    public IReadOnlyList<Vorgang> Alle => _alle;

    public async Task<(double Saettigung, List<Stufe> Stufen)> SaettigungAsync(CancellationToken ct)
    {
        // Aufwärmen (nicht gewertet): erste Aktivierungen, JIT, Marten-Schema — sonst misst die erste Stufe den Kaltstart.
        var warm = await StufeAsync("aufwaermen", _p.StartRate, _p.StufenDauer / 2, ct);
        Console.WriteLine($"    aufwärmen: {warm.Sichtbar}/{warm.Anzahl} sichtbar · p50 {warm.P50:F0} ms (nicht gewertet)");
        if (!await LeerlaufenAsync(TimeSpan.FromSeconds(90), ct)) throw new RampenFehler("Aufwärmen baut sich nicht ab");
        var stufen = new List<Stufe>();
        double bestanden = 0, gescheitert = 0;
        for (var rate = _p.StartRate; rate <= _p.MaxRate; rate *= _p.Faktor)
        {
            var s = await StufeAsync($"stufe-{stufen.Count + 1}", rate, _p.StufenDauer, ct);
            stufen.Add(s);
            Console.WriteLine($"    {s.Phase}: {rate:F1}/s · {s.Sichtbar}/{s.Anzahl} sichtbar · p50 {s.P50:F0} p99 {s.P99:F0} ms · {(s.Bestanden ? "ok" : s.Grund)}");
            if (!s.Bestanden) { gescheitert = rate; break; }
            bestanden = rate;
            if (!await LeerlaufenAsync(TimeSpan.FromSeconds(30), ct)) { gescheitert = rate; break; }
        }
        if (!await LeerlaufenAsync(TimeSpan.FromSeconds(180), ct)) throw new RampenFehler("Rückstand baut sich nach der Rampe nicht ab");
        // Verfeinern: einmal zwischen der letzten bestandenen und der ersten gescheiterten Rate.
        if (bestanden > 0 && gescheitert > 0 && gescheitert / bestanden > 1.2)
        {
            var mitte = (bestanden + gescheitert) / 2;
            var s = await StufeAsync($"stufe-{stufen.Count + 1}", mitte, _p.StufenDauer, ct);
            stufen.Add(s);
            Console.WriteLine($"    {s.Phase}: {mitte:F1}/s · {s.Sichtbar}/{s.Anzahl} sichtbar · p50 {s.P50:F0} p99 {s.P99:F0} ms · {(s.Bestanden ? "ok" : s.Grund)}");
            if (s.Bestanden) bestanden = mitte;
            if (!await LeerlaufenAsync(TimeSpan.FromSeconds(180), ct)) throw new RampenFehler("Rückstand baut sich nach der Verfeinerung nicht ab");
        }
        return (bestanden, stufen);
    }

    /// <summary>Eine Stufe: <paramref name="dauer"/> lang <paramref name="rate"/> Vorgänge je Sekunde starten, danach höchstens den Nachlauf warten.</summary>
    public async Task<Stufe> StufeAsync(string phase, double rate, TimeSpan dauer, CancellationToken ct)
    {
        var anzahl = Math.Max(1, (int)Math.Round(rate * dauer.TotalSeconds));
        var vorgaenge = new List<Vorgang>(anzahl);
        var laufend = new List<Task>(anzahl);
        var von = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        string? grund = null;
        for (var k = 0; k < anzahl; k++)
        {
            var warte = TimeSpan.FromSeconds(k / rate) - sw.Elapsed;
            if (warte > TimeSpan.FromMilliseconds(1)) await Task.Delay(warte, ct);
            if (Interlocked.Read(ref _offen) > _p.MaxOffen) { grund = $"Eingang staut (> {_p.MaxOffen} offene Sendungen)"; break; }
            var v = new Vorgang { Schluessel = (++_nummer).ToString(), Phase = phase, Rate = rate };
            vorgaenge.Add(v);
            lock (_alle) _alle.Add(v);
            laufend.Add(StarteSicherAsync(v, ct));
        }
        var sendeEnde = sw.Elapsed;
        await Task.WhenAll(laufend);
        var frist = sendeEnde + _p.Nachlauf;
        while (sw.Elapsed < frist && vorgaenge.Any(v => v.Sichtbar == null && v.Fehler == null))
            await Task.Delay(100, ct);
        var lat = vorgaenge.Select(v => double.IsNaN(v.LatenzMs) ? double.PositiveInfinity : v.LatenzMs).ToList();
        var sichtbar = vorgaenge.Count(v => v.Sichtbar != null);
        var fehler = vorgaenge.Count(v => v.Fehler != null);
        var p99 = Statistik.Perzentil(lat, 99);
        grund ??= fehler > 0 ? $"{fehler} Eingänge gescheitert ({vorgaenge.First(v => v.Fehler != null).Fehler})"
            : sichtbar < vorgaenge.Count ? $"Rückstand: {vorgaenge.Count - sichtbar} nicht sichtbar nach {_p.Nachlauf.TotalSeconds:0} s Nachlauf"
            : p99 > _p.P99GrenzeMs ? $"p99 {p99:F0} ms > {_p.P99GrenzeMs:F0} ms" : "";
        var endeSichtbar = vorgaenge.Where(v => v.Sichtbar != null).Select(v => v.Sichtbar!.Value).DefaultIfEmpty(DateTimeOffset.UtcNow).Max();
        return new Stufe(phase, rate, vorgaenge.Count, sichtbar, fehler, Statistik.Perzentil(lat, 50), Statistik.Perzentil(lat, 95), p99,
            grund.Length == 0, grund, von, endeSichtbar);
    }

    private long _offen;

    private async Task StarteSicherAsync(Vorgang v, CancellationToken ct)
    {
        Interlocked.Increment(ref _offen);
        try { await _last.StarteAsync(v, ct); }
        catch (Exception e) { v.Fehler = e.Message; }
        finally { Interlocked.Decrement(ref _offen); }
    }

    /// <summary>Wartet, bis alle bisher gestarteten Vorgänge sichtbar sind (oder gescheitert).</summary>
    public async Task<bool> LeerlaufenAsync(TimeSpan frist, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            bool offen;
            lock (_alle) offen = _alle.Any(v => v.Sichtbar == null && v.Fehler == null);
            if (!offen) return true;
            await Task.Delay(200, ct);
        }
        return false;
    }
}

internal sealed class RampenFehler(string meldung) : Exception(meldung);
