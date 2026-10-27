using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>
/// Eine Funktion, die einen Hinweis hört, als Schnittstelle (Plan §2.9, E9): unter derselben Adresse rechnet sie im Host (<see cref="Schwellenpruefung"/>)
/// oder draußen bei einem Träger (Worker, Mensch) — der Absender sieht nie, was dahinter rechnet.
/// </summary>
public interface IAusreisserpruefung : IFunktion
{
    MeldeAusreisser? Auf(Merkmal m);
}

/// <summary>Die Umsetzung im Host: ein Wert über der Schwelle ist ein Ausreißer.</summary>
public sealed class Schwellenpruefung : IAusreisserpruefung
{
    public const double Schwelle = 1_000;

    public MeldeAusreisser? Auf(Merkmal m) => m.Wert > Schwelle ? new MeldeAusreisser(m.Stelle, m.Wert) : null;
}
