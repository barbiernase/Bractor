using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine Funktion draußen (Schnittstelle): wer eine gestellte Rechnung freigibt oder sperrt, entscheidet ein Mensch (Laufort Hand) oder ein Worker; im
/// Host rechnet sonst der <see cref="Freigabeautomat"/>. Die erste Antwort gewinnt am Schlüssel „Funktion, Eingang“; gesendet wird beim Funktions-Actor.
/// </summary>
public interface IFreigabe : IFunktion
{
    OneOf<GibRechnungFrei, SperreRechnung> Auf(RechnungGestellt e);
}
