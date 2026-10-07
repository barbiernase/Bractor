using Abstractions;
using Domain.Trainingslauf;
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.Trainingslauf;

/// <summary>
/// Timeout-Wächter für Trainingsläufe (Konzept §4.3) — plant beim Beginn eine durable Frist
/// (<see cref="Frist{TCmd}"/> mit <see cref="MarkiereAlsHaengengeblieben"/>) gegen die DB-Uhr und räumt sie beim
/// regulären Ende wieder ab (<see cref="FristStorno{TCmd}"/>).
///
/// Beides sind typisierte AUSGÄNGE der Signatur, keine Aufrufe: der generierte Dispatch reicht sie an den
/// Fristplan, der Fristen-Router (<c>GeneratedFristen</c>) baut beim Feuern den Command aus der Ziel-Id. Die
/// Frist-Id ist deterministisch (Command-Typ + Trainingslauf-Id), die Fälligkeit zählt ab der Log-Zeit von
/// <c>TrainingBegonnen</c>: erneutes Planen (Redelivery, Poll ab 0) ergibt dieselbe Frist und schiebt sie nicht —
/// sie läuft 6 h nach dem Beginn ab, egal wie viel Fortschritt kommt. Das Abräumen trifft genau dieselbe Frist.
/// Eine späte Frist ist wirkungslos — das Aggregat ignoriert sie, wenn der Lauf schon beendet ist.
/// </summary>
public partial class TrainingFristPipeline : IPipelineHandler
{
    private readonly TrainingFristConfig _config;
    private readonly ILogger<TrainingFristPipeline> _logger;

    public TrainingFristPipeline(TrainingFristConfig config, ILogger<TrainingFristPipeline> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string PipelineId => "training-frist";

    // ─── Beginn: Frist planen ───

    public IEnumerable<OneOf<Frist<MarkiereAlsHaengengeblieben>>> Handle(TrainingBegonnen evt, PipelineContext ctx)
    {
        var id = ctx.SourceAggregateId!.Value;
        _logger.LogInformation("Training-Frist: {Id} läuft nach {Timeout} ab", id, _config.Timeout);
        yield return new Frist<MarkiereAlsHaengengeblieben>(id, _config.Timeout);
    }

    // ─── Reguläres Ende: Frist abräumen ───

    public IEnumerable<OneOf<FristStorno<MarkiereAlsHaengengeblieben>>> Handle(TrainingAbgeschlossen evt, PipelineContext ctx) => Storno(ctx);
    public IEnumerable<OneOf<FristStorno<MarkiereAlsHaengengeblieben>>> Handle(TrainingGescheitert evt, PipelineContext ctx) => Storno(ctx);
    public IEnumerable<OneOf<FristStorno<MarkiereAlsHaengengeblieben>>> Handle(TrainingAbgebrochen evt, PipelineContext ctx) => Storno(ctx);

    private static IEnumerable<OneOf<FristStorno<MarkiereAlsHaengengeblieben>>> Storno(PipelineContext ctx)
    {
        yield return new FristStorno<MarkiereAlsHaengengeblieben>(ctx.SourceAggregateId!.Value);
    }
}

/// <summary>Timeout-Dauer für Trainingsläufe (registriert in DomainPipelineExtensions).</summary>
public record TrainingFristConfig(TimeSpan Timeout);
