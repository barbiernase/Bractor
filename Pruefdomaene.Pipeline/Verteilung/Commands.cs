using Abstractions;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

// COMMANDS, die nur Knoten einander geben (docs/04 §4.4): Commands ohne Ziel, nie im Log.

/// <summary>Ein Ziel der Verteilung ist zuzubuchen — je Ziel ein Unterlauf (aus dem Strom der <see cref="Zielaufteilung"/>).</summary>
public record ZielZubuchen(Id<global::Pruefdomaene.Verteilung.Verteilung> VerteilungId, Id<global::Pruefdomaene.Posten.Posten> Ziel, int Menge) : ICommand;

/// <summary>Ein Ziel ist gebucht (die Antwort des Postens) — gesammelt vom <see cref="Verteilabschluss"/>.</summary>
public record ZielGebucht(Id<global::Pruefdomaene.Verteilung.Verteilung> VerteilungId, int Menge) : ICommand;
