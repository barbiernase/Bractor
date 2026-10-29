using Abstractions;
using Marten;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence;

/// <summary>
/// Welche Vorgänge es schon gibt — eine Abfrage auf <c>mt_streams</c> (ein Stream je Nachricht) und auf das Kennungs-Verzeichnis der Quellen (ein Stream
/// je Schlüssel) mit <c>= any(@ids)</c>.
/// </summary>
public sealed class MartenStreamBestand(IDocumentStore store) : IStreamBestand
{
    public async Task<IReadOnlySet<Guid>> BekannteAsync(IReadOnlyCollection<Guid> streams, CancellationToken ct = default)
    {
        var da = new HashSet<Guid>();
        if (streams.Count == 0) return da;
        await using var session = store.QuerySession();
        var conn = (NpgsqlConnection)session.Connection;
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        var schema = store.Options.Events.DatabaseSchemaName;
        await using var cmd = conn.CreateCommand();
        await InboxTabelle.SicherAsync(store, ct);
        cmd.CommandText = $"select id from \"{schema}\".mt_streams where id = any(@ids) "
            + $"union select vorgang from {Infrastructure.Quellen.QuellKennungen.Name(store)} where vorgang = any(@ids)";
        cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = streams.ToArray() });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) da.Add(reader.GetGuid(0));
        return da;
    }
}
