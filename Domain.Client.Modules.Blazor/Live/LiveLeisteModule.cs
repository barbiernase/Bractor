using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Live;

namespace Domain.Client.Modules.Live;

/// <summary>
/// Header-Leiste „Live" (Rekonzeption des Live-Modus): zeigt, dass das System aufnimmt UND
/// mit welchem <b>aktiven Modell</b> es die einlaufenden Bildpaare klassifiziert — der Gegenpart
/// zur Sammel-Ziel-Leiste. Macht die Kette Datensatz → Training → Modell → <i>Live-Inferenz</i>
/// im Kopf der App sichtbar. Auto-entdeckt als <see cref="IHeaderModule"/>.
/// </summary>
public class LiveLeisteModule : IHeaderModule
{
    public string Id    => "live-leiste";
    public string Title => "Live";
    public Type   ComponentType => typeof(LiveLeiste);
    public int    Order => 2;   // unter StatusBar (0) + Sammel-Ziel (1)
}
