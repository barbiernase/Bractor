using Abstractions;

namespace Pruefdomaene.Kontingent;

/// <summary>Der exakte Stand eines Kontingents, direkt vom Aggregat (Plan Pub/Sub-Neustrukturierung §2.6) — mit seiner Version als OCC-Stand.</summary>
public record HoleKontingent(Id<Kontingent> Kontingent) : IQuery;
