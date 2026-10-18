using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Infrastructure.Persistence;

/// <summary>
/// Schlüssel-Präfix des Version-Index (docs/konzept-werkbank.md §4.3): zwei Umgebungen auf demselben Redis mischen sich nie
/// (<c>prod:</c> vs. <c>sim:&lt;branch&gt;:</c>). Leer = kein Präfix (bisheriges Verhalten).
/// </summary>
public sealed record RedisPraefix(string Wert)
{
    /// <summary>Die Datenbank des Multiplexers, bei gesetztem Präfix mit Schlüssel-Isolation.</summary>
    public static IDatabase Db(IConnectionMultiplexer redis, RedisPraefix? praefix)
        => praefix is { Wert.Length: > 0 } p ? redis.GetDatabase().WithKeyPrefix(p.Wert) : redis.GetDatabase();
}

/// <summary>
/// Wie lange ein Schlüssel des Version-Index ohne neuen Schreibvorgang lebt (Umgebung <c>VersionIndex.HaltbarkeitStunden</c>). Der Index trägt
/// nur „Read your writes“ für Sekunden; ohne Ablauf wüchse Redis mit jedem Aggregat und jedem Read-Model, das es je gab, bis der Speicher voll
/// ist. Ein abgelaufener Schlüssel ist wie ein Redis-Ausfall: keine Information, niemand wartet.
/// </summary>
public sealed record VersionIndexHaltbarkeit(TimeSpan Dauer)
{
    public static VersionIndexHaltbarkeit Standard { get; } = new(TimeSpan.FromHours(24));
}
