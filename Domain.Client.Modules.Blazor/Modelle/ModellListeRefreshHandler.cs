using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.Modell;
using Domain.Projections;

namespace Domain.Client.Modules.Modelle;

/// <summary>Lädt die Modell-Liste nach dem Verbindungsaufbau und nach jedem Modell-Event.</summary>
public partial class ModellListeRefreshHandler
{
    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx) => Lade();
    IEnumerable<object> Handle(ModellRegistriert evt, MessageContext ctx)     => Lade();
    IEnumerable<object> Handle(ModellAktiviert evt, MessageContext ctx)       => Lade();
    IEnumerable<object> Handle(ModellArchiviert evt, MessageContext ctx)      => Lade();

    private static IEnumerable<object> Lade()
    {
        yield return new HoleModelle();
    }
}
