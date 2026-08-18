using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.Kuratieren;

// Client-lokale Intents (IClientEvent) der „Datensatz-als-Tag"-Kuratierung.
// Der KuratierenIntentHandler übersetzt sie in Datensatz-Commands.

/// <summary>
/// Mitgliedschaft eines Bildpaars im aktiven Sammel-Ziel umschalten (Konzept §5).
/// <paramref name="PairId"/> == null → das aktuelle Cursor-Paar (Einbild-Geste, Taste A);
/// gesetzt → ein konkretes Paar (Galerie-Badge-Klick).
/// </summary>
public record DatensatzTagGetoggelt(Guid? PairId = null) : IClientEvent;
