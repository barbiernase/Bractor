/*using Abstractions;
using Core;
using Domain.Projections;
using Infrastructure.Projections;

namespace Infrastructure.PubSub.Actors;

/// <summary>
/// â˜… Phase 2: Beide Actors nehmen ReadModelDepsWriter entgegen.
///   LagerbestandProjection nutzt ihn (hat Track-Aufrufe).
///   AuditLog nutzt ihn nicht (kein writer.Execute()), aber bekommt ihn fÃ¼r UniformitÃ¤t.
/// </summary>
public class LagerbestandProjectionActor : SubscriberActorBase<LagerbestandProjection>
{
    public LagerbestandProjectionActor(
        LagerbestandProjection logic,
        BrokerPublisher? publisher = null,
        ReadModelDepsWriter? depsWriter = null) 
        : base(logic, publisher, depsWriter) { }

    protected override IReadOnlyList<Type> GetSubscribedMessageTypes()
        => LagerbestandProjection.SubscribedMessageTypes;

    protected override IAsyncEnumerable<IEvent> DispatchAsync(
        IMessageEnvelope envelope, ProjectionWriter writer, CancellationToken ct)
        => _logic.DispatchAsync(envelope, writer, ct);
}

public class AuditLogActor : SubscriberActorBase<AuditLog>
{
    public AuditLogActor(
        AuditLog logic,
        BrokerPublisher? publisher = null,
        ReadModelDepsWriter? depsWriter = null) 
        : base(logic, publisher, depsWriter) { }

    protected override IReadOnlyList<Type> GetSubscribedMessageTypes()
        => AuditLog.SubscribedMessageTypes;

    protected override IAsyncEnumerable<IEvent> DispatchAsync(
        IMessageEnvelope envelope, ProjectionWriter writer, CancellationToken ct)
        => _logic.DispatchAsync(envelope, writer, ct);
}*/