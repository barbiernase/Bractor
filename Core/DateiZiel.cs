using System.Globalization;
using Abstractions;

namespace Core;

/// <summary>
/// Ein Store der <b>Klasse 2</b> im Dateisystem (<see cref="IWiederholungsfestesZiel"/>, Plan Pub/Sub-Neustrukturierung §2.4) — der Nachweis, dass
/// das Framework keine Transaktion des Stores braucht. Den Fortschritt hält das Framework und rückt ihn erst vor, wenn <see cref="SchreibeAsync"/>
/// durch ist; ein Abbruch dazwischen wiederholt die Wirkungen. Darum ergibt jede Wirkung wiederholt dasselbe:
/// <list type="bullet">
///   <item><see cref="LegeAb"/>: eine Datei unter einem Pfad mit der Event-Identität (Stream und Version aus der <see cref="Herkunft"/>) — eine
///     Wiederholung schreibt dieselbe Datei mit demselben Inhalt.</item>
///   <item><see cref="LegeAbWennNeuer"/>: eine Datei, die je Schlüssel den letzten Stand hält, BEDINGT: nur wenn die Version des Eingangs größer
///     ist als die gespeicherte (unter einer Sperre geprüft und ersetzt). Sonst brächte eine zweite Aktivierung mit älterem Stand alte Werte zurück.</item>
/// </list>
/// Lesen-Ändern-Schreiben ohne Bedingung gibt es hier nicht — es zählte bei einer Wiederholung doppelt.
///
/// <b>Generationen (A4):</b> jede liegt in einem eigenen Verzeichnis <c>&lt;wurzel&gt;/&lt;generation&gt;/</c>; geschrieben wird in die Generation aus
/// dem Fortschritt (<see cref="Beginne"/>), gelesen in die, auf die <c>&lt;wurzel&gt;/aktuell</c> zeigt (<see cref="UmschaltenAsync"/>). Ein Leser der
/// alten Generation, der nach dem Neuaufbau noch schreibt, trifft das neue Ziel nicht.
///
/// Ersetzt wird eine Datei immer über eine temporäre Datei und <c>File.Move</c> — ein Leser sieht nie eine halbe.
/// </summary>
public abstract class DateiZiel : IWiederholungsfestesZiel
{
    private const string Zeiger = "aktuell", Endung = ".stand";
    private readonly string _wurzel;
    private readonly List<Wirkung> _puffer = new();
    private string _generation = Generationen.Erste;

    private sealed record Wirkung(string Pfad, byte[] Inhalt, int? Version);

    /// <param name="wurzel">Das Verzeichnis dieses Stores (aus der Umgebung, <see cref="StoreOrte"/>).</param>
    protected DateiZiel(string wurzel) => _wurzel = Path.GetFullPath(wurzel);

    // ─────────────────────────────────────────────────────────────
    // Wirkungen (für die fachlichen Schreib-Methoden)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Puffert: die Datei <paramref name="pfad"/> (relativ, mit der Event-Identität darin) mit <paramref name="inhalt"/>.</summary>
    protected Task LegeAb(string pfad, byte[] inhalt)
    {
        _puffer.Add(new Wirkung(Pruefe(pfad), inhalt, null));
        return Task.CompletedTask;
    }

