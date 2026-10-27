using System.Text;
using Abstractions;
using Marten;
using Marten.Exceptions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// ZUSTAND EINER FUNKTION (Plan Pub/Sub-Neustrukturierung §2.9, E6, A5, A6): gekapselt in der Aktivierung des Funktions-Actors, gesichert
// ZUSAMMEN MIT DER POSITION — ein Dokument je Adresse, geschrieben in derselben Transaktion wie der Stand der Partition (beide mit erwarteter
// Version: eine zweite Aktivierung derselben Adresse verliert ihren ganzen Schreibversuch). Ein großer Zustand liegt als Datei in einer Ablage der
// Umgebung; dann wird ZUERST die Datei geschrieben, DANN Position und Verweis — ein Absturz dazwischen lässt nur eine verwaiste Datei zurück,
// der gesicherte Stand bleibt der alte (Zustand und Position passen zusammen). Keine Zustands-Events, keine Faltung aus dem Log.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Der Zustand einer Aktivierung (die Umsetzung bekommt ihn als <see cref="IZustand{T}"/> in den Konstruktor).</summary>
public sealed class Zustand<T> : IZustand<T> where T : class, new()
{
    public T Wert { get; set; } = new();
}

/// <summary>
/// Was der Generator je Funktion mit Zustand beibringt (<c>GeneratedFunktionsBausteine</c>): der Typ, ein frischer Anfangszustand, und Sichern bzw.
/// Laden über den generierten Serializer-Kontext (ohne Reflection), und zurück auf den Anfangszustand (nach einem Konflikt mit einer zweiten Aktivierung).
/// </summary>
public sealed record FunktionsZustandsform(Type Typ, Func<object> Neu, Func<object, byte[]> Schreibe, Action<object, byte[]> Lies, Action<object> Anfang);

/// <summary>
/// Wie der Zustand einer Funktion gesichert wird (Umgebung <c>Funktionen:&lt;F&gt;:Zustand</c>): Ablage und Verzeichnis, und wie oft höchstens —
/// Standard wie der Stand (<see cref="FunktionsActor.StandAlle"/>); beim Stoppen, Passivieren und Umziehen immer.
/// </summary>
public sealed record ZustandsBindung(ZustandsAblage Ablage = ZustandsAblage.Dokument, string? Verzeichnis = null, TimeSpan? SichernAlle = null)
{
    public static readonly ZustandsBindung Standard = new();

    /// <summary>Aus dem Abschnitt der Umgebung (ohne Eintrag: <see cref="Standard"/>).</summary>
    public static ZustandsBindung Aus(ZustandKonfig? k) => k is null ? Standard
        : new(k.Ablage, string.IsNullOrWhiteSpace(k.Verzeichnis) ? null : k.Verzeichnis,
            k.SichernAlleSekunden > 0 ? TimeSpan.FromSeconds(k.SichernAlleSekunden) : null);
}

/// <summary>
/// Der gesicherte Zustand einer Funktion an einer Adresse (Marten-Dokument, numerische Revision auf <see cref="Version"/>): die Position, bis zu der
/// er jeden Log-Eingang enthält, und entweder die Daten selbst (<see cref="Daten"/>, das JSON des Zustands) oder ein <see cref="Verweis"/> in die Ablage.
/// </summary>
public sealed class FunktionsZustandsDokument
{
    public string Id { get; set; } = "";
    public string Funktion { get; set; } = "";
    public string Adresse { get; set; } = "";
    /// <summary>Bis hierher ist jeder Log-Eingang der Adresse im Zustand enthalten (dieselbe Zahl wie die Position im Stand).</summary>
    public long Position { get; set; }
    public string? Daten { get; set; }
    public string? Verweis { get; set; }
    public int Groesse { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Marten-Registrierung (numerische Revision wie beim Partitions-Checkpoint).</summary>
    public static void Registriere(StoreOptions options) =>
        options.Schema.For<FunktionsZustandsDokument>().Identity(x => x.Id).UseNumericRevisions(true).Metadata(m => m.Revision.MapTo(x => x.Version));
}

/// <summary>Eine Ablage für große Zustände (Datei, später Objektspeicher): schreiben, lesen, entfernen — der Verweis steht im Dokument.</summary>
public interface IZustandsDateien
{
    Task<string> LegeAbAsync(string funktion, string adresse, byte[] daten, CancellationToken ct);
    Task<byte[]> LiesAsync(string verweis, CancellationToken ct);
    Task EntferneAsync(string verweis, CancellationToken ct);
}

/// <summary>Die Datei-Ablage: je Sicherung eine neue Datei (nie überschrieben — so bleibt die gesicherte bis zum Commit des Verweises gültig).</summary>
public sealed class VerzeichnisZustandsDateien(string verzeichnis) : IZustandsDateien
{
    public async Task<string> LegeAbAsync(string funktion, string adresse, byte[] daten, CancellationToken ct)
    {
        var verweis = Path.Combine(funktion, adresse, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
        var pfad = Path.Combine(verzeichnis, verweis);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        var neu = pfad + ".neu";
        await File.WriteAllBytesAsync(neu, daten, ct);
        File.Move(neu, pfad);
        return verweis;
    }

    public Task<byte[]> LiesAsync(string verweis, CancellationToken ct) => File.ReadAllBytesAsync(Path.Combine(verzeichnis, verweis), ct);

    public Task EntferneAsync(string verweis, CancellationToken ct)
    {
        var pfad = Path.Combine(verzeichnis, verweis);
        if (File.Exists(pfad)) File.Delete(pfad);
        return Task.CompletedTask;
    }
}

/// <summary>Was geladen wurde: die Daten des Zustands (oder null = noch nie gesichert), seine Position und Version, ein Verweis in die Ablage.</summary>
public sealed record GeladenerZustand(byte[]? Daten, long Position, int Version, string? Verweis)
{
    public static readonly GeladenerZustand Nichts = new(null, 0, 0, null);
}

/// <summary>
/// Laden und Sichern des Zustands einer Adresse — die Naht zwischen dem Funktions-Actor und dem Store: zuerst (großer Zustand) die Datei, dann in
/// EINER Transaktion Stand (Position) und Zustands-Dokument, beide mit erwarteter Version (<see cref="IFunktionsStand.SichereAsync"/>).
/// </summary>
public sealed class ZustandsSicherung(IFunktionsStand staende, ZustandsBindung bindung, IZustandsDateien? dateien)
{
    public ZustandsBindung Bindung => bindung;

