using Abstractions;
using Marten;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Infrastructure.Projections;

/// <summary>
/// Der Code-Stand einer replaybaren Projektion (vom PullPathGenerator registriert): Batch-Kind (<paramref name="KindName"/>),
/// Schlüssel der Partitionen und Guards (<paramref name="ProjektionsId"/> = <c>SubscriberId</c>), Hash der Klasse und die Store-Klassen hinter
/// ihren Schreib-Fähigkeiten. Emittierende Konsumenten haben keinen Stand — sie werden nie neu aufgebaut.
/// </summary>
public sealed record ProjektionsStand(string KindName, string ProjektionsId, string KlassenHash, IReadOnlyList<Type> Stores);

/// <summary>
/// „Passt die Leseseite noch zum Code?" (docs/konzept-werkbank.md §4.6, §12.3 E9). Läuft beim Start nach dem Siegel und VOR dem
/// Pull-Pfad: der Code-Hash jeder Projektion (Klasse + Store) wird mit dem in <c>&lt;schema&gt;.stand</c> gespeicherten verglichen.
/// Zum Stand gehört auch die Form des Konsums (<see cref="KonsumForm"/>, Partitionen): ein Wechsel der Partitionszahl ist ein Neuaufbau.
/// <list type="bullet">
///   <item><b>Sim</b>, mindestens eine Projektion geändert (oder ohne gespeicherten Stand): die ganze Leseseite wird geleert (alle
///     Read-Model-Typen), die Partitionen und Guards aller Projektionen auf Anfang gesetzt. Der erste Poll weckt danach jede Partition, die
///     Leser lesen ab 0 seitenweise — Neuaufbau IST der normale Batch-Konsum (wie <see cref="ProjectionRebuilder"/>). Geleert wird alles, weil
///     Read-Models nicht einer Projektion gehören müssen; in Sim ist das billig und immer richtig.</item>
///   <item><b>Prod</b>: nur eine Meldung (ein Neuaufbau ist eine bewusste Betriebsentscheidung), der gespeicherte Stand bleibt.</item>
/// </list>
/// </summary>
public sealed class ProjektionsStandDienst(
    UmgebungKonfig umgebung,
    IEnumerable<ProjektionsStand> projektionen,
    IEnumerable<StoreStand> stores,
    IEnumerable<ReadModelTyp> readModels,
    IDocumentStore marten,
    Konsum konsum) : IHostedService
{
    public const string Praefix = "projektion:";

    /// <summary>Die Form des Konsums, die der gespeicherte Stand voraussetzt: Batch-Konsum mit P Partitionen.</summary>
    public static string KonsumForm(Konsum konsum) => $"batch/{konsum.Partitionen}";

    /// <summary>Der Hash je Projektions-Id: Klasse + Stores (Reihenfolge der Store-Typen wie generiert) + Form des Konsums.</summary>
    public static IReadOnlyDictionary<string, string> Hashes(IEnumerable<ProjektionsStand> projektionen, IEnumerable<StoreStand> stores, Konsum konsum)
    {
        var je = stores.ToDictionary(s => s.Store, s => s.CodeHash);
        return projektionen.ToDictionary(p => p.ProjektionsId,
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

        // Leseseite leeren, Partitionen und Guards aller Projektionen auf Anfang.
        foreach (var t in readModels.Select(r => r.Typ).Distinct())
            await marten.Advanced.Clean.DeleteDocumentsByTypeAsync(t, ct);
        await using (var s = marten.LightweightSession())
        {
            foreach (var p in liste)
                await Core.KonsumZuruecksetzen.PartitionenUndGuardsAsync(s, p.ProjektionsId, ct);
            await s.SaveChangesAsync(ct);
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
        Console.WriteLine($"[CQRS] Leseseite neu: Code von {string.Join(", ", geaendert)} geändert — {liste.Count} Projektion(en) bauen aus dem Log neu auf.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