    /// <summary>Puffert: die Datei <paramref name="pfad"/> mit <paramref name="inhalt"/>, nur wenn <c>h.Version</c> größer ist als der gespeicherte Stand.</summary>
    protected Task LegeAbWennNeuer(string pfad, byte[] inhalt, Herkunft h)
    {
        _puffer.Add(new Wirkung(Pruefe(pfad), inhalt, h.Version));
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────
    // Lesen (für die fachlichen Lese-Methoden): die gelesene Generation
    // ─────────────────────────────────────────────────────────────

    /// <summary>Der Inhalt der Datei <paramref name="pfad"/> in der gelesenen Generation, oder null.</summary>
    protected byte[]? Lies(string pfad)
    {
        var datei = Path.Combine(LeseVerzeichnis(), Pruefe(pfad));
        try { return File.Exists(datei) ? File.ReadAllBytes(datei) : null; }
        catch (FileNotFoundException) { return null; }
    }

    /// <summary>Die Dateien (relative Pfade) unter <paramref name="ordner"/> in der gelesenen Generation, sortiert.</summary>
    protected IReadOnlyList<string> Liste(string ordner)
    {
        var basis = LeseVerzeichnis();
        var verz = Path.Combine(basis, Pruefe(ordner));
        if (!Directory.Exists(verz)) return Array.Empty<string>();
        return Directory.EnumerateFiles(verz, "*", SearchOption.AllDirectories)
            .Where(d => !d.EndsWith(".tmp", StringComparison.Ordinal) && !d.EndsWith(Endung, StringComparison.Ordinal))
            .Select(d => Path.GetRelativePath(basis, d).Replace('\\', '/'))
            .OrderBy(d => d, StringComparer.Ordinal).ToList();
    }

    private string LeseVerzeichnis()
    {
        var zeiger = Path.Combine(_wurzel, Zeiger);
        var generation = File.Exists(zeiger) ? File.ReadAllText(zeiger).Trim() : Generationen.Erste;
        return Path.Combine(_wurzel, generation);
    }

    // ─────────────────────────────────────────────────────────────
    // IProjektionsZiel
    // ─────────────────────────────────────────────────────────────

    public void Beginne(string generation)
    {
        _puffer.Clear();
        _generation = generation;
    }

    public void Stream(Guid stream) { }

    public async Task SchreibeAsync(CancellationToken ct)
    {
        try
        {
            var verz = Path.Combine(_wurzel, _generation);
            foreach (var w in _puffer)
            {
                ct.ThrowIfCancellationRequested();
                var ziel = Path.Combine(verz, w.Pfad);
                Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
                if (w.Version is not { } version) await ErsetzeAsync(ziel, w.Inhalt, ct);
                else await ErsetzeWennNeuerAsync(ziel, w.Inhalt, version, ct);
            }
        }
        finally
        {
            _puffer.Clear();
        }
    }

    public Task NeueGenerationAsync(string generation, CancellationToken ct)
    {
        var verz = Path.Combine(_wurzel, generation);
        if (Directory.Exists(verz)) Directory.Delete(verz, recursive: true);
        Directory.CreateDirectory(verz);
        return Task.CompletedTask;
    }

    public async Task UmschaltenAsync(string generation, CancellationToken ct)
    {
        Directory.CreateDirectory(_wurzel);
        await ErsetzeAsync(Path.Combine(_wurzel, Zeiger), System.Text.Encoding.UTF8.GetBytes(generation), ct);
        foreach (var alt in Directory.EnumerateDirectories(_wurzel).Where(d => Path.GetFileName(d) != generation))
        {
            try { Directory.Delete(alt, recursive: true); }
            catch (IOException) { }   // ein alter Leser schreibt noch hinein — liegen lassen, gelesen wird es nicht
        }
    }

    // ─────────────────────────────────────────────────────────────

    private static async Task ErsetzeAsync(string ziel, byte[] inhalt, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        var tmp = ziel + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(tmp, inhalt, ct);
        File.Move(tmp, ziel, overwrite: true);
    }

    /// <summary>Bedingt ersetzen: unter einer exklusiven Sperre (<c>&lt;ziel&gt;.stand</c>, sie hält zugleich die Version) prüfen und ersetzen.</summary>
    private static async Task ErsetzeWennNeuerAsync(string ziel, byte[] inhalt, int version, CancellationToken ct)
    {
        for (var versuch = 0; ; versuch++)
        {
            FileStream sperre;
            try
            {
                sperre = new FileStream(ziel + Endung, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (versuch < 200)
            {
                await Task.Delay(5, ct);   // eine andere Aktivierung prüft gerade
                continue;
            }
            await using (sperre)
            {
                using var leser = new StreamReader(sperre, leaveOpen: true);
                var text = await leser.ReadToEndAsync(ct);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gespeichert) && gespeichert >= version) return;
                await ErsetzeAsync(ziel, inhalt, ct);
                sperre.SetLength(0);
                var bytes = System.Text.Encoding.ASCII.GetBytes(version.ToString(CultureInfo.InvariantCulture));
                await sperre.WriteAsync(bytes, ct);
                await sperre.FlushAsync(ct);
                return;
            }
        }
    }

    private static string Pruefe(string pfad)
    {
        if (Path.IsPathRooted(pfad) || pfad.Split('/', '\\').Any(t => t is ".." or ""))
            throw new ArgumentException($"'{pfad}' ist kein relativer Pfad innerhalb des Stores.", nameof(pfad));
        return pfad;
    }
}

/// <summary>
/// Ein Store der <b>Klasse 3</b> (<see cref="INurMindestensEinmal"/>): hängt je Wirkung eine Zeile an eine Datei — ohne Bedingung, ohne Event-Identität.
/// Nach einem Abbruch zwischen Schreiben und Fortschritt steht eine Zeile doppelt (mindestens einmal). Für Beobachtung (Metriken, Protokolle); eine
/// Projektion darf hier nur mit ausdrücklicher Erlaubnis schreiben (<see cref="MindestensEinmalAttribute"/>, R14). Generationen wie <see cref="DateiZiel"/>.
/// </summary>
public abstract class DateiSink : INurMindestensEinmal
{
    private const string Zeiger = "aktuell";
    private readonly string _wurzel;
    private readonly List<(string Datei, string Zeile)> _puffer = new();
    private string _generation = Generationen.Erste;

