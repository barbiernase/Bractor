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
