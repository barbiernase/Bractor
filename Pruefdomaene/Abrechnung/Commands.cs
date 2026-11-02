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

/// <summary>Die Rechnung ist gestellt: freigeben oder sperren entscheidet die Freigabe (im Host ein Automat, draußen ein Mensch oder ein Worker).</summary>
public record Freizugeben(Id<Rechnung> Rechnung, decimal Betrag) : ICommand;

/// <summary>Die Rechnung ist bezahlt: der Betrag geht dem Konto gut.</summary>
public record Gutschreibung(Id<Kundenkonto> Konto, Id<Rechnung> Rechnung, decimal Betrag) : ICommand;

/// <summary>Die Zahlung ist überfällig: die nächste Mahnstufe ist zu versenden.</summary>
public record Mahnfall(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto, decimal Betrag, int Mahnstufe) : ICommand;
