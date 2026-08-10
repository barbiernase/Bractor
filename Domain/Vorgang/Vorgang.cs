using Abstractions;

namespace Domain.Vorgang;

/// <summary>
/// Ziel-Aggregat der Verkettungs-Demo. Es durchläuft ZWEI Schritte, die von ZWEI verketteten Prozessen
/// getrieben werden: erst <c>Genehmige</c> (Prozess A), dann — ausgelöst durch das PERSISTIERTE
/// <see cref="VorgangGenehmigt"/> — <c>Aktiviere</c> (Prozess B). REINE Domäne; die Verkettung sieht das
/// Aggregat nie. Beide Commands treffen DENSELBEN Stream (die Vorgang-Id) — das ist korrekt, es ist dasselbe
/// Aggregat; nur der Antrag hat einen eigenen Stream.
/// </summary>
public partial class Vorgang : IState
{
    public bool Genehmigt { get; set; }
    public bool Aktiviert { get; set; }
}

public record Genehmige(Guid AggregateId) : ICommand;
public record Aktiviere(Guid AggregateId) : ICommand;

public record VorgangGenehmigt(Guid VorgangId) : IEvent;
public record VorgangAktiviert(Guid VorgangId) : IEvent;

public partial class Vorgang
{
    public partial class Decider : IDecider<Vorgang>
    {
        public IEnumerable<OneOf<VorgangGenehmigt>> Decide(Genehmige cmd)
        {
            if (this.State.Genehmigt) yield break;   // fachlich: nur einmal
            yield return new VorgangGenehmigt(cmd.AggregateId);
        }

        public IEnumerable<OneOf<VorgangAktiviert>> Decide(Aktiviere cmd)
        {
            if (this.State.Aktiviert) yield break;   // fachlich: nur einmal
            yield return new VorgangAktiviert(cmd.AggregateId);
        }
    }

    public partial class Applier : IApplier<Vorgang>
    {
        public void Apply(VorgangGenehmigt evt) => this.State.Genehmigt = true;
        public void Apply(VorgangAktiviert evt) => this.State.Aktiviert = true;
    }
}
