using Abstractions;
using Pruefdomaene.Import;

namespace Pruefdomaene.Pipeline.Import;

/// <summary>
/// Eine gleichbleibende Funktion auf einer Quell-Nachricht: die Anforderung startet den Import — seine Id ist aus der Herkunft abgeleitet (R13), also beim
/// Wiederholen dieselbe, und die Inbox des Importauftrags verwirft das Zweite.
/// </summary>
public sealed class Importannahme : IFunktion
{
    public StarteImport Auf(ImportAngefordert e, Herkunft h) => new(h.NeueId<Importauftrag>(), e.Zeilen);
}
