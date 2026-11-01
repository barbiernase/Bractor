using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine Funktion draußen (Schnittstelle): wer eine gestellte Rechnung freigibt oder sperrt, entscheidet ein Mensch (Laufort Hand) oder ein Worker; im
/// Host rechnet sonst der <see cref="Freigabeautomat"/>. Sie hört die Folge der Rechnung (vor dem Aggregat, nie das Log); die erste Antwort gewinnt,
/// gesendet wird beim Kind ihres Parents.
/// </summary>
public interface IFreigabe : IFunktion
{
    OneOf<GibRechnungFrei, SperreRechnung> Auf(Freizugeben f);
}

/// <summary>Gerufen (kein Auf): der Versand einer Mahnung (Außenwelt) — liefert die Versandnummer des Briefs.</summary>
public interface IMahnversand : IFunktion
{
    Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe);
}
