using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.DatensatzKomposition;

namespace Domain.Client.Modules.DatensatzKomposition;

/// <summary>
/// Bühne „Verwalten" (Konzept datensatz-kuratierung §9): die frühere Korb-/Kompositions-Bühne,
/// auf eine <em>Verwalten</em>-Sicht zurückgestuft — Mitglieder sehen + einzeln aussortieren,
/// Provenienz/Ranges, Split, Klassenbalance, Einfrieren. Kuratiert (getaggt) wird jetzt in
/// Galerie/Einbild (Sammel-Ziel + Taste A / Badge-Klick); der filter-getriebene
/// „ganze Range → Datensatz"-Weg bleibt als optionaler Bulk-Saat erhalten.
/// Als IStageModule automatisch ein Tab in der Shell.
/// </summary>
public class DatensatzKompositionModule : IStageModule
{
    public string Id    => "datensatz-komposition";
    public string Title => "Verwalten";
    public Type   ComponentType => typeof(DatensatzKompositionPanel);
    public int    Order => 10;
}
