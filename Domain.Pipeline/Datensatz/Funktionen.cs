using Abstractions;
using Domain.Projections.ImagePair;    // ISearchImagePairs/IFindImagePair (Lese-Fähigkeiten der Funktionen)

namespace Domain.Pipeline.Datensatz;

/// <summary>Range-Kriterien durchpaginiert zu Paar-Ids auflösen (Lese-Fähigkeit <see cref="ISearchImagePairs"/>).</summary>
public interface IRangeSuche : IFunktion
{
    Task<OneOf<RangeGefunden, RangeOhneTreffer>> RufeAsync(SucheRange auftrag, IAusfuehrung x, ISearchImagePairs suche);
}

/// <summary>Mitglieder-Snapshot mit Label-Stand und Split (Lese-Fähigkeit <see cref="IFindImagePair"/>).</summary>
public interface IMitgliederEinfrieren : IFunktion
{
    Task<OneOf<MitgliederEingefroren, KeinMitgliedAuffindbar>> RufeAsync(FriereMitgliederEin auftrag, IAusfuehrung x, IFindImagePair finde);
}
