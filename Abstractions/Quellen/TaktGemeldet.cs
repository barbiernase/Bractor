using System;

namespace Abstractions;

/// <summary>
/// Was der TAKT-Treiber meldet (ersetzt den Timer-Trigger der Handle-Welt): ein Zeitpunkt ist erreicht — je
/// Intervall genau einmal im Log (die Kennung ist der Beginn des Intervalls, über Knoten und Neustarts hinweg derselbe). Die Domäne benennt die Quelle
/// nur als Typ: <c>public sealed record Stundentakt : TaktGemeldet;</c> (CQRS070); wie oft, sagt die Konfig (<c>Quellen:&lt;Typ&gt;:Intervall</c>).
/// Gezählt wird mit der Uhr der Datenbank (eine Uhr, G-10); ein Intervall, das während einer Pause aller Knoten verstrich, kommt nicht nach.
/// </summary>
public abstract record TaktGemeldet : IQuellNachricht
{
    /// <summary>Der Beginn des Intervalls (Datenbank-Uhr), auf das Intervall gerundet.</summary>
    public required DateTimeOffset Zeitpunkt { get; init; }

    /// <summary>Der Zeitpunkt als Text (ISO 8601, UTC) — dasselbe Intervall startet genau einen Vorgang.</summary>
    public required string Kennung { get; init; }
}

/// <summary>Die Konfig einer Takt-Quelle (Abschnitt <c>Quellen:&lt;Typ&gt;</c>): das Intervall (mindestens eine Sekunde).</summary>
public sealed class TaktQuellKonfig : QuellKonfig
{
    public TimeSpan Intervall { get; set; } = TimeSpan.FromMinutes(1);
}
