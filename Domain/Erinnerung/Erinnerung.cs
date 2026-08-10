using Abstractions;

namespace Domain.Erinnerung;

/// <summary>
/// Demo-Ziel des Deadline-Primitivs: ein Aggregat, das eine fällig gewordene Frist empfängt. Der
/// Deadline-Scheduler stellt bei Fälligkeit <see cref="FristFaellig"/> zu (deterministische CommandId →
/// Inbox-Dedup); das Aggregat verbucht die Erinnerung genau einmal. REINE Domäne — es weiß nichts von Uhren,
/// Fristplan oder Scheduler; für es ist <see cref="FristFaellig"/> ein Command wie jeder andere.
/// </summary>
public partial class Erinnerung : IState
{
    public bool Erinnert { get; set; }
    public string? Kontext { get; set; }
}

public record FristFaellig(Guid AggregateId, string Kontext) : ICommand;
public record ErinnerungAusgeloest(string Kontext) : IEvent;

public partial class Erinnerung
{
    public partial class Decider : IDecider<Erinnerung>
    {
        public IEnumerable<OneOf<ErinnerungAusgeloest>> Decide(FristFaellig cmd)
        {
            if (this.State.Erinnert) yield break;   // fachlich: nur einmal
            yield return new ErinnerungAusgeloest(cmd.Kontext);
        }
    }

    public partial class Applier : IApplier<Erinnerung>
    {
        public void Apply(ErinnerungAusgeloest evt)
        {
            this.State.Erinnert = true;
            this.State.Kontext = evt.Kontext;
        }
    }
}
