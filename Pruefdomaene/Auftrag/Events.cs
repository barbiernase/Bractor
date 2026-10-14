using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>Der Auslöser des <see cref="AuftragsProzess"/>; trägt die eigene Id, damit die Bestätigung ihr Ziel kennt.</summary>
public record AuftragGestartet(Guid AuftragId, Guid PostenId, Guid KontingentId, int Menge) : IEvent;
public record AuftragBestaetigt() : IEvent;

public record AuftragExistiertBereits(Guid AuftragId) : ITransientEvent;
public record AuftragNichtGefunden(Guid AuftragId) : ITransientEvent;
