using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
namespace Domain.Client.Modules;

/// <summary>Mensch-Label Server-Events → VirtualImagePairs patchen.</summary>
public partial class Store
{
    void Handle(PhysischesProduktGelabelt evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => e with { PhysischesProduktLabel = evt.Label });

    void Handle(BildPaarGelabelt evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => e with { MenschBildpaarLabel = evt.Label });

    void Handle(EinzelBildGelabelt evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e with { Dc0MenschBildLabel = evt.Label },
            BildVersion.Dc2 => e with { Dc2MenschBildLabel = evt.Label },
            _ => e
        });

    void Handle(ImagePairInspiziert evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => e with { IstInspiziert = true });
}
