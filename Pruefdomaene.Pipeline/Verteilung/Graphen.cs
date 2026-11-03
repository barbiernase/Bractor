using Abstractions;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>Der Anfang einer Verteilung: die Quelle reservieren.</summary>
public sealed record Verteilungsablauf([Kopf] Verteilabwicklung Abwicklung) : IGraph;

/// <summary>Die Zubuchung einer Verteilung (Kopf: <c>Zubuchen</c>): JE Ziel ein Unterlauf, der es bucht und der Verteilung meldet (sie zählt).</summary>
public sealed record Zubuchung([Kopf] Zielaufteilung Aufteilung, [Von(nameof(Aufteilung))] Zielbuchung Buchung) : IGraph;
