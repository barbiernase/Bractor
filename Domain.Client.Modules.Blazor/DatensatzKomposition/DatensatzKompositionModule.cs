using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.DatensatzKomposition;

namespace Domain.Client.Modules.DatensatzKomposition;

/// <summary>
/// Zentrale Bühne „Datensatz-Komposition" (Transfer-/Korb-Layout, Konzept §8.1).
/// Als IStageModule automatisch ein Tab in der Shell. Links die Kandidaten aus dem
/// vorhandenen Such-Filter, rechts der Datensatz-Korb mit Größe/Split/Provenienz/Einfrieren.
/// </summary>
public class DatensatzKompositionModule : IStageModule
{
    public string Id    => "datensatz-komposition";
    public string Title => "Datensatz";
    public Type   ComponentType => typeof(DatensatzKompositionPanel);
    public int    Order => 10;
}
