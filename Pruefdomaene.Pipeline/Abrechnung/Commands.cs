using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

// COMMANDS ohne Ziel an den Köpfen des Abrechnungsablaufs (docs/04 §4.4): nie im Log. Ein Client, der auf das Fakt reagiert, gibt ihn hinein (Schritt 6: ein Decide gibt nur Fakten aus).

/// <summary>Die Rechnung ist gestellt (<see cref="RechnungGestellt"/>): freigeben oder dem Rechnungsprüfer vorlegen entscheidet die Freigabe.</summary>
public record Freizugeben(Id<Rechnung> Rechnung, decimal Betrag) : ICommand;

/// <summary>Die Rechnung ist bezahlt (<see cref="RechnungBezahlt"/>): der Betrag geht dem Konto gut.</summary>
public record Gutschreibung(Id<Kundenkonto> Konto, Id<Rechnung> Rechnung, decimal Betrag) : ICommand;

/// <summary>Die Zahlung ist überfällig (<see cref="ZahlungUeberfaellig"/>): die nächste Mahnstufe ist zu versenden.</summary>
public record Mahnfall(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto, decimal Betrag, int Mahnstufe) : ICommand;
