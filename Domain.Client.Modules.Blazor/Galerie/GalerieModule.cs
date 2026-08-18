using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Galerie;

namespace Domain.Client.Modules.Galerie;

/// <summary>
/// Bühne „Galerie" (Konzept galerie-datensatz-komposition §3, Phase 1). Ein Thumbnail-Grid
/// über demselben <c>VirtualImagePairs</c>-Fenster wie die Paarliste — die Galerie ERGÄNZT die
/// dichte Text-Liste als Sicht-/Kuratier-Fläche (nicht ersetzen). Als IStageModule automatisch
/// ein Tab in der Shell; kein Handwiring.
///
/// Links das Grid, rechts das eingebettete Einbild-Detail (die bestehende BilderStage) — beide
/// über den geteilten Cursor gekoppelt: Klick auf eine Kachel setzt den Cursor, das Detail folgt.
/// </summary>
public class GalerieModule : IStageModule
{
    public string Id    => "galerie";
    public string Title => "Galerie";
    public Type   ComponentType => typeof(GalerieStage);
    public int    Order => 2;   // direkt nach „Bilder" (1), vor „Datensatz" (10)

    // Enter „geht ins Bild" wird im Grid selbst behandelt (VirtualGrid.OnKey → OnOpen), weil die
    // Shell-Keybindings den Fokus auf dem Shell-Container brauchen — ein Klick auf eine Zelle
    // verliert den aber. Das Grid hält den Fokus selbst und ist damit unabhängig.
}
