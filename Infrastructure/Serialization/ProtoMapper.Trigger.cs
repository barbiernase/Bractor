using Abstractions;
using Infrastructure.Mappers;
using ProtoRepo;

namespace Infrastructure.Serialization;

/// <summary>
/// Trigger-Mapping-Erweiterungen für ProtoMessageMapper.
/// Delegiert an die generierten ProtoTriggerMappingHelpers.
/// </summary>
public partial class ProtoMessageMapper
{
    // =========================================================================
    // TRIGGER MAPPING
    // =========================================================================

    public IPipelineTrigger MapToDomain(TriggerPayloadDto dto)
    {
        return ProtoTriggerMappingHelpers.MapToDomain(dto);
    }

    public TriggerPayloadDto MapToDto(IPipelineTrigger trigger)
    {
        return ProtoTriggerMappingHelpers.MapToDto(trigger);
    }
}
