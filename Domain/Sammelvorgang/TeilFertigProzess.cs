using Abstractions;
using Domain.Teilauftrag;

namespace Domain.Sammelvorgang;

/// <summary>
/// Fan-in-Verdrahtung: jedes fertige Teil meldet sich an SEINER Barriere. Ein <c>TeilauftragAbgeschlossen</c>
/// wird auf <c>MeldeTeilFertig</c> am zugehörigen <see cref="Sammelvorgang"/> abgebildet (Korrelation =
/// die im Event mitgeführte <c>SammelvorgangId</c>). Der Zähler-Fold + die Barriere selbst liegen im
/// Aggregat; dieser Prozess ist nur die eine Kante Event→Command.
///
/// Kein Join, kein Count-Join, keine Collection — genau ein <c>Auf → Sende</c>. Registrierung erfolgt
/// automatisch (ProzessRegelnGenerator findet jede <see cref="IProzessDefinition"/>).
/// </summary>
public sealed class TeilFertigProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<TeilauftragAbgeschlossen>.Definiere(p =>
        p.Auf<TeilauftragAbgeschlossen>()
         .Sende<MeldeTeilFertig>(e => new MeldeTeilFertig(e.SammelvorgangId)));
}
