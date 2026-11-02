using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>
/// Eine Funktion als Schnittstelle (docs/04 §4.4, docs/10 E9): als Knoten rechnet sie im Host (<see cref="Schwellenpruefung"/>) oder draußen bei einem
/// Träger (Worker, Mensch) — der Knoten davor sieht nie, was dahinter rechnet.
/// </summary>
public interface IAusreisserpruefung : IFunktion
{
    IEnumerable<MeldeAusreisser> Auf(Merkmal m);
}

/// <summary>Die Umsetzung im Host: ein Wert über der Schwelle ist ein Ausreißer.</summary>
public sealed class Schwellenpruefung : IAusreisserpruefung
{
    public const double Schwelle = 1_000;

    public IEnumerable<MeldeAusreisser> Auf(Merkmal m)
    {
        if (m.Wert > Schwelle) yield return new MeldeAusreisser(m.Stelle, m.Wert);
    }
}
