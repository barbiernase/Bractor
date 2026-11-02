using Abstractions;

namespace Domain.Pipeline.Sammelvorgang;

/// <summary>Ein fertiger Teil wird seinem Sammelvorgang gemeldet.</summary>
public sealed record Teilmeldung(TeilFertigMeldung Meldung) : IGraph;
