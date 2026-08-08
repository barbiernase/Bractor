using System.Security.Cryptography;
using System.Text;
using Abstractions;

namespace Infrastructure.Aggregate;

/// <summary>
/// Die EINE deterministische Ableitung der CommandId eines Emits (EM-1) — ersetzt die früher
/// getrennten <c>ReaktionsId.For</c> und <c>ProzessId.FürTransition</c> durch eine Funktion.
///
/// Aus <see cref="EmitKausalität"/> + Ziel-Aggregat: gleiche Ursache → gleiche Id → der Empfänger
/// dedupliziert über die Framework-Inbox (Noop). Das ist die Grundlage für „at-least-once gesendet,
/// exactly-once wirksam durch den Empfänger" (Spec 9.3). Das Ziel-Aggregat ist Teil des Hashes →
/// Fan-out an verschiedene Ziele kollidiert nicht (Härtung gegen Befund 8).
///
/// MD5 dient nur als stabile, gut streuende Ableitung — keine kryptografische Eigenschaft nötig.
/// </summary>
public static class EmitId
{
    public static Guid Ableiten(EmitKausalität k, Guid ziel)
    {
        var input = $"{k.Korrelation:N}:{k.Ursache:N}:{k.Diskriminator}:{ziel:N}";
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes(input), hash);
        return new Guid(hash);
    }
}
