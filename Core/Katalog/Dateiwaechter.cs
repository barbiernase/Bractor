using System.Runtime.CompilerServices;
using Abstractions;

namespace Core.Katalog;

/// <summary>Wie ein Scan geweckt wird: nur per Intervall oder zusätzlich durch ein Dateisystem-Ereignis (das nur weckt, nie entscheidet).</summary>
public enum DateiErkennung { Abfrage, EreignisUndAbfrage }

/// <summary>Wann eine Datei als fertig gilt: <c>Stabil</c> = Größe und Änderungszeit über das Fenster unverändert; <c>Marker</c> = <c>&lt;datei&gt;&lt;Endung&gt;</c> existiert.</summary>
public enum DateiFertigArt { Stabil, Marker }

/// <summary>Woran der Wächter eine Datei wiedererkennt (dieselbe Kennung wird nicht noch einmal gemeldet).</summary>
public enum DateiKennungArt { NameGroesse, PfadGroesseZeit }

/// <summary>
/// Die Konfig eines Dateiwächters (Werte am Knoten der Instanz, z. B. <c>"Eingang": { "Pfad": "/share/linie1", "Muster": ["*.png"] }</c>).
/// <see cref="TimeSpan.Zero"/> heißt der Standard (Intervall eine Sekunde, Fenster eine Sekunde).
/// </summary>
public sealed record Dateiwaechterkonfig(
    string Pfad,
    string[]? Muster = null,
    bool Unterordner = false,
    DateiErkennung Erkennung = DateiErkennung.Abfrage,
    TimeSpan Intervall = default,
    DateiFertigArt Fertig = DateiFertigArt.Stabil,
    TimeSpan Fenster = default,
    string Endung = ".done",
    DateiKennungArt Kennung = DateiKennungArt.PfadGroesseZeit)
{
    public TimeSpan WirksamesIntervall => Intervall > TimeSpan.Zero ? Intervall : TimeSpan.FromSeconds(1);
    public TimeSpan WirksamesFenster => Fenster > TimeSpan.Zero ? Fenster : TimeSpan.FromSeconds(1);
    public IReadOnlyList<string> WirksameMuster => Muster is { Length: > 0 } m ? m : ["*"];
}

/// <summary>Was der Dateiwächter gemeldet hat: relativer Pfad → Kennung (im Speicher, je Lauf; nach einem Neustart meldet er alles erneut).</summary>
public sealed class Gesehen
{
    public Dictionary<string, string> Je { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Der DATEIWÄCHTER (Katalog des Frameworks, docs/04 §4.7): eine Quellart (IQuellart), eine Quelle einer App. Er LIEST nur — er legt nichts an, verschiebt und
/// löscht nichts. Ein Weckruf (Intervall oder, bei <see cref="DateiErkennung.EreignisUndAbfrage"/>, ein Dateisystem-Ereignis) löst einen Scan aus; der
/// Scan entscheidet (Inv. 2); je fertige, noch nicht gesehene Datei ein <see cref="DateiGefunden"/> — ein neuer Lauf. Was er gemeldet hat, merkt er
/// sich im Speicher; nach einem Neustart meldet er alles im Verzeichnis erneut — mit derselben Kennung aus dem Inhalt, das Backend verwirft, was schon
/// gewirkt hat (Inbox; ihr Horizont begrenzt das, docs/04 §4.7). Ein unlesbares Verzeichnis heißt eine wachsende Pause (bis eine Minute); der nächste
/// Scan holt alles nach.
/// </summary>
public sealed class Dateiwaechter(Dateiwaechterkonfig konfig) : IQuellart
{
    private static readonly TimeSpan MaxPause = TimeSpan.FromMinutes(1);

    private readonly Gesehen _gesehen = new();

    public async IAsyncEnumerable<DateiGefunden> Lausche([EnumeratorCancellation] CancellationToken ct)
    {
        var scan = new DateiScan(konfig, _gesehen);
        using var weck = new SemaphoreSlim(0);
        FileSystemWatcher? beobachter = null;
        var pause = konfig.WirksamesIntervall;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                IReadOnlyList<DateiStand> sicht;
                try
                {
                    sicht = Liste();
                    if (konfig.Erkennung == DateiErkennung.EreignisUndAbfrage && beobachter == null) beobachter = Beobachte(weck);
                    pause = konfig.WirksamesIntervall;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    beobachter?.Dispose();
                    beobachter = null;
                    sicht = Array.Empty<DateiStand>();
                    pause = TimeSpan.FromTicks(Math.Min(MaxPause.Ticks, Math.Max(pause.Ticks, TimeSpan.FromSeconds(1).Ticks) * 2));
                }
                if (sicht.Count > 0)
                    foreach (var d in scan.Fertige(sicht, DateTimeOffset.UtcNow))
                    {
                        scan.Gemeldet(d);
                        yield return new DateiGefunden(d.Name, d.Pfad, d.RelativerPfad, d.Groesse, d.GeaendertAm, DateTimeOffset.UtcNow);
                    }
                try { await weck.WaitAsync(pause, ct); }
                catch (OperationCanceledException) { yield break; }
            }
        }
        finally { beobachter?.Dispose(); }
    }

