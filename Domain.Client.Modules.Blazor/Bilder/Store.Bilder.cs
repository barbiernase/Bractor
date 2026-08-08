using System.Linq;
using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
namespace Domain.Client.Modules;

/// <summary>Bild-bezogene Handle-Methoden: FileLoaded, BildVerfuegbar.</summary>
public partial class Store
{
    void Handle(FileLoaded evt, MessageContext ctx)
    {
        // Der passende Datensatz wird über den Pfad gefunden (nicht über den Key),
        // daher ein Scan — aber nur über die GELADENEN Items, nicht über GesamtAnzahl.
        // ToList(), weil Patch die Chunk-Arrays während der Iteration mutiert.
        foreach (var e in VirtualImagePairs.AlleGeladenen().ToList())
        {
            var neu = e;
            if (e.Dc0Pfad == evt.SourcePath && e.Dc0BildSrc == null)
                neu = neu with { Dc0BildSrc = evt.AccessUrl };
            if (e.Dc2Pfad == evt.SourcePath && e.Dc2BildSrc == null)
                neu = neu with { Dc2BildSrc = evt.AccessUrl };
            if (!ReferenceEquals(neu, e))
                VirtualImagePairs.Patch(e.Id, _ => neu);
        }
    }

    void Handle(BildVerfuegbar evt, MessageContext ctx)
    {
        VirtualImagePairs.Patch(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e with { Dc0Pfad = evt.Pfad },
            BildVersion.Dc2 => e with { Dc2Pfad = evt.Pfad },
            _ => e
        });
    }
}
