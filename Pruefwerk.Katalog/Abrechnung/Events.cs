using Abstractions;

namespace Pruefwerk.Katalog.Abrechnung;

/// <summary>Ein Hinweis: es gibt nichts anzumahnen (der Betrag ist beglichen) — flüchtig, an die Akteure, die ihn hören.</summary>
public record NichtZuMahnen(Guid Vorgang) : ITransientEvent;
