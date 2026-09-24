namespace Abstractions;

/// <summary>Welche Abbildung eine generierte Routing-Tabelle trägt.</summary>
public enum RoutingArt
{
    /// <summary>Command-Typ → Aggregat-Name.</summary>
    CommandZuAggregat = 1,
    /// <summary>Command-Typ → die (persistierten) Event-Typen, die er erzeugen kann.</summary>
    CommandZuEvents = 2,
}

/// <summary>
/// Markiert eine vom Framework-Generator erzeugte Routing-Tabelle (die Abbildung, nach der die Laufzeit routet). Der
/// Anker, über den der Domänen-Extractor die Routing-Wahrheit und das Laufzeit-Projekt findet — statt die Tabelle an
/// ihrer Syntaxform zu erraten.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class RoutingTabelleAttribute : Attribute
{
    public RoutingTabelleAttribute(RoutingArt art) => Art = art;

    public RoutingArt Art { get; }
}
