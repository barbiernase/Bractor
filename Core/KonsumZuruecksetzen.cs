using Abstractions;
using Marten;

namespace Core;

/// <summary>
/// Der Neuaufbau-Schritt des Batch-Konsums an EINER Stelle (Tracker und <c>ProjektionsStandDienst</c>): alle Partitionen einer Projektion auf
/// Anfang und alle Guards weg. Die Partitionen werden nicht gelöscht, sondern mit neuer Revision überschrieben (Position 0, nichts geparkt) —
/// so verliert ein noch laufender Leser seinen nächsten Commit an der Versionsprüfung, statt seinen alten Stand wieder anzulegen.
/// </summary>
public static class KonsumZuruecksetzen
{
    public static async Task PartitionenUndGuardsAsync(IDocumentSession s, string projektionsId, CancellationToken ct)
    {
        var partitionen = await s.Query<PartitionsCheckpoint>().Where(p => p.ProjektionsId == projektionsId).ToListAsync(ct);
        var jetzt = DateTimeOffset.UtcNow;
        foreach (var p in partitionen)
        {
            p.Position = 0;
            p.Geparkt = new();
            p.UpdatedAt = jetzt;
            s.Store(p);   // Store erhöht die Revision bedingungslos
        }
        s.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projektionsId);
    }
}
