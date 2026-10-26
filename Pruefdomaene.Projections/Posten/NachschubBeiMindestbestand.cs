using Abstractions;
using Core;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Projections.Posten;

/// <summary>
/// Reaktion: drückt eine Reservierung den verfügbaren Bestand eines Postens unter seinen Mindestbestand, wird die Fehlmenge nachgebucht.
/// Sie hört auf die langlebigen Posten-Streams (jede Reservierung eines Auftrags oder einer Verteilung landet dort) und ist damit der
/// Emittent, an dem sich zeigt, ob seine Kosten mit der Stream-Länge wachsen (docs/13 G-2, docs/plan-garantien-und-durchsatz.md S5).
/// </summary>
public partial class NachschubBeiMindestbestand : ISubscriber, IPullSubscriber
{
    public string SubscriberId => "nachschub-bei-mindestbestand";

    public async IAsyncEnumerable<BuchePostenZu> Handle(PostenReserviert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await Task.CompletedTask;
        if (evt.Fehlmenge > 0) yield return new BuchePostenZu(new Id<Pruefdomaene.Posten.Posten>(envelope.AggregateId), evt.Fehlmenge);
    }
}
