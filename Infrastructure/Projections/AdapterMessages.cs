using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Weckruf an den per-Stream-Adapter (Phase 4a). Die Ziel-Cluster-Identität IST der
/// Stream — deshalb trägt Wake nur die Weck-Quelle. Darf verloren/doppelt sein;
/// die Wahrheit liegt im Log-Read des Adapters (Invariante 2).
///
/// <paramref name="VomPoll"/> unterscheidet Signal- (schneller Pfad) von Poll-Weckung
/// (Backstop). Für EMITTIERENDE Konsumenten (Achse B, P4) nutzt der Signal-Pfad den
/// best-effort <c>IEmittentenCursor</c> (kein Re-Emit ab 0), während der Poll bewusst
/// AB 0 heilt — so bleibt die at-least-once-Garantie exakt erhalten (ein verlorener
/// detached-Emit wird spätestens vom nächsten Poll re-emittiert, Empfänger dedupliziert).
/// Für REPLAYBARE Konsumenten (Projektion) ist der Tracker maßgeblich → das Flag ist folgenlos.
/// </summary>
public sealed record Wake(bool VomPoll = false) : IWireMessage;

/// <summary>Quittung auf einen Wake (der Sender feuert i. d. R. fire-and-forget).</summary>
public sealed record WakeAck : IWireMessage;

/// <summary>
/// Beitrag eines zusätzlichen Cluster-Kinds zur ClusterConfig (Phase 4a). Wird von
/// AddCqrsActorSystem VOR StartMemberAsync eingesammelt — so kann eine auf den Pull-Pfad
/// migrierte Projektion ihren per-Stream-Adapter-Kind registrieren, ohne dass das
/// Framework die Projektion kennt.
/// </summary>
public interface IClusterKindContributor
{
    Proto.Cluster.ClusterKind CreateKind(Proto.ActorSystem system, IServiceProvider provider);
}
