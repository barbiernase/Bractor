/*using System.Runtime.CompilerServices;
using Abstractions;
using Core;
using Domain.Lagerartikel;
using Domain.Profil;

namespace Domain.Projections;

// Dieser Code würde später vom Generator erzeugt
public partial class AuditLog
{
    /// <summary>
    /// Message-Typen für Subscriptions (aus HandleAsync-Methoden extrahiert)
    /// </summary>
    public static IReadOnlyList<Type> SubscribedMessageTypes { get; } = new[]
    {
        typeof(LagerartikelErstellt),
        typeof(WareneingangGebucht),
        typeof(WarenabgangGebucht),
        typeof(LagerartikelDeaktiviert),
        typeof(NachbestellungAngefordert),
        typeof(ProfilErstellt),
        typeof(NameGeändert)
    };

    /// <summary>
    /// Dispatch an den richtigen Handler basierend auf Payload-Typ.
    /// Einheitliche Signatur: ProjectionWriter wird immer durchgereicht.
    /// </summary>
    public async IAsyncEnumerable<IEvent> DispatchAsync(
        IMessageEnvelope envelope,
        ProjectionWriter writer,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var events = envelope.Payload switch
        {
            LagerartikelErstellt e => HandleAsync(e, envelope, writer),
            WareneingangGebucht e => HandleAsync(e, envelope, writer),
            WarenabgangGebucht e => HandleAsync(e, envelope, writer),
            LagerartikelDeaktiviert e => HandleAsync(e, envelope, writer),
            NachbestellungAngefordert e => HandleAsync(e, envelope, writer),
            ProfilErstellt e => HandleAsync(e, envelope, writer),
            NameGeändert e => HandleAsync(e, envelope, writer),
            _ => EmptyAsync()
        };

        await foreach (var evt in events.WithCancellation(ct))
        {
            yield return evt;
        }
    }

    private static async IAsyncEnumerable<IEvent> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }
}
*/