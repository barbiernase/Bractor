using Abstractions;
using Marten;

namespace Core;

/// <summary>
/// Die Marten-Registrierung der Konsum-Dokumente — eine Stelle für Host und Tests: der Guard je (Projektion, Stream)
/// (<see cref="ProjectionCheckpoint"/>) und der Stand je (Projektion, Partition) (<see cref="PartitionsCheckpoint"/>) mit numerischer
/// Revision auf <see cref="PartitionsCheckpoint.Version"/>.
/// </summary>
public static class KonsumSchema
{
    public static void Registriere(StoreOptions options)
    {
        options.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);
        options.Schema.For<PartitionsCheckpoint>().Identity(x => x.Id)
            .UseNumericRevisions(true)
            .Metadata(m => m.Revision.MapTo(x => x.Version));
    }
}
