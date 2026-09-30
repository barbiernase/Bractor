using Abstractions;
using Core;
using Domain.Datensatz;
using DatensatzAgg = Domain.Datensatz.Datensatz;

namespace Domain.Projections;

/// <summary>
/// Materialisiert den Datensatz-Zustand (Konzept §3.5): ein <see cref="DatensatzReadModel"/>
/// pro Datensatz (Upsert) und beim Einfrieren je Mitglied eine <see cref="DatensatzSampleReadModel"/>
/// (die queryierbare Trainings-Wahrheit).
///
/// APPEND-ARTIG (<see cref="IAppendProjektion"/>): das Einfrieren schreibt neue Sample-Zeilen —
/// der GA-1-Check erzwingt daher den Co-Commit-Store (der <c>DatensatzStore</c> ist zugleich
/// <see cref="ICoCommitTracker"/> → Effekt + Marke in EINER Transaktion, exactly-once).
///
/// Kein EventStore-Zugriff — die Wahrheit kommt ausschließlich aus PubSub-Events.
/// </summary>
public partial class DatensatzProjektion : ISubscriber, IPullSubscriber, IAppendProjektion
{
    public string SubscriberId => "datensatz-projection";

    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        DatensatzErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer, IUpsertDatensatz upsertDatensatz)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<DatensatzAgg>(envelope.AggregateId);
            await upsertDatensatz.UpsertAsync(new DatensatzReadModel
            {
                Id = envelope.AggregateId,
                Name = evt.Name,
                Status = DatensatzStatus.Entwurf,
                AnzahlMitglieder = 0,
                LetzteAktualisierung = envelope.CreatedAtUtc
            });
        });
    }

    // ═══════════════════════════════════════════════════════════
    // KOMPOSITION — Ranges & manuelles Delta
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        PaareAufgenommen evt, IAggregateEnvelope envelope, ProjectionWriter writer, INimmRangeAuf nimmRangeAuf)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<DatensatzAgg>(envelope.AggregateId);
            await nimmRangeAuf.NimmRangeAufAsync(
                envelope.AggregateId, evt.ImagePairIds, evt.Herkunft, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        PaarAufgenommen evt, IAggregateEnvelope envelope, ProjectionWriter writer, INimmPaarAuf nimmPaarAuf)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<DatensatzAgg>(envelope.AggregateId);
            await nimmPaarAuf.NimmPaarAufAsync(envelope.AggregateId, evt.ImagePairId, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        PaarEntfernt evt, IAggregateEnvelope envelope, ProjectionWriter writer, IEntfernePaar entfernePaar)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<DatensatzAgg>(envelope.AggregateId);
            await entfernePaar.EntfernePaarAsync(envelope.AggregateId, evt.ImagePairId, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        SplitGesetzt evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetzeSplit setzeSplit)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<DatensatzAgg>(envelope.AggregateId);
            await setzeSplit.SetzeSplitAsync(
                envelope.AggregateId,
                new SplitKonfig(evt.TrainProzent, evt.ValProzent, evt.TestProzent, evt.Seed),
                envelope.CreatedAtUtc);
        });
    }

    // ═══════════════════════════════════════════════════════════
    // EINFRIEREN — Snapshot festschreiben (append-artig)
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        DatensatzEingefroren evt, IAggregateEnvelope envelope, ProjectionWriter writer, IFriereEin friereEin)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<DatensatzAgg>(envelope.AggregateId);
            await friereEin.FriereEinAsync(
                envelope.AggregateId, evt.Version, evt.Mitglieder, envelope.CreatedAtUtc);
        });
    }
}
