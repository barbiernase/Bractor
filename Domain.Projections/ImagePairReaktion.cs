using Abstractions;
using Core;
using Domain.ImagePair;
using Domain.Reaktion;

namespace Domain.Projections;

/// <summary>
/// Phase-3-Demo-REAKTION: reagiert auf jede ImagePair-Erstellung und wirkt eine
/// Reaktion auf einem festen Empfänger-Aggregat (yieldet <see cref="WirkeReaktion"/>).
///
/// Kein Repo-Write → reine Reaktion (Spec 9.1: als feldlose, pure Funktion schreibbar).
/// Der RÜCKGABETYP wählt den Effekt: ein Command → der Actor routet ihn an das
/// Ziel-Aggregat (Output-Routing, Spec 4.6). Die ReaktionId (= Id des auslösenden
/// Paars) ist die fachliche Korrelation, sodass ein wiederholt zugestellter Trigger
/// beim Empfänger als Duplikat erkannt wird und verpufft.
/// </summary>
public partial class ImagePairReaktion : ISubscriber, IPullSubscriber
{
    /// <summary>Fester Demo-Empfänger — ein Reaktionsempfaenger-Aggregat.</summary>
    public static readonly Guid EmpfaengerId =
        Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public string SubscriberId => "imagepair-reaktion";

    public async IAsyncEnumerable<OneOf<WirkeReaktion>> Handle(
        ImagePairErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        yield return new WirkeReaktion(
            AggregateId: EmpfaengerId,
            ReaktionId: envelope.AggregateId,   // Korrelation = auslösendes Paar
            Notiz: $"ImagePair {envelope.AggregateId} erstellt");

        await Task.CompletedTask;   // erfüllt die async-Iterator-Signatur
    }
}
