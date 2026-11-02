using Abstractions;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>Die Abrechnung: eine Rechnung freigeben, eine Gutschrift buchen, eine Mahnung versenden — drei Köpfe, je ein Command aus einem Decide.</summary>
public sealed record Abrechnungsablauf(IFreigabe Freigabe, Gutschrift Gutschrift, Mahnwesen Mahnwesen) : IGraph;
