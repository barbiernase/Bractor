using Abstractions;

namespace Pruefdomaene.Abrechnung;

/// <summary>Stellt die Rechnung; mit einem Zahlungsziel (&gt; 0 Sekunden) prüft die Uhr danach, ob sie bezahlt ist.</summary>
public record StelleRechnung(Id<Rechnung> AggregateId, Id<Kundenkonto> Konto, decimal Betrag, int ZahlungszielSekunden = 0) : ICommand<Rechnung>, ICreationCommand;
public record GibRechnungFrei(Id<Rechnung> AggregateId) : ICommand<Rechnung>;
public record SperreRechnung(Id<Rechnung> AggregateId, string Grund) : ICommand<Rechnung>;
public record BezahleRechnung(Id<Rechnung> AggregateId) : ICommand<Rechnung>;

/// <summary>Prüft, ob die Rechnung bezahlt ist — gesendet von der Uhr, wenn ihre Zahlungsfrist fällig ist.</summary>
public record PruefeZahlung(Id<Rechnung> AggregateId) : ICommand<Rechnung>;

public record BucheGutschrift(Id<Kundenkonto> AggregateId, Id<Rechnung> Rechnung, decimal Betrag) : ICommand<Kundenkonto>;
public record VermerkeMahnung(Id<Kundenkonto> AggregateId, Id<Rechnung> Rechnung, string Versand, int Stufe) : ICommand<Kundenkonto>;
