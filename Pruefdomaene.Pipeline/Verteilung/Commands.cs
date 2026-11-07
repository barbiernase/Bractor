using Abstractions;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

// COMMANDS ohne Ziel (docs/04 §4.4): nie im Log. An den Köpfen gibt sie ein Client hinein, der auf das Fakt reagiert (Schritt 6: ein Decide gibt
// nur Fakten aus); zwischen den Knoten geben sie Knoten einander.

/// <summary>Die Verteilung ist gestartet (<see cref="VerteilungGestartet"/>): die Abwicklung reserviert die Quelle; was die Quelle entscheidet, hört sie als Antwort.</summary>
public record VerteilungAbzuwickeln(Id<global::Pruefdomaene.Verteilung.Verteilung> VerteilungId, Id<global::Pruefdomaene.Posten.Posten> QuelleId,
    IReadOnlyList<Id<global::Pruefdomaene.Posten.Posten>> Ziele, int MengeJeZiel) : ICommand;

/// <summary>Die Quelle ist reserviert (<see cref="QuelleReserviert"/>): jedes Ziel wird zugebucht (JE: ein Unterlauf je Ziel; gezählt wird an der Verteilung).</summary>
public record Zubuchen(Id<global::Pruefdomaene.Verteilung.Verteilung> VerteilungId, IReadOnlyList<Id<global::Pruefdomaene.Posten.Posten>> Ziele, int MengeJeZiel) : ICommand;

/// <summary>Ein Ziel der Verteilung ist zuzubuchen — je Ziel ein Unterlauf (aus dem Strom der <see cref="Zielaufteilung"/>).</summary>
public record ZielZubuchen(Id<global::Pruefdomaene.Verteilung.Verteilung> VerteilungId, Id<global::Pruefdomaene.Posten.Posten> Ziel, int Menge) : ICommand;
