namespace Infrastructure.Projections;

/// <summary>
/// Weckruf an den per-Stream-Adapter (Phase 4a). Die Ziel-Cluster-Identität IST der
/// Stream — deshalb trägt Wake selbst keine Nutzlast. Darf verloren/doppelt sein;
/// die Wahrheit liegt im Log-Read des Adapters (Invariante 2).
/// </summary>
public sealed record Wake;

/// <summary>Quittung auf einen Wake (der Sender feuert i. d. R. fire-and-forget).</summary>
public sealed record WakeAck;

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
