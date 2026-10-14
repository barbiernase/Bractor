using Abstractions;
using Cqrs.Leitung;

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

    public IPipelineTrigger TriggerAus(Nutzlast n)
    {
        return _tabelle.Trigger(n);
    }

    public Nutzlast MapToDto(IPipelineTrigger trigger)
    {
        return _tabelle.PackeTrigger(trigger);
    }
}
