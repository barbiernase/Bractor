using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>Der Auftrag beginnt: die Auftragsabwicklung reserviert den Posten und belegt das Kontingent (zwei unabhängige Zweige).</summary>
public record AuftragGestartet(Id<Auftrag> AuftragId, Id<Posten.Posten> PostenId, Id<Kontingent.Kontingent> KontingentId, int Menge) : IEvent;

public record ReservierungVermerkt() : IEvent;
public record BelegungVermerkt() : IEvent;

/// <summary>Beide Zweige sind gelungen, keiner abgelehnt.</summary>
public record AuftragBestaetigt() : IEvent;

/// <summary>Der erste abgelehnte Zweig: der Auftrag wird nie bestätigt.</summary>
public record AuftragGescheitert(string Grund) : IEvent;
/// <summary>
/// KOMPENSATION, ausgeschrieben (docs/13 §13.7): der Auftrag ist gescheitert, die Reservierung war gelungen — sie muss zurück. Genau einmal (nach dem
/// Scheitern entscheidet das Aggregat keine weitere Absage); den Posten gibt die Auftragsabwicklung frei, angestoßen von einem Client.
/// </summary>
public record PostenfreigabeAngefordert(Id<Posten.Posten> PostenId, int Menge) : IEvent;
/// <summary>Gegenzug der gelungenen Belegung eines gescheiterten Auftrags (wie <see cref="PostenfreigabeAngefordert"/>).</summary>
public record KontingententlastungAngefordert(Id<Kontingent.Kontingent> KontingentId, int Menge) : IEvent;

public record AuftragExistiertBereits(Id<Auftrag> AuftragId) : ITransientEvent;
public record AuftragNichtGefunden(Id<Auftrag> AuftragId) : ITransientEvent;
