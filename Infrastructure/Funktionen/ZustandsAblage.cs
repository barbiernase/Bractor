using Marten;
using Npgsql;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// GESICHERTER ZUSTAND (docs/04 §4.4, Entscheidung 9): IZustand<T> je Instanz und Knoten, ein Schreiber (die Sperre der Instanz, ein Kind). Geladen beim
// Aktivieren; gesichert, wenn der Aufruf, der ihn geändert hat, und alle früheren beendet sind — mit den Kennungen dieser Aufrufe (der Parent hält
// die Schnappschüsse; was der Stand enthält, rechnet nach einem Absturz niemand neu). Bei einer Quelle zählt die laufende Nummer ihrer Elemente mit
// (der Graph-Actor sichert sie, wenn die Kette des Elements und alle früheren beendet sind): nach einem Neustart rechnet sie dort weiter.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Was zu einem Knoten einer Instanz gesichert ist: das Bild seiner Zustände (<see cref="Zustandsbild"/>) und die Nummer des letzten Elements einer Quelle.</summary>
public sealed record GesicherterZustand(byte[] Bild, long Nummer);

/// <summary>Wo die Zustände der Knoten liegen (Naht; live <see cref="ZustandsAblage"/>).</summary>
public interface IZustandsAblage
{
    /// <summary>Der gesicherte Zustand eines Knotens, oder null (noch nie gesichert: der Anfangszustand).</summary>
    Task<GesicherterZustand?> LadeAsync(string instanz, string knoten, CancellationToken ct);

    /// <summary>Sichert die Zustände (je Knoten der neueste; ein älterer Stand überschreibt nie einen neueren).</summary>
    Task SichereAsync(IReadOnlyCollection<(string Instanz, string Knoten, GesicherterZustand Zustand)> zustaende, CancellationToken ct);
}

/// <summary>Die Zustände in einer Tabelle im Event-Schema (<see cref="Tabelle"/>), je Instanz und Knoten eine Zeile.</summary>
public sealed class ZustandsAblage(IDocumentStore store) : IZustandsAblage
{
    public const string Tabelle = "cqrs_zustaende";

    public static void Registriere(StoreOptions options)
    {
        var t = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Tabelle));
        t.AddColumn<string>("instanz").AsPrimaryKey();
        t.AddColumn<string>("knoten").AsPrimaryKey();
        t.AddColumn("bild", "bytea").NotNull();
        t.AddColumn<long>("nummer").NotNull();
        t.AddColumn("gesichert", "timestamptz").NotNull().DefaultValueByExpression("now()");
        options.Storage.ExtendedSchemaObjects.Add(t);
    }

    public static string Name(IDocumentStore store) => $"{store.Options.Events.DatabaseSchemaName}.{Tabelle}";

    public async Task<GesicherterZustand?> LadeAsync(string instanz, string knoten, CancellationToken ct)
    {
        await Persistence.InboxTabelle.SicherAsync(store, ct);   // rohes SQL löst Martens AutoCreate nicht aus
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"select bild, nummer from {Name(store)} where instanz = $1 and knoten = $2", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = instanz });
        cmd.Parameters.Add(new NpgsqlParameter { Value = knoten });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new GesicherterZustand(r.GetFieldValue<byte[]>(0), r.GetInt64(1)) : null;
    }

    public async Task SichereAsync(IReadOnlyCollection<(string Instanz, string Knoten, GesicherterZustand Zustand)> zustaende, CancellationToken ct)
    {
        if (zustaende.Count == 0) return;
        await Persistence.InboxTabelle.SicherAsync(store, ct);
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"insert into {Name(store)} (instanz, knoten, bild, nummer) select * from unnest($1::text[], $2::text[], $3::bytea[], $4::bigint[]) "
            + "on conflict (instanz, knoten) do update set bild = excluded.bild, nummer = excluded.nummer, gesichert = now() "
            + $"where {Name(store)}.nummer <= excluded.nummer", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = zustaende.Select(z => z.Instanz).ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = zustaende.Select(z => z.Knoten).ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = zustaende.Select(z => z.Zustand.Bild).ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = zustaende.Select(z => z.Zustand.Nummer).ToArray() });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Das Bild der Zustände einer Aktivierung: der eigene (Schlüssel <c>""</c>) und der jeder Technik in ihrer Konstruktor-Kette (Schlüssel = ihr Name), je
/// über den generierten Serializer-Kontext geschrieben — gepackt ohne Reflection (Anzahl, je Eintrag Name und Bytes).
/// </summary>
public static class Zustandsbild
{
    public static byte[] Packe(IReadOnlyDictionary<string, byte[]> je)
    {
        using var m = new MemoryStream();
        using (var w = new BinaryWriter(m))
        {
            w.Write(je.Count);
            foreach (var (name, daten) in je.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                w.Write(name);
                w.Write(daten.Length);
                w.Write(daten);
            }
        }
        return m.ToArray();
    }

    public static Dictionary<string, byte[]> Entpacke(byte[] bild)
    {
        var je = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var r = new BinaryReader(new MemoryStream(bild));
        var n = r.ReadInt32();
        for (var i = 0; i < n; i++)
        {
            var name = r.ReadString();
            je[name] = r.ReadBytes(r.ReadInt32());
        }
        return je;
    }
}
