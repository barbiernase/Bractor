using System.Runtime.CompilerServices;
using Abstractions;
using Domain.ImagePair;
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// Katalog-QUELLE „Datei": beobachtet ein Verzeichnis (Samba-Share) per Polling und meldet jede stabile Datei als
/// <see cref="DateiErkannt"/>. Ersetzt die FileWatch-Pipeline samt Trigger — Tick und Start sind Innenleben der Quelle, nicht
/// mehr Knoten im Board.
///
/// Kein Domänenwissen (kein Parsing, keine PairId). Polling statt FileSystemWatcher (inotify geht auf CIFS nicht); eine Datei gilt
/// als fertig, wenn sie ein Poll-Intervall lang dieselbe Größe hat. Schon vorhandene Dateien werden beim Start ebenfalls gemeldet:
/// das Framework schreibt jede Nachricht genau einmal (Kennung) — eine früher verarbeitete Datei startet keinen zweiten Vorgang, eine
/// früher gescheiterte bekommt so eine neue Chance (statt wie zuvor beim Start als „gesehen" zu verschwinden).
/// </summary>
public sealed class DateiQuelle(FileWatchConfig config, ILogger<DateiQuelle> logger) : IQuelle<DateiErkannt>
{
    public async IAsyncEnumerable<DateiErkannt> LaufeAsync([EnumeratorCancellation] CancellationToken abbruch)
    {
        Directory.CreateDirectory(config.WatchPath);
        logger.LogInformation("DateiQuelle beobachtet {Pfad} (alle {Intervall})", config.WatchPath, config.PollInterval);
        var gemeldet = new HashSet<string>();
        var wartend = new Dictionary<string, long>();
        while (!abbruch.IsCancellationRequested)
        {
            foreach (var (pfad, name, groesse) in StabileKandidaten(gemeldet, wartend))
            {
                gemeldet.Add(name);
                yield return new DateiErkannt(pfad, name, groesse, DateTimeOffset.UtcNow);
            }
            try { await Task.Delay(config.PollInterval, abbruch); }
            catch (OperationCanceledException) { yield break; }
        }
    }

    /// <summary>Dateien, deren Größe seit dem letzten Blick gleich geblieben ist (fertig geschrieben) und die noch nicht gemeldet sind.</summary>
    private List<(string Pfad, string Name, long Groesse)> StabileKandidaten(HashSet<string> gemeldet, Dictionary<string, long> wartend)
    {
        var stabil = new List<(string, string, long)>();
        string[] dateien;
        try { dateien = Directory.GetFiles(config.WatchPath); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DateiQuelle: Verzeichnis {Pfad} nicht lesbar", config.WatchPath);
            return stabil;
        }
        foreach (var pfad in dateien)
        {
            var name = Path.GetFileName(pfad);
            if (gemeldet.Contains(name)) continue;
            long groesse;
            try { groesse = new FileInfo(pfad).Length; }
            catch { continue; }   // zwischenzeitlich gelöscht/gesperrt — nächster Blick
            if (!wartend.TryGetValue(name, out var vorher) || vorher != groesse) { wartend[name] = groesse; continue; }
            wartend.Remove(name);
            stabil.Add((pfad, name, groesse));
        }
        // Gedächtnis begrenzen: nur Namen merken, die es noch gibt (Flicker-Schutz bis 2× Ringpuffer).
        if (gemeldet.Count > config.RingBufferSize * 2)
        {
            var da = dateien.Select(Path.GetFileName).ToHashSet();
            gemeldet.RemoveWhere(n => !da.Contains(n));
        }
        return stabil;
    }
}
