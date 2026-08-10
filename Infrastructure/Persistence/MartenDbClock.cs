using System.Data;
using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Die DB-Uhr über Marten/Postgres: <c>SELECT now()</c> auf der Session-Verbindung. Damit laufen alle
/// Fälligkeits-Entscheidungen über die EINE Postgres-Uhr statt über (bei Multi-Node driftende) Node-Uhren.
/// </summary>
public sealed class MartenDbClock : IDbClock
{
    private readonly IDocumentStore _store;

    public MartenDbClock(IDocumentStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<DateTimeOffset> JetztAsync(CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        var conn = session.Connection;
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select now()";
        var wert = await cmd.ExecuteScalarAsync(ct);

        return wert switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("now() lieferte keinen Zeitwert"),
        };
    }
}
