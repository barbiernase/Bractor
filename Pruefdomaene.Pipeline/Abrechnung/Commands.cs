using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

// COMMANDS, die Knoten abgeben (docs/04 §4.4): Commands ohne Ziel, nie im Log.

/// <summary>Eine große Rechnung ist von Hand zu prüfen — Arbeit des Rechnungsprüfers (ein Akteur, der ihn hört).</summary>
public record RechnungZuPruefen(Id<Rechnung> Rechnung, decimal Betrag) : ICommand;
