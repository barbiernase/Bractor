using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>Der Auslöser des <see cref="AuftragsProzess"/>; trägt die eigene Id, damit die Bestätigung ihr Ziel kennt.</summary>
public record AuftragGestartet(Id<Auftrag> AuftragId, Id<Posten.Posten> PostenId, Id<Kontingent.Kontingent> KontingentId, int Menge) : IEvent;
public record AuftragBestaetigt() : IEvent;

public record AuftragExistiertBereits(Id<Auftrag> AuftragId) : ITransientEvent;
public record AuftragNichtGefunden(Id<Auftrag> AuftragId) : ITransientEvent;
