namespace Abstractions;

/// <summary>
/// Die DB-Uhr — die EINE Zeitquelle für Fälligkeits-Entscheidungen (Querschnitts-Regel: Zeit-Entscheidungen
/// per DB-Uhr, NIE per Node-<c>DateTime.UtcNow</c>). Grund: bei mehreren Nodes driften lokale Uhren; die
/// gemeinsame Postgres-Uhr macht „ist fällig?" deterministisch und knoten-unabhängig.
///
/// Informative Zeitstempel (z. B. „seit wann offen") dürfen weiter Node-Zeit nutzen — nur ENTSCHEIDUNGEN
/// (Fälligkeit einer Frist) müssen über diese Uhr laufen.
/// </summary>
public interface IDbClock
{
    /// <summary>Die aktuelle Zeit aus der Datenbank (<c>SELECT now()</c>).</summary>
    Task<DateTimeOffset> JetztAsync(CancellationToken ct = default);
}
