using Abstractions;

namespace Domain.Reiseauftrag;

/// <summary>
/// Trigger-Aggregat der Reisebuchungs-Saga (eigener Namespace — pro Namespace genau EIN Aggregat). Sein
/// Event <see cref="ReiseGebucht"/> ist der fachliche Auslöser, aus dem der Korrelations-Router den
/// Reise-Prozess startet. Es trägt die Auftrags-Id (= sein eigener Stream) UND die eigenständige
/// Reise-Aggregat-Id explizit als Felder, weil die Prozess-Regeln nur den Event-Payload sehen.
/// </summary>
public partial class Reiseauftrag : IState
{
    public bool Gebucht { get; set; }
}

// Der Fach-Command, den der Aufrufer dispatcht. AggregateId = die Auftrags-Id (der eigene Stream).
// Reise/Flug/Hotel = EIGENE Ids für die jeweiligen Ziel-Aggregate (jeder Stream braucht eine eindeutige Guid).
public record BucheReise(Guid AggregateId, Guid Reise, Guid Flug, Guid Hotel, Guid Kunde, int Plaetze, int Zimmer) : ICommand;

// Der Auslöser-Event. ReiseId = AggregateId des Auftrags (= eigener Stream); Reise = die Bestätigungs-Aggregat-Id.
public record ReiseGebucht(Guid ReiseId, Guid Reise, Guid Flug, Guid Hotel, Guid Kunde, int Plaetze, int Zimmer) : IEvent;

public partial class Reiseauftrag
{
    public partial class Decider : IDecider<Reiseauftrag>
    {
        public IEnumerable<OneOf<ReiseGebucht>> Decide(BucheReise cmd)
        {
            if (this.State.Version > 0) yield break;   // schon gebucht → Noop (idempotent)
            yield return new ReiseGebucht(cmd.AggregateId, cmd.Reise, cmd.Flug, cmd.Hotel, cmd.Kunde, cmd.Plaetze, cmd.Zimmer);
        }
    }

    public partial class Applier : IApplier<Reiseauftrag>
    {
        public void Apply(ReiseGebucht evt) => this.State.Gebucht = true;
    }
}
