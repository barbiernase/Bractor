using Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Quellen.Treiber.Datei;

/// <summary>
/// Der Datei-Treiber (docs/konzept-werkbank.md §3.5). Er LIEST nur — er legt nichts an, verschiebt und löscht nichts.
/// <list type="number">
/// <item>Start: alle passenden Dateien listen und gebündelt im Log nachsehen, welche Vorgänge es schon gibt (kein eigener Stand).</item>
/// <item>Schleife: ein Weckruf (Intervall oder, bei <c>EreignisUndAbfrage</c>, ein Dateisystem-Ereignis) löst einen Scan aus; der Scan
/// entscheidet (Inv. 2).</item>
/// <item>Melden nacheinander über den <see cref="QuellEingang"/>: die nächste Datei erst, wenn die vorige im Log steht (Rückstau).</item>
/// <item>Fehler: Verzeichnis nicht lesbar → „gestört" (<c>/health</c>), wachsende Pause; der nächste Scan holt alles nach.</item>
/// </list>
/// </summary>
public sealed class DateiTreiber
{
    private static readonly TimeSpan MaxPause = TimeSpan.FromMinutes(1);
    private const int BuendelGroesse = 500;

    private readonly QuellTyp _quelle;
    private readonly DateiQuellKonfig _konfig;
    private readonly Func<IQuellNachricht, CancellationToken, Task> _melde;
    private readonly Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlySet<Guid>>> _bekannt;
    private readonly QuellenZustand _zustand;
    private readonly TimeProvider _zeit;
    private readonly ILogger? _logger;

    public DateiTreiber(QuellTyp quelle, DateiQuellKonfig konfig, Func<IQuellNachricht, CancellationToken, Task> melde,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlySet<Guid>>> bekannt, QuellenZustand zustand,
        TimeProvider? zeit = null, ILogger? logger = null)
    {
        _quelle = quelle;
        _konfig = konfig;
        _melde = melde;
        _bekannt = bekannt;
        _zustand = zustand;
        _zeit = zeit ?? TimeProvider.System;
        _logger = logger;
    }

    public async Task LaufeAsync(CancellationToken ct)
    {
        var scan = new DateiScan(_konfig);
        var abgeglichen = false;
        var pause = _konfig.Intervall;
        using var weck = new SemaphoreSlim(0);
        FileSystemWatcher? beobachter = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var sicht = Liste();
                    if (_konfig.Erkennung == DateiErkennung.EreignisUndAbfrage && beobachter == null) beobachter = Beobachte(weck);
                    if (!abgeglichen)
                    {
                        await StartAbgleichAsync(scan, sicht, ct);
                        abgeglichen = true;
                    }
                    foreach (var d in scan.Fertige(sicht, _zeit.GetUtcNow()))
                    {
                        var daten = new DateiDaten(d.Name, d.Pfad, d.RelativerPfad, d.Groesse, d.GeaendertAm, _zeit.GetUtcNow(), scan.Kennung(d));
                        await _melde(_quelle.DateiFabrik!(daten), ct);
                        scan.Gemeldet(d);
                        _zustand.Gemeldet(_quelle.Abschnitt);
                    }
                    _zustand.Ok(_quelle.Abschnitt);
                    pause = _konfig.Intervall;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _zustand.Gestoert(_quelle.Abschnitt, ex.Message);
                    _logger?.LogWarning("Quelle {Quelle}: {Pfad} nicht lesbar ({Grund}) — neuer Versuch in {Pause}", _quelle.Abschnitt, _konfig.Pfad, ex.Message, pause);
                    beobachter?.Dispose();
                    beobachter = null;
                    pause = TimeSpan.FromTicks(Math.Min(MaxPause.Ticks, Math.Max(pause.Ticks, TimeSpan.FromSeconds(1).Ticks) * 2));
                }
                try { await weck.WaitAsync(pause, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
        finally { beobachter?.Dispose(); }
    }

    /// <summary>Beim ersten Scan: gebündelt im Log nachsehen, welche Dateien schon einen Vorgang haben (die Wahrheit ist der Log).</summary>
    private async Task StartAbgleichAsync(DateiScan scan, IReadOnlyList<DateiStand> sicht, CancellationToken ct)
    {
        var kandidaten = sicht.Where(d => !scan.IstMarker(d.Name))
            .Select(d => (Datei: d, Stream: QuellVorgang.StreamFür(_quelle.Typ, scan.Kennung(d)))).ToList();
        var schonDa = 0;
        for (var i = 0; i < kandidaten.Count; i += BuendelGroesse)
        {
            var buendel = kandidaten.Skip(i).Take(BuendelGroesse).ToList();
            var bekannt = await _bekannt(buendel.Select(x => x.Stream).ToList(), ct);
            foreach (var (d, stream) in buendel)
                if (bekannt.Contains(stream)) { scan.SchonBekannt(d); schonDa++; }
        }
        _logger?.LogInformation("Quelle {Quelle} beobachtet {Pfad}: {Anzahl} Dateien, davon {Bekannt} schon im Log",
            _quelle.Abschnitt, _konfig.Pfad, kandidaten.Count, schonDa);
    }

    /// <summary>Alle passenden Dateien des Verzeichnisses (rekursiv nach Konfig). Fehlt das Verzeichnis, ist das ein Lesefehler.</summary>
    private IReadOnlyList<DateiStand> Liste()
    {
        if (!Directory.Exists(_konfig.Pfad)) throw new DirectoryNotFoundException($"Verzeichnis {_konfig.Pfad} fehlt");
        var option = _konfig.Unterordner ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var pfade = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var muster in _konfig.WirksameMuster)
            foreach (var p in Directory.EnumerateFiles(_konfig.Pfad, muster, option)) pfade.Add(p);
        var marker = _konfig.Fertig.Art == DateiFertigArt.Marker;
        var sicht = new List<DateiStand>(pfade.Count);
        foreach (var p in pfade)
        {
            try
            {
                var fi = new FileInfo(p);
                if (!fi.Exists) continue;
                sicht.Add(new DateiStand(fi.FullName, Path.GetRelativePath(_konfig.Pfad, fi.FullName), fi.Name, fi.Length,
                    new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero), marker && File.Exists(fi.FullName + _konfig.Fertig.Endung)));
            }
            catch (IOException) { /* zwischenzeitlich gelöscht/gesperrt — nächster Scan */ }
        }
        return sicht;
    }

    /// <summary>Ein Dateisystem-Ereignis weckt nur (Inv. 2) — es entscheidet nichts; der Scan danach entscheidet.</summary>
    private FileSystemWatcher Beobachte(SemaphoreSlim weck)
    {
        var w = new FileSystemWatcher(_konfig.Pfad) { IncludeSubdirectories = _konfig.Unterordner, EnableRaisingEvents = true };
        void Wecke(object _, FileSystemEventArgs __) { if (weck.CurrentCount == 0) weck.Release(); }
        w.Created += Wecke;
        w.Changed += Wecke;
        w.Renamed += (o, e) => Wecke(o, e);
        return w;
    }
}
