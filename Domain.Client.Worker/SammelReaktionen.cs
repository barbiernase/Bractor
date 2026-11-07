using Client.Infrastructure.Reaktionen;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;

namespace Domain.Client.Worker;

/// <summary>
/// Die Reaktion des Teilmelders (Fan-in, Schritt 6): ein fertiges Teil (<see cref="TeilauftragAbgeschlossen"/>, ein Fakt seines Teilauftrags) meldet
/// er an der Barriere, die das Event nennt (<see cref="MeldeTeilFertig"/>). Zähler und Barriere liegen im Sammelvorgang; dieselbe Position ergibt
/// dieselbe Command-Id, die Inbox des Sammelvorgangs verwirft Wiederholungen.
/// </summary>
public static class SammelReaktionen
{
    public static Reaktionen Registriere(Reaktionen r) => r.Auf<TeilauftragAbgeschlossen>(MeldeAsync);

    public static MeldeTeilFertig Meldung(TeilauftragAbgeschlossen e) => new(e.SammelvorgangId);

    private static Task MeldeAsync(TeilauftragAbgeschlossen e, ReaktionsKontext k)
    {
        k.Sende(Meldung(e));
        return Task.CompletedTask;
    }
}
