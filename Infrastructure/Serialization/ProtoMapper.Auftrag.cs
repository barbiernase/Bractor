using Abstractions;
using Infrastructure.Mappers;
using Cqrs.Leitung;

namespace Infrastructure.Serialization;

/// <summary>
/// Auftrags-/Ergebnis-Mapping für externe Funktions-Ausführer (docs/konzept-editor-pipelines.md §14.5).
/// Delegiert an die generierten Helfer — reflexionsfrei (Invariante 4).
/// </summary>
public partial class ProtoMessageMapper
{
    // =========================================================================
    // AUFTRAG MAPPING (Katalog-Funktionen)
    // =========================================================================

    public IAuftrag AuftragAus(Nutzlast n)
    {
        return ProtoAuftragMappingHelpers.MapToDomain(n);
    }

    public Nutzlast MapToDto(IAuftrag auftrag)
    {
        return ProtoAuftragMappingHelpers.MapToDto(auftrag);
    }

    /// <summary>
    /// Nur die Nutzlast eines Ergebnis-Envelopes — die Metadaten (Stream, Korrelation, Akteur) stempelt der Server selbst,
    /// ein externer Ausführer muss sie nicht (und darf sie nicht) setzen.
    /// </summary>
    public IEvent MapErgebnis(EventEnvelopeDto dto)
    {
        return ProtoEventMappingHelpers.MapPayload(dto);
    }
}
