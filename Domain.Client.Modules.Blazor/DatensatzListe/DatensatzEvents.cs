using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.Datensaetze;

/// <summary>
/// Client-lokal (IClientEvent): der Nutzer hat in der Datensatz-Liste einen Datensatz
/// gewählt. Der Komposition-Store (M9-Stage) übernimmt ihn als aktiven Datensatz und
/// lädt seinen Kopf nach. Geht nie an den Server.
/// </summary>
public record DatensatzAusgewaehlt(Guid DatensatzId) : IClientEvent;
