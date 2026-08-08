using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Aggregate;

/// <summary>
/// Deterministische Reaktions-/CommandId aus `(StreamId, AggregateVersion)` des
/// auslösenden Events plus einem Diskriminator (i. d. R. der Command-Typname) —
/// Spec 9.3 / 11.3.
///
/// Gleiche Auslöse-Position + gleicher Command-Typ → gleiche Id. Damit ist ein
/// wiederholt gesendeter Reaktions-Command beim Empfänger ALS DUPLIKAT erkennbar
/// (Noop-Decider), nicht nur zufällig unschädlich. Das ist die Grundlage für
/// „at-least-once gesendet, exactly-once wirksam durch den Empfänger".
///
/// MD5 dient hier nur als stabile, gut streuende Ableitung — keine kryptografische
/// Eigenschaft nötig.
/// </summary>
public static class ReaktionsId
{
    public static Guid For(Guid streamId, int aggregateVersion, string discriminator)
    {
        var input = $"{streamId:N}:{aggregateVersion}:{discriminator}";
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes(input), hash);
        return new Guid(hash);
    }
}
