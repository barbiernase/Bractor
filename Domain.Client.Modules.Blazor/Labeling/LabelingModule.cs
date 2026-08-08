using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Labeling;
using Domain.ImagePair;
namespace Domain.Client.Modules.Labeling;
public class LabelingModule : IFooterModule
{
    public string Id    => "labeling";
    public string Title => "Labeling";
    public Type   ComponentType => typeof(LabelingFooter);
    public int    Order => 2;
    public IReadOnlyList<KeyBinding> KeyBindings =>
    [
        new("r", "OK",        () => new LabelProduktAngefordert(Klassifikation.KeineAnomalie)),
        new("q", "Fraglich",  () => new LabelProduktAngefordert(Klassifikation.Questionable)),
        new("f", "Anomalie",  () => new LabelProduktAngefordert(Klassifikation.Anomalie)),
        new("e", "Kamera ja", () => new LabelKameraAngefordert(Klassifikation.Anomalie)),
        new("n", "Kamera nein", () => new LabelKameraAngefordert(Klassifikation.KeineAnomalie)),
    ];
}
