using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.Datensatz;
using Domain.Projections;

namespace Domain.Client.Modules.Datensaetze;

/// <summary>
/// Feuert die Listen-Query nach dem Verbindungsaufbau und nach jedem Datensatz-Event,
/// das die Liste sichtbar verändert (neu, Mitglieder-Delta, eingefroren). Der Generator
/// subscribed die Handle-Methoden automatisch; <c>yield return</c> einer Query stellt sie
/// über die QueryBridge.
/// </summary>
public partial class DatensatzListeRefreshHandler
{
    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx) => Lade();
    IEnumerable<object> Handle(DatensatzErstellt evt, MessageContext ctx)     => Lade();
    IEnumerable<object> Handle(PaareAufgenommen evt, MessageContext ctx)      => Lade();
    IEnumerable<object> Handle(PaarAufgenommen evt, MessageContext ctx)       => Lade();
    IEnumerable<object> Handle(PaarEntfernt evt, MessageContext ctx)          => Lade();
    IEnumerable<object> Handle(DatensatzEingefroren evt, MessageContext ctx)  => Lade();

    private static IEnumerable<object> Lade()
    {
        yield return new HoleDatensaetze();
    }
}
