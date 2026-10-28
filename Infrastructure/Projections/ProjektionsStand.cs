using Abstractions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Infrastructure.Projections;

/// <summary>
/// Eine Projektion, wie der Generator sie registriert (PullPathGenerator): ihre Kennung (Typname), das Batch-Kind, die gehörten Event-Typen,
/// der Code-Stand ihrer Klasse, die Store-Klassen hinter ihren Schreib-Fähigkeiten und wie man ihren Store aus einem Fähigkeits-Bereich holt
/// (dieselbe Instanz unter jeder Fähigkeit). Daraus lesen Neuaufbau (<see cref="ProjektionsStandDienst"/>) und Stand je Id (<see cref="ProjektionsStaende"/>).
/// </summary>
public sealed record Projektionseintrag(
    string Kennung,
    string KindName,
    IReadOnlyList<Type> Typen,
    string KlassenHash,
    IReadOnlyList<Type> Stores,
    Func<IFaehigkeiten, IProjektionsZiel> Ziel)
{
    /// <summary>Die Naht zum Store dieser Projektion in einem Fähigkeits-Bereich.</summary>
    public Projektionsnaht Naht(IFaehigkeiten faehigkeiten, IFortschrittBeimFramework? framework) => new(Ziel(faehigkeiten), framework);
}

/// <summary>
/// „Passt die Leseseite noch zum Code?" (docs/konzept-werkbank.md §4.6, §12.3 E9). Läuft beim Start nach dem Siegel und VOR dem
/// Pull-Pfad: der Code-Hash jeder Projektion (Klasse + Store) wird mit dem in <c>&lt;schema&gt;.stand</c> gespeicherten verglichen.
/// Zum Stand gehört auch die Form des Konsums (<see cref="KonsumForm"/>, Partitionen): ein Wechsel der Partitionszahl ist ein Neuaufbau.
/// <list type="bullet">
///   <item><b>Sim</b>, mindestens eine Projektion geändert (oder ohne gespeicherten Stand): jede Projektion bekommt eine neue Generation (ihr Store
///     ein leeres Ziel — bei Klasse 1 geleert —, der Fortschritt aller Partitionen auf Anfang) und wird sofort umgeschaltet. Der erste Takt weckt
///     danach jede Partition, die Leser lesen ab 0 seitenweise — Neuaufbau IST der normale Batch-Konsum (wie <see cref="ProjectionRebuilder"/>, nur
///     ohne auf das Ende zu warten: in Sim ist eine leere Sicht während des Aufbaus billig und richtig). Neu aufgebaut wird die ganze Leseseite,
///     weil ein Store mehreren Projektionen dienen kann.</item>
///   <item><b>Prod</b>: nur eine Meldung (ein Neuaufbau ist eine bewusste Betriebsentscheidung), der gespeicherte Stand bleibt.</item>
/// </list>
/// </summary>
public sealed class ProjektionsStandDienst(
    UmgebungKonfig umgebung,
    IEnumerable<Projektionseintrag> projektionen,
    IEnumerable<StoreStand> stores,
    IFaehigkeitsFabrik faehigkeiten,
    IFortschrittBeimFramework framework,
    Konsum konsum) : IHostedService
{
    public const string Praefix = "projektion:";

    /// <summary>Die Form des Konsums, die der gespeicherte Stand voraussetzt: Batch-Konsum mit P Partitionen.</summary>
    public static string KonsumForm(Konsum konsum) => $"batch/{konsum.Partitionen}";

    /// <summary>Der Hash je Projektion: Klasse + Stores (Reihenfolge der Store-Typen wie generiert) + Form des Konsums.</summary>
    public static IReadOnlyDictionary<string, string> Hashes(IEnumerable<Projektionseintrag> projektionen, IEnumerable<StoreStand> stores, Konsum konsum)
    {
        var je = stores.ToDictionary(s => s.Store, s => s.CodeHash);
        return projektionen.ToDictionary(p => p.Kennung,
            p => Kodehash.Kombiniere([p.KlassenHash, .. p.Stores.Select(t => je.TryGetValue(t, out var h) ? h : t.FullName ?? t.Name), KonsumForm(konsum)]),
            StringComparer.Ordinal);
    }

    /// <summary>Die Projektionen, deren Code nicht zum gespeicherten Stand passt (fehlt der Stand: geändert).</summary>
    public static IReadOnlyList<string> Geaendert(IReadOnlyDictionary<string, string> aktuell, IReadOnlyDictionary<string, string> gespeichert)
        => aktuell.Where(a => !gespeichert.TryGetValue(a.Key, out var g) || g != a.Value).Select(a => a.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public async Task StartAsync(CancellationToken ct)
    {
        var liste = projektionen.ToList();
        if (liste.Count == 0) return;
        var aktuell = Hashes(liste, stores, konsum);
        var schema = umgebung.EventStore.Schema;
        await using var conn = new NpgsqlConnection(Umgebung.UmgebungLader.Verbindung(umgebung.EventStore));
        await conn.OpenAsync(ct);
        var gespeichert = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                create schema if not exists "{schema}";
                create table if not exists "{schema}".stand (schluessel text primary key, wert text not null);
                select schluessel, wert from "{schema}".stand where schluessel like '{Praefix}%';
                """;
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) gespeichert[r.GetString(0)[Praefix.Length..]] = r.GetString(1);
        }
        var geaendert = Geaendert(aktuell, gespeichert);
        if (geaendert.Count == 0) return;

        if (umgebung.Umgebung.Art != UmgebungsArt.Sim)
        {
            Console.WriteLine($"  ⚠ Leseseite: Code der Projektion(en) {string.Join(", ", geaendert)} geändert — in {umgebung.Umgebung.Art} kein automatischer Neuaufbau.");
            return;
        }

        // Jede Projektion in eine neue Generation, Fortschritt auf Anfang, sofort umschalten.
        var generation = Generationen.Neu(DateTimeOffset.UtcNow);
        foreach (var p in liste)
        {
            using var bereich = faehigkeiten.Oeffne();
            var naht = p.Naht(bereich, framework);
            await naht.Ziel.NeueGenerationAsync(generation, ct);
            await naht.Ablage.FortschrittZuruecksetzenAsync(p.Kennung, konsum.Partitionen, generation, ct);
            await naht.Ziel.UmschaltenAsync(generation, ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                delete from "{schema}".stand where schluessel like '{Praefix}%';
                insert into "{schema}".stand (schluessel, wert) select unnest(@k), unnest(@w);
                """;
            cmd.Parameters.AddWithValue("k", aktuell.Keys.Select(k => Praefix + k).ToArray());
            cmd.Parameters.AddWithValue("w", aktuell.Values.ToArray());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        Console.WriteLine($"[CQRS] Leseseite neu: Code von {string.Join(", ", geaendert)} geändert — {liste.Count} Projektion(en) bauen aus dem Log neu auf (Generation {generation}).");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
