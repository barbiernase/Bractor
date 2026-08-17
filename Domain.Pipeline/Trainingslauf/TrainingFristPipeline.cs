using System.Security.Cryptography;
using System.Text;
using Abstractions;
using Domain.Trainingslauf;
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.Trainingslauf;

/// <summary>
/// Timeout-Wächter für Trainingsläufe (Konzept §4.3) — plant beim Beginn eine durable
/// <see cref="Frist"/> gegen die DB-Uhr und räumt sie beim regulären Ende wieder ab.
///
/// Feuert die Frist, bevor der Lauf abschließt, stellt der Deadline-Scheduler
/// <c>MarkiereAlsHaengengeblieben</c> ans Ziel-Aggregat zu (Routing über den Kontext
/// <see cref="Kontext"/>, siehe Host.Grpc <c>AddDeadlines</c>). Eine späte Frist ist
/// wirkungslos — das Aggregat ignoriert sie, wenn der Lauf schon beendet ist.
///
/// Die Frist-Id ist deterministisch aus der Trainingslauf-Id abgeleitet: erneutes Planen
/// (Event-Redelivery) überschreibt idempotent, und das Abräumen trifft genau dieselbe Frist.
/// Reine Seiteneffekt-Handler (kein Command-Yield), analog <c>ImageProcessingPipeline</c>.
/// </summary>
public partial class TrainingFristPipeline : IPipelineHandler
{
    /// <summary>Kontext-Diskriminator, an dem der Host die fällige Frist auf den richtigen Command routet.</summary>
    public const string Kontext = "training-timeout";

    private readonly IFristplan _fristplan;
    private readonly IDbClock _uhr;
    private readonly TrainingFristConfig _config;
    private readonly ILogger<TrainingFristPipeline> _logger;

    public TrainingFristPipeline(
        IFristplan fristplan,
        IDbClock uhr,
        TrainingFristConfig config,
        ILogger<TrainingFristPipeline> logger)
    {
        _fristplan = fristplan;
        _uhr = uhr;
        _config = config;
        _logger = logger;
    }

    public string PipelineId => "training-frist";

    // ─── Beginn: Frist planen ───

    public async Task Handle(TrainingBegonnen evt, PipelineContext ctx)
    {
        var id = ctx.SourceAggregateId!.Value;
        var jetzt = await _uhr.JetztAsync();
        var faellig = jetzt + _config.Timeout;

        await _fristplan.PlaneAsync(new Frist(FristId(id), faellig, id, Kontext));

        _logger.LogInformation(
            "Training-Frist: {Id} läuft ab {Faellig:u} (Timeout {Timeout})", id, faellig, _config.Timeout);
    }

    // ─── Reguläres Ende: Frist abräumen ───

    public Task Handle(TrainingAbgeschlossen evt, PipelineContext ctx) => Entferne(ctx);
    public Task Handle(TrainingGescheitert evt, PipelineContext ctx) => Entferne(ctx);
    public Task Handle(TrainingAbgebrochen evt, PipelineContext ctx) => Entferne(ctx);

    private async Task Entferne(PipelineContext ctx)
    {
        var id = ctx.SourceAggregateId!.Value;
        await _fristplan.EntferneAsync(FristId(id));   // folgenlos, falls schon gefeuert/entfernt
    }

    /// <summary>Deterministische Frist-Id aus der Trainingslauf-Id (analog <see cref="FristId"/>-Ableitungen).</summary>
    private static Guid FristId(Guid trainingslaufId)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes($"training-frist:{trainingslaufId:N}"), hash);
        return new Guid(hash);
    }
}

/// <summary>Timeout-Dauer für Trainingsläufe (registriert in DomainPipelineExtensions).</summary>
public record TrainingFristConfig(TimeSpan Timeout);
