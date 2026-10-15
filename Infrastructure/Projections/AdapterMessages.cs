using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Weckruf an den per-Stream-Adapter (Phase 4a). Die Ziel-Cluster-Identität IST der Stream — deshalb trägt Wake nichts. Darf
/// verloren/doppelt sein; die Wahrheit liegt im Log-Read des Adapters (Invariante 2). Signal und Poll wecken gleich: der Adapter liest ab
/// seiner Marke (Projektion) bzw. seinem Cursor (Emittent, G-2).
/// </summary>
public sealed record Wake : IWireMessage;

/// <summary>
/// Quittung auf einen Wake. <paramref name="Aufgeholt"/> = false: der Stream ist nicht bestätigt aufgeholt (ein ausgegebener Command
/// blieb unbekannt) — der Poll darf seine Hochwassermarke nicht über diesen Stream vorrücken.
/// </summary>
public sealed record WakeAck(bool Aufgeholt = true) : IWireMessage;

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
