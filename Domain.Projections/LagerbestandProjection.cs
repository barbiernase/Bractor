using Abstractions;
using Core;
using Domain.Lagerartikel;

namespace Domain.Projections;

/// <summary>
/// Projektion für Lagerbestände — Write-Seite.
/// 
/// Nutzt ILagerbestandWriteStore für Persistenz.
/// Reader ist als eigenständige Top-Level-Klasse (LagerbestandReader.cs)
/// mit ILagerbestandReadStore — vollständige CQRS-Trennung.
///
/// Handler-Signatur: Handle(TEvent, IAggregateEnvelope, ProjectionWriter)
/// Events haben immer Aggregate-Kontext → IAggregateEnvelope statt IMessageEnvelope.
/// </summary>
public partial class LagerbestandProjection : ISubscriber
{
    private readonly ILagerbestandWriteStore _store;

    public LagerbestandProjection(ILagerbestandWriteStore store)
    {
        _store = store;
    }

    public string SubscriberId => "lagerbestand-projection";

    public const int NachbestellungSchwelle = 10;
    public const int NachbestellungMenge = 100;

    // =========================================================================
    // WRITE-HANDLER — IAggregateEnvelope + WriteStore
    // =========================================================================

    public async Task Handle(
        LagerartikelErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<Lagerartikel.Lagerartikel>(envelope.AggregateId);
            await _store.UpsertAsync(new LagerbestandReadModel
            {
                Id = envelope.AggregateId,
                Name = evt.Name,
                Anzahl = evt.InitialeAnzahl,
                LetzteAktualisierung = envelope.CreatedAtUtc
            });
        });
    }

    public async Task Handle(
        WareneingangGebucht evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<Lagerartikel.Lagerartikel>(envelope.AggregateId);
            await _store.AddToBestandAsync(envelope.AggregateId, evt.Anzahl, envelope.CreatedAtUtc);
        });
    }

    public async IAsyncEnumerable<NachbestellungAngefordert> Handle(
        WarenabgangGebucht evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<Lagerartikel.Lagerartikel>(envelope.AggregateId);
            await _store.SubtractFromBestandAsync(envelope.AggregateId, evt.Anzahl, envelope.CreatedAtUtc);
        });

        var current = await _store.GetBestandAsync(envelope.AggregateId);
        if (current.HasValue && current.Value < NachbestellungSchwelle)
        {
            yield return new NachbestellungAngefordert(envelope.AggregateId, NachbestellungMenge);
        }
    }
}