using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Domain.Projections;
namespace Domain.Client.Modules.Navigation;

/// <summary>
/// Cursor-Navigation im virtuellen Modell: rein index-basiert, kein Seitenüberlauf
/// mehr (es gibt logisch nur eine fortlaufende Liste). Ist das Nachbar-Item noch
/// nicht geladen (Pufferrand), passiert vorerst nichts — der Treiber lädt beim
/// Scrollen ohnehin nach. Die Skeleton-übergreifende Bewegung verfeinert Phase 7.
/// </summary>
public partial class NavigationIntentHandler
{
    private readonly Store _store;
    public NavigationIntentHandler(Store store) => _store = store;

    IEnumerable<object> Handle(NavigateNextAngefordert evt, MessageContext ctx)
    {
        var next = ItemBei(_store.Cursor.Index + 1);
        if (next is not null) yield return new ImagePairAusgewaehlt(next.Id);
    }

    IEnumerable<object> Handle(NavigatePrevAngefordert evt, MessageContext ctx)
    {
        // Untergrenze ist der oberste Live-Insert (stabiler Index -InsertOffset),
        // nicht 0 — sonst sind die Neuzugänge über Pfeil-hoch nicht erreichbar.
        if (_store.Cursor.Index <= -_store.VirtualImagePairs.InsertOffset) yield break;
        var prev = ItemBei(_store.Cursor.Index - 1);
        if (prev is not null) yield return new ImagePairAusgewaehlt(prev.Id);
    }

    IEnumerable<object> Handle(NavigateNextOffenAngefordert evt, MessageContext ctx)
    {
        var gesamt = _store.VirtualImagePairs.GesamtAnzahl;
        for (var i = _store.Cursor.Index + 1; i < gesamt; i++)
        {
            var it = ItemBei(i);
            if (it is null) yield break;   // Skeleton-Grenze erreicht — Phase 7 verfeinert
            if (it.MenschBildpaarLabel is null)
            { yield return new ImagePairAusgewaehlt(it.Id); yield break; }
        }
    }

    IEnumerable<object> Handle(BeendeTagLabelingAngefordert evt, MessageContext ctx)
    {
        if (_store.Modus == ArbeitsModus.Tag)
            yield return new TagLabelingBeendet();
    }

    IEnumerable<object> Handle(ModusToggleAngefordert evt, MessageContext ctx)
    {
        yield return new ModusGeaendert(
            _store.Modus == ArbeitsModus.Live ? ArbeitsModus.Inspekt : ArbeitsModus.Live);
    }

    // Cursor.Index ist ein STABILER Index → Anzeige = stabil + InsertOffset.
    private ImagePairRecord? ItemBei(int stabilerIndex)
        => _store.VirtualImagePairs[stabilerIndex + _store.VirtualImagePairs.InsertOffset];
}
