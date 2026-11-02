using Abstractions;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>Der Anfang einer Verteilung: die Quelle reservieren.</summary>
public sealed record Verteilungsablauf([Kopf] Verteilabwicklung Abwicklung) : IGraph;

/// <summary>Die Zubuchung einer Verteilung (Kopf: <c>Zubuchen</c>): JE Ziel ein Unterlauf, der es bucht, dann SAMMELN und abschließen.</summary>
public sealed record Zubuchung([Kopf] Zielaufteilung Aufteilung, [Von(nameof(Aufteilung))] Zielbuchung Buchung,
    [Von(nameof(Buchung))] Verteilabschluss Abschluss) : IGraph;
