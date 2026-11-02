using Abstractions;

namespace Pruefdomaene.Pipeline.Auftrag;

/// <summary>Der Ablauf eines Auftrags: reservieren und belegen, Rückmeldungen vermerken, Gegenzüge senden.</summary>
public sealed record Auftragsablauf(Auftragsabwicklung Abwicklung) : IGraph;
