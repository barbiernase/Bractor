using Client.Infrastructure.Abstractions;
namespace Domain.Client.Modules;

public partial class Store
{
    void Handle(NavigationZielGesetzt evt, MessageContext ctx)
        => Cursor.SetPending(evt.Index);

    void Handle(ImagePairAusgewaehlt evt, MessageContext ctx)
    {
        // Stabilen Index aus dem Key-Index holen — kein Scan mehr über die Liste.
        if (VirtualImagePairs.StabilerIndexVon(evt.PairId) is { } idx)
            Cursor.Select(idx, evt.PairId);
    }
}
