using Client.Infrastructure.Abstractions;
namespace Domain.Client.Modules.Suche;

/// <summary>
/// Rein clientseitig: die im Baum ausgewählten Tage (auf Tagesgrenze normiert,
/// 00:00). Die Häkchen an Jahr/Monat/Tag werden zu dieser Tagesmenge aufgelöst
/// (Jahr/Monat = alle Tage darunter). Setzt nur den SuchStore — das eigentliche
/// Laden macht separat SucheGeaendert mit der Bounding-Range der Auswahl.
/// </summary>
public record AuswahlGeaendert(IReadOnlySet<DateTimeOffset> Tage) : IClientEvent;