    protected DateiSink(string wurzel) => _wurzel = Path.GetFullPath(wurzel);

    /// <summary>Puffert: eine Zeile an die Datei <paramref name="datei"/> hängen.</summary>
    protected Task HaengeAn(string datei, string zeile)
    {
        if (Path.IsPathRooted(datei) || datei.Contains("..", StringComparison.Ordinal) || zeile.Contains('\n'))
            throw new ArgumentException($"'{datei}' bzw. die Zeile taugt nicht für die Senke.");
        _puffer.Add((datei, zeile));
        return Task.CompletedTask;
    }

    /// <summary>Die Zeilen der Datei <paramref name="datei"/> in der gelesenen Generation.</summary>
    protected IReadOnlyList<string> Zeilen(string datei)
    {
        var zeiger = Path.Combine(_wurzel, Zeiger);
        var generation = File.Exists(zeiger) ? File.ReadAllText(zeiger).Trim() : Generationen.Erste;
        var pfad = Path.Combine(_wurzel, generation, datei);
        return File.Exists(pfad) ? File.ReadAllLines(pfad) : Array.Empty<string>();
    }

    public void Beginne(string generation)
    {
        _puffer.Clear();
        _generation = generation;
    }

    public void Stream(Guid stream) { }

    public async Task SchreibeAsync(CancellationToken ct)
    {
        try
        {
            foreach (var g in _puffer.GroupBy(p => p.Datei))
            {
                var pfad = Path.Combine(_wurzel, _generation, g.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
                await File.AppendAllLinesAsync(pfad, g.Select(p => p.Zeile), ct);
            }
        }
        finally
        {
            _puffer.Clear();
        }
    }

    public Task NeueGenerationAsync(string generation, CancellationToken ct)
    {
        var verz = Path.Combine(_wurzel, generation);
        if (Directory.Exists(verz)) Directory.Delete(verz, recursive: true);
        Directory.CreateDirectory(verz);
        return Task.CompletedTask;
    }

    public async Task UmschaltenAsync(string generation, CancellationToken ct)
    {
        Directory.CreateDirectory(_wurzel);
        var tmp = Path.Combine(_wurzel, Zeiger + "." + Guid.NewGuid().ToString("N") + ".tmp");
        await File.WriteAllTextAsync(tmp, generation, ct);
        File.Move(tmp, Path.Combine(_wurzel, Zeiger), overwrite: true);
        foreach (var alt in Directory.EnumerateDirectories(_wurzel).Where(d => Path.GetFileName(d) != generation))
        {
            try { Directory.Delete(alt, recursive: true); }
            catch (IOException) { }
        }
    }
}

/// <summary>
/// Wo die Stores einer Umgebung im Dateisystem liegen (Abschnitt <c>Stores:&lt;Store&gt;:Verzeichnis</c>); ohne Eintrag unter dem Temp-Verzeichnis
/// je Datenbank und Schema des Logs — so gehören die Dateien zu genau einem Log.
/// </summary>
public sealed class StoreOrte(UmgebungKonfig umgebung)
{
    public string Verzeichnis(string store)
        => umgebung.Stores.TryGetValue(store, out var k) && k.Verzeichnis is { Length: > 0 } v
            ? v
            : Path.Combine(Path.GetTempPath(), "cqrs-stores", $"{umgebung.EventStore.Datenbank}_{umgebung.EventStore.Schema}", store);
}
