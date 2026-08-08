using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
namespace Domain.Client.Modules.Labeling;

public partial class LabelingIntentHandler
{
    private readonly Store _store;
    public LabelingIntentHandler(Store store) => _store = store;

    IEnumerable<object> Handle(LabelProduktAngefordert evt, MessageContext ctx)
    { if (_store.Cursor.Id is { } id) yield return new LabelPhysischesProdukt(id, evt.Label); }

    IEnumerable<object> Handle(LabelKameraAngefordert evt, MessageContext ctx)
    { if (_store.Cursor.Id is { } id) yield return new LabelBildPaar(id, evt.Label); }
}