    /// <summary>Wie oft höchstens gesichert wird.</summary>
    public TimeSpan Alle => bindung.SichernAlle ?? FunktionsActor.StandAlle;

    public async Task<GeladenerZustand> LadeAsync(string id, CancellationToken ct)
    {
        var dok = await staende.LadeZustandAsync(id, ct);
        if (dok is null) return GeladenerZustand.Nichts;
        byte[]? daten = dok.Verweis is { } v
            ? await (dateien ?? throw new InvalidOperationException($"Zustand {id} liegt als Datei ({v}), aber die Umgebung nennt keine Ablage.")).LiesAsync(v, ct)
            : dok.Daten is { } d ? Encoding.UTF8.GetBytes(d) : null;
        return new GeladenerZustand(daten, dok.Position, dok.Version, dok.Verweis);
    }

    /// <summary>
    /// Sichert <paramref name="daten"/> mit der Position und (bei einer Funktion mit Log-Eingängen) dem Stand. Liefert die neuen Versionen und den
    /// Verweis; ein Konflikt (zweite Aktivierung) wirft <see cref="PartitionsKonfliktException"/> — dann gilt der Stand des anderen.
    /// </summary>
    public async Task<(int? StandVersion, int ZustandVersion, string? Verweis)> SichereAsync(string funktion, string adresse, PartitionsCheckpoint? stand,
        long position, byte[] daten, int zustandVersion, string? alterVerweis, CancellationToken ct)
    {
        var dok = new FunktionsZustandsDokument
        {
            Id = Funktionsvertrag.ZustandId(funktion, adresse), Funktion = funktion, Adresse = adresse, Position = position, Groesse = daten.Length,
            Version = zustandVersion, UpdatedAt = DateTimeOffset.UtcNow,
        };
        string? verweis = null;
        if (bindung.Ablage == ZustandsAblage.Datei)
            dok.Verweis = verweis = await (dateien ?? throw new InvalidOperationException(
                $"Funktionen:{funktion}:Zustand:Ablage = Datei braucht ein Verzeichnis (Zustand:Verzeichnis).")).LegeAbAsync(funktion, adresse, daten, ct);
        else dok.Daten = Encoding.UTF8.GetString(daten);
        try
        {
            var (s, z) = await staende.SichereAsync(stand, dok, ct);
            if (alterVerweis is not null && dateien is not null)
            {
                try { await dateien.EntferneAsync(alterVerweis, CancellationToken.None); } catch { /* verwaist, harmlos */ }
            }
            return (s, z, verweis);
        }
        catch
        {
            if (verweis is not null && dateien is not null)
            {
                try { await dateien.EntferneAsync(verweis, CancellationToken.None); } catch { /* verwaist, harmlos */ }
            }
            throw;
        }
    }
}

/// <summary>Marten-Seite des Zustands (im selben Store wie der Stand).</summary>
internal static class MartenZustand
{
    public static async Task<FunktionsZustandsDokument?> LadeAsync(IDocumentStore store, string id, CancellationToken ct)
    {
        await using var s = store.QuerySession();
        return await s.LoadAsync<FunktionsZustandsDokument>(id, ct);
    }

    /// <summary>Stand (optional) und Zustand in EINER Transaktion, je mit erwarteter Version.</summary>
    public static async Task<(int? Stand, int Zustand)> SichereAsync(IDocumentStore store, PartitionsCheckpoint? stand, FunktionsZustandsDokument zustand,
        CancellationToken ct)
    {
        try
        {
            await using var s = store.LightweightSession();
            int? standVersion = null;
            if (stand is not null)
            {
                var neu = new PartitionsCheckpoint
                {
                    Id = stand.Id, ProjektionsId = stand.ProjektionsId, Partition = stand.Partition, Position = stand.Position,
                    Geparkt = stand.Geparkt.ToList(), UpdatedAt = DateTimeOffset.UtcNow, Version = stand.Version + 1,
                };
                if (stand.Version == 0) s.Insert(neu);
                else s.UpdateRevision(neu, stand.Version + 1);
                standVersion = neu.Version;
            }
            var erwartet = zustand.Version;
            zustand.Version = erwartet + 1;
            if (erwartet == 0) s.Insert(zustand);
            else s.UpdateRevision(zustand, erwartet + 1);
            await s.SaveChangesAsync(ct);
            return (standVersion, zustand.Version);
        }
        catch (Exception ex) when (ex is JasperFx.ConcurrencyException or DocumentAlreadyExistsException
                                       || (ex is AggregateException a && a.InnerExceptions.All(i => i is JasperFx.ConcurrencyException or DocumentAlreadyExistsException)))
        {
            throw new PartitionsKonfliktException($"Zustand {zustand.Id} zwischenzeitlich geschrieben (erwartet v{zustand.Version - 1}).", ex);
        }
    }
}
