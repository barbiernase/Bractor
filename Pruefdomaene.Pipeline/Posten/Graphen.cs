using Abstractions;

namespace Pruefdomaene.Pipeline.Posten;

/// <summary>Ein unterschrittener Posten wird nachbestellt.</summary>
public sealed record Nachschubablauf(Nachschub Nachschub) : IGraph;
