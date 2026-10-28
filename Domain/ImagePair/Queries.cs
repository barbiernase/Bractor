using Abstractions;

namespace Domain.ImagePair;

/// <summary>
/// Der exakte Stand eines Bildpaars, direkt vom Aggregat (Plan Pub/Sub-Neustrukturierung §2.6): die Antwort trägt seine Version — der Stand, auf dem
/// der nächste Command entschieden würde (etwa ein Label mit Versionsprüfung).
/// </summary>
public record HoleBildPaarStand(Id<ImagePair> Paar) : IQuery;
