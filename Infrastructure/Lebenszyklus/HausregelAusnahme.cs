namespace Infrastructure.Lebenszyklus;

/// <summary>
/// Nimmt einen Member oder Typ von der Hausregel für Actors aus (R18, Plan Pub/Sub-Neustrukturierung §1.7, CQRS112) — nur mit Begründung
/// (leer = CQRS113). Gedacht für die wenigen Stellen, an denen der Rahmen selbst das Verbotene sein muss, etwa der Bote als einziger
/// Berührungspunkt mit dem Cluster.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property,
    Inherited = false)]
public sealed class HausregelAusnahmeAttribute(string begruendung) : Attribute
{
    public string Begruendung { get; } = begruendung;
}
