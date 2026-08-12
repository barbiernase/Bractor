using Domain.DddPatterns.Gemeinsam;

namespace Domain.DddPatterns.Bestellung;

/// <summary>
/// DDD-Baustein DOMAIN EVENT — ein fachlich bedeutsames Faktum in der Vergangenheitsform.
/// Marker für alles, was in der Bestell-Domäne geschehen ist. Unveränderlich (records).
/// </summary>
public interface IDomaenenEreignis { }

public sealed record BestellungEroeffnet(Guid BestellungId, Guid KundeId, Geld Kreditlimit) : IDomaenenEreignis;

public sealed record PositionHinzugefuegt(string ArtikelNr, string Bezeichnung, int Menge, Geld Stueckpreis) : IDomaenenEreignis;

public sealed record PositionMengeGeaendert(string ArtikelNr, int NeueMenge) : IDomaenenEreignis;

public sealed record PositionEntfernt(string ArtikelNr) : IDomaenenEreignis;

public sealed record BestellungAufgegeben(Guid BestellungId, Geld Gesamtsumme) : IDomaenenEreignis;

public sealed record BestellungStorniert(Guid BestellungId, string Grund) : IDomaenenEreignis;
