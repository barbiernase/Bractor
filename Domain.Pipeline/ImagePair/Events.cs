using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.ImagePair;

/// <summary>Der Name passt zur Konvention: Paar-Id (deterministisch aus dem PairKey), Version und Aufnahmezeit.</summary>
public record ImagePairDateiGedeutet(Id<Domain.ImagePair.ImagePair> PaarId, string PairKey, BildVersion Version, DateTimeOffset ProduziertAm, string Pfad) : IEvent;

/// <summary>Der Name passt nicht zur Konvention — die Datei gehört zu keinem Paar.</summary>
public record DateinameUnbekannt(string Dateiname) : IEvent;
