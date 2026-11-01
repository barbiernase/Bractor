using Abstractions;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;

namespace Domain.Pipeline.Sammelvorgang;

/// <summary>
/// Fan-in-Verdrahtung: jedes fertige Teil meldet sich an SEINER Barriere. Die Folge <c>TeilFertig</c> wird auf <c>MeldeTeilFertig</c> am
/// zugehörigen Sammelvorgang abgebildet — die Zugehörigkeit ist das fachliche Feld in der Folge (A2). Zähler und Barriere liegen im Aggregat;
/// diese Funktion ist nur die eine Kante Folge → Command. Derselbe Eingang ergibt dieselbe Command-Id, die Inbox des Sammelvorgangs verwirft
/// Wiederholungen.
/// </summary>
public sealed class TeilFertigMeldung : IFunktion
{
    public MeldeTeilFertig Auf(TeilFertig f) => new(f.Sammelvorgang);
}
