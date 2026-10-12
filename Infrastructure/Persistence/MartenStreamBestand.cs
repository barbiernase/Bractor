using Abstractions;
using Marten;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence;

/// <summary>Welche Streams existieren — eine Abfrage auf <c>mt_streams</c> mit <c>id = any(@ids)</c>.</summary>
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
        cmd.CommandText = $"select id from \"{schema}\".mt_streams where id = any(@ids)";
        cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = streams.ToArray() });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) da.Add(reader.GetGuid(0));
        return da;
    }
}