    /// <summary>Alle passenden Dateien des Verzeichnisses (rekursiv nach Konfig). Fehlt das Verzeichnis, ist das ein Lesefehler.</summary>
    private IReadOnlyList<DateiStand> Liste()
    {
        if (!Directory.Exists(konfig.Pfad)) throw new DirectoryNotFoundException($"Verzeichnis {konfig.Pfad} fehlt");
        var option = konfig.Unterordner ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var pfade = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var muster in konfig.WirksameMuster)
            foreach (var p in Directory.EnumerateFiles(konfig.Pfad, muster, option)) pfade.Add(p);
        var marker = konfig.Fertig == DateiFertigArt.Marker;
        var sicht = new List<DateiStand>(pfade.Count);
        foreach (var p in pfade)
        {
            try
            {
                var fi = new FileInfo(p);
                if (!fi.Exists) continue;
                sicht.Add(new DateiStand(fi.FullName, Path.GetRelativePath(konfig.Pfad, fi.FullName), fi.Name, fi.Length,
                    new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero), marker && File.Exists(fi.FullName + konfig.Endung)));
            }
            catch (IOException) { /* zwischenzeitlich gelöscht/gesperrt — nächster Scan */ }
        }
        return sicht;
    }

    /// <summary>Ein Dateisystem-Ereignis weckt nur (Inv. 2) — es entscheidet nichts; der Scan danach entscheidet.</summary>
    private FileSystemWatcher Beobachte(SemaphoreSlim weck)
    {
        var w = new FileSystemWatcher(konfig.Pfad) { IncludeSubdirectories = konfig.Unterordner, EnableRaisingEvents = true };
        void Wecke(object _, FileSystemEventArgs __) { if (weck.CurrentCount == 0) weck.Release(); }
        w.Created += Wecke;
        w.Changed += Wecke;
        w.Renamed += (o, e) => Wecke(o, e);
        return w;
    }
}

/// <summary>Eine Datei, wie ein Scan sie sieht.</summary>
public sealed record DateiStand(string Pfad, string RelativerPfad, string Name, long Groesse, DateTimeOffset GeaendertAm, bool MarkerDa);

/// <summary>
/// Der Kern des Dateiwächters ohne Dateisystem und Uhr (alles kommt als Argument, der Prüfstand beweist ihn vollständig): Kennung bilden,
/// Fertig-Erkennung (Stabil/Marker), Wartezone und was schon gemeldet ist (das Gedächtnis des Wächters, im Speicher).
/// </summary>
public sealed class DateiScan(Dateiwaechterkonfig konfig, Gesehen gesehen)
{
    private readonly Dictionary<string, (long Groesse, DateTimeOffset Geaendert, DateTimeOffset Seit)> _wartezone = new(StringComparer.Ordinal);

    public string Kennung(DateiStand d) => Bilde(konfig.Kennung, d);

    public static string Bilde(DateiKennungArt art, DateiStand d) => art switch
    {
        DateiKennungArt.NameGroesse => $"{d.Name}|{d.Groesse}",
        _ => $"{d.RelativerPfad.Replace('\\', '/')}|{d.Groesse}|{d.GeaendertAm.ToUniversalTime():O}",
    };

    /// <summary>Ist die Datei eine Marker-Datei (selbst kein Kandidat)?</summary>
    public bool IstMarker(string name) => konfig.Fertig == DateiFertigArt.Marker && name.EndsWith(konfig.Endung, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nach dem Melden: diese Datei (unter dieser Kennung) nicht noch einmal melden.</summary>
    public void Gemeldet(DateiStand d)
    {
        gesehen.Je[d.RelativerPfad] = Kennung(d);
        _wartezone.Remove(d.RelativerPfad);
    }

    /// <summary>
    /// Ein Scan: aus der aktuellen Sicht die Dateien, die jetzt fertig und noch nicht gemeldet sind (Reihenfolge: relativer Pfad). Verschwundene Dateien
    /// fallen aus Wartezone und Gedächtnis; eine ersetzte Datei mit neuer Kennung ist wieder ein Kandidat.
    /// </summary>
    public IReadOnlyList<DateiStand> Fertige(IReadOnlyList<DateiStand> sicht, DateTimeOffset jetzt)
    {
        var da = new HashSet<string>(sicht.Select(s => s.RelativerPfad), StringComparer.Ordinal);
        foreach (var weg in gesehen.Je.Keys.Where(k => !da.Contains(k)).ToList()) gesehen.Je.Remove(weg);
        foreach (var weg in _wartezone.Keys.Where(k => !da.Contains(k)).ToList()) _wartezone.Remove(weg);

        var fertig = new List<DateiStand>();
        foreach (var d in sicht.Where(s => !IstMarker(s.Name)).OrderBy(s => s.RelativerPfad, StringComparer.Ordinal))
        {
            if (gesehen.Je.TryGetValue(d.RelativerPfad, out var k) && k == Kennung(d)) continue;
            if (konfig.Fertig == DateiFertigArt.Marker)
            {
                if (d.MarkerDa) fertig.Add(d);
                continue;
            }
            if (!_wartezone.TryGetValue(d.RelativerPfad, out var w) || w.Groesse != d.Groesse || w.Geaendert != d.GeaendertAm)
                _wartezone[d.RelativerPfad] = w = (d.Groesse, d.GeaendertAm, jetzt);
            if (jetzt - w.Seit >= konfig.WirksamesFenster) fertig.Add(d);
        }
        return fertig;
    }
}
