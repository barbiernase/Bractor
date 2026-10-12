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
