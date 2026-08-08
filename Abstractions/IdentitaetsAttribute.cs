namespace Abstractions;

/// <summary>
/// ★ P1c (TG-3): optionaler, EXPLIZITER Identitäts-Name eines Aggregats. Ohne Attribut ist die Identität
/// der einfache Typname des <c>IState</c> (Default → KEINE Migration bestehender Streams/Kinds). Mit Attribut
/// wird die Identität vom Typnamen entkoppelt — nötig, wenn zwei Aggregate denselben einfachen Typnamen tragen
/// (verschiedene Namespaces), oder um den persistierten <c>aggregate_type</c>/<c>ClusterKind</c> bewusst
/// stabil zu halten. So entfällt die frühere implizite Konvention „pro Namespace genau ein Aggregat".
///
/// Zwei Aggregate, die auf DIESELBE Identität auflösen, meldet der <c>CommandAggregateMapGenerator</c> als
/// Build-Fehler (CQRS011) — der Namens-Kollisionsfall bricht den Build, nicht die Laufzeit (TG-3).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AggregatNameAttribute : Attribute
{
    public string Name { get; }
    public AggregatNameAttribute(string name) => Name = name;
}

/// <summary>
/// ★ P1c (TG-3): optionaler, EXPLIZITER Name eines Prozesses (<c>IProzessDefinition</c>). Ohne Attribut ist
/// der Name der Definitions-Klassenname (Default → KEINE Migration). Aus dem Namen leitet der Korrelations-
/// Router die Korrelation ab (<c>ProzessId.Für</c>), ein Rename ist also migrationswirksam — das Attribut hält
/// ihn stabil bzw. entkoppelt ihn vom Klassennamen. Zwei Prozesse mit gleichem aufgelöstem Namen meldet der
/// <c>ProzessRegelnGenerator</c> als Build-Fehler (CQRS012).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProzessNameAttribute : Attribute
{
    public string Name { get; }
    public ProzessNameAttribute(string name) => Name = name;
}
