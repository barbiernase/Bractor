using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Modelle;

namespace Domain.Client.Modules.Modelle;

/// <summary>
/// Bühne „Modelle" (Konzept konzept-training-und-datensatz §5): registrierte Modell-Artefakte,
/// Metrik-Vergleich, „aktiv setzen" → schließt den Kreis Datensatz → Training → Modell → Inferenz.
/// Auto-entdeckt als <see cref="IStageModule"/>.
/// </summary>
public class ModelleModule : IStageModule
{
    public string Id    => "modelle";
    public string Title => "Modelle";
    public Type   ComponentType => typeof(ModellePanel);
    public int    Order => 11;   // nach „Verwalten" (10)
}
