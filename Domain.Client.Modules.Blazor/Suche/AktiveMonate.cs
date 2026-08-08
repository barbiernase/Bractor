using Client.Infrastructure.Abstractions;
namespace Domain.Client.Modules.Suche;

/// <summary>
/// Rein clientseitig: welche Monate im Baum "relevant" sind, also ihre Tage
/// anzeigen. Schlüssel = Jahr*100 + Monat (z.B. 202511). Multi-Select über die
/// Häkchen. KEINE Query, KEIN Reload — nur der SuchStore reagiert, das Panel
/// re-derived/re-rendert. Geladen wird erst beim Klick auf einen konkreten Tag.
/// </summary>
public record AktiveMonateGeaendert(IReadOnlySet<int> Schluessel) : IClientEvent;
