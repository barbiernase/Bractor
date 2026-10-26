using Abstractions;

namespace Pruefdomaene.Abrechnung;

public record RechnungGestellt(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto, decimal Betrag) : IEvent;
public record RechnungFreigegeben() : IEvent;
public record RechnungGesperrt(string Grund) : IEvent;
public record RechnungBezahlt(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto, decimal Betrag) : IEvent;
public record ZahlungUeberfaellig(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto, decimal Betrag, int Mahnstufe) : IEvent;

public record GutschriftGebucht(Id<Rechnung> Rechnung, decimal Betrag) : IEvent;
public record MahnungVermerkt(Id<Rechnung> Rechnung, string Versand, int Stufe) : IEvent;

public record RechnungExistiert(Id<Rechnung> RechnungId) : ITransientEvent;
public record RechnungUnbekannt(Id<Rechnung> RechnungId) : ITransientEvent;
