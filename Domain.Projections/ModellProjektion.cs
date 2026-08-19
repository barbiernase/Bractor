using Abstractions;
using Core;
using Domain.Modell;
using ModellAgg = Domain.Modell.Modell;

namespace Domain.Projections;

/// <summary>
/// Materialisiert den Modell-Zustand (Konzept konzept-training-und-datensatz §5): ein
/// <see cref="ModellReadModel"/> pro Modell (Upsert) + den Singleton-Zeiger
/// <see cref="AktivesModellReadModel"/> auf das aktive Modell.
///
/// Upsert-artig (kein Append) → <see cref="ISubscriber"/>, <see cref="IPullSubscriber"/>; der
/// <c>ModellStore</c> ist zugleich Co-Commit-Tracker (Effekt + Marke in EINER Transaktion).
/// Kein EventStore-Zugriff — die Wahrheit kommt aus PubSub-Events.
/// </summary>
public partial class ModellProjektion : ISubscriber, IPullSubscriber
{
    private readonly IModellWriteStore _store;

    public ModellProjektion(IModellWriteStore store)
    {
        _store = store;
    }

    public string SubscriberId => "modell-projection";

    public async Task Handle(
        ModellRegistriert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ModellAgg>(envelope.AggregateId);
            await _store.UpsertAsync(new ModellReadModel
            {
                Id = envelope.AggregateId,
                Name = evt.Name,
                Pfad = evt.Pfad,
                TrainingslaufId = evt.TrainingslaufId,
                DatensatzId = evt.DatensatzId,
                DatensatzVersion = evt.DatensatzVersion,
                Loss = evt.Metriken.Loss,
                Genauigkeit = evt.Metriken.Genauigkeit,
                Status = ModellStatus.Registriert,
                RegistriertAm = envelope.CreatedAtUtc
            });
        });
    }

    public async Task Handle(
        ModellAktiviert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ModellAgg>(envelope.AggregateId);
            await _store.SetzeAktivAsync(envelope.AggregateId, evt.Name, evt.Pfad, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        ModellArchiviert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ModellAgg>(envelope.AggregateId);
            await _store.ArchiviereAsync(envelope.AggregateId, envelope.CreatedAtUtc);
        });
    }
}
