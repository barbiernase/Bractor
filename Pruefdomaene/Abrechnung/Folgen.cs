using Abstractions;

namespace Pruefdomaene.Abrechnung;

// FOLGEN der Abrechnung (docs/03 §3.2): was eine Entscheidung den Funktionen sagt, die sie hören — schlichte Records, nicht im Log.

/// <summary>Die Rechnung ist gestellt: freigeben oder sperren entscheidet die Freigabe (im Host ein Automat, draußen ein Mensch oder ein Worker).</summary>
public record Freizugeben(Id<Rechnung> Rechnung, decimal Betrag);

/// <summary>Die Rechnung ist bezahlt: der Betrag geht dem Konto gut.</summary>
public record Gutschreibung(Id<Kundenkonto> Konto, Id<Rechnung> Rechnung, decimal Betrag);

/// <summary>Die Zahlung ist überfällig: die nächste Mahnstufe ist zu versenden.</summary>
public record Mahnfall(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto, decimal Betrag, int Mahnstufe);
