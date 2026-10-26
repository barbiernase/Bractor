using Abstractions;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;

namespace Domain.Pipeline.Sammelvorgang;

/// <summary>
/// Fan-in-Verdrahtung: jedes fertige Teil meldet sich an SEINER Barriere. Ein <c>TeilauftragAbgeschlossen</c> wird auf <c>MeldeTeilFertig</c> am
/// zugehörigen Sammelvorgang abgebildet — die Zugehörigkeit ist das fachliche Feld <c>SammelvorgangId</c> im Event (A2). Zähler und Barriere
/// liegen im Aggregat; diese Funktion ist nur die eine Kante Event → Command. Gleichbleibend (R12): ohne Festhalten, derselbe Eingang ergibt
/// dieselbe Command-Id, die Inbox des Sammelvorgangs verwirft Wiederholungen.
/// </summary>
public sealed class TeilFertigMeldung : IFunktion
{
    public MeldeTeilFertig Auf(TeilauftragAbgeschlossen e) => new(e.SammelvorgangId);
}
