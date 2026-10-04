using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.ImagePair;
using Domain.Pipeline.ImageProcessing;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Dienste als Akteure (docs/konzept-akteure.md): die KI (<see cref="IClassifierService"/> : IAkteur) kommt als Parameter an den
/// Pipeline-Handle; der GENERIERTE Dispatch setzt „im Auftrag von" um den Aufruf, der Emit stempelt den Akteur als Urheber.
/// Ohne Cluster: echter generierter Dispatch + echter <see cref="CommandEmitter"/> über die Sende-Naht.
/// </summary>
public class AkteurDienstTests
{
    private sealed class KiDouble : IClassifierService
    {
        public Task<ClassificationResult> ClassifyPairAsync(Guid pairId) => Task.FromResult(new ClassificationResult(default));
    }
    private sealed class Werkzeug : IImageResizer, IHistogramEqualizer
    {
        public Mat Resize(Mat input, int targetHeight) => input;
        public Mat Equalize(Mat input) => input;
    }

    [Fact]
    public void Der_Dienst_Vertrag_ist_der_Akteur_nicht_seine_Implementierung()
    {
        var ki = GeneratedAkteurRechte.Alle["IClassifierService"];
        ki.Typ.Should().Be(typeof(IClassifierService));
        ki.Commands.Should().BeEquivalentTo(new[] { typeof(KlassifiziereBildPaarDurchKi) });
        GeneratedAkteurRechte.Alle.Keys.Should().NotContain("ClassifierService", "die Implementierung ist kein eigener Akteur");
    }

    [Fact]
    public async Task Der_generierte_Dispatch_entscheidet_im_Auftrag_der_KI()
    {
        var pipeline = new ImageProcessingPipeline(new Werkzeug(), new Werkzeug(),
            new PreprocessingConfig(Path.Combine(Path.GetTempPath(), "akteur-dienst-test")), NullLogger<ImageProcessingPipeline>.Instance);
        var paar = Guid.NewGuid();
        var gesendet = new List<(ICommand Cmd, string? Akteur)>();

        await pipeline.DispatchEventAsync(
            new EventEnvelope { AggregateId = paar, AggregateType = "ImagePair", Payload = new ImagePairKomplett() },
            new PipelineContext { CorrelationId = Guid.NewGuid().ToString(), SourceAggregateId = paar },
            cmd => { gesendet.Add((cmd, ImAuftrag.Akteur)); return Task.CompletedTask; },
            _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask,
            new FaehigkeitenAus(new KiDouble()));

        gesendet.Should().ContainSingle();
        gesendet[0].Cmd.Should().BeOfType<KlassifiziereBildPaarDurchKi>();
        gesendet[0].Akteur.Should().Be("IClassifierService");
        ImAuftrag.Akteur.Should().BeNull("der Auftrag endet mit dem Handle-Aufruf");
    }

    [Fact]
    public async Task Der_Emit_stempelt_den_Akteur_als_Urheber()
    {
        var umschlaege = new List<CommandEnvelope>();
        var emitter = new CommandEmitter((_, env, _) => { umschlaege.Add(env); return Task.FromResult<CommandResult?>(null); });
        var cmd = new KlassifiziereBildPaarDurchKi(Guid.NewGuid(), default);
        var k = new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "x");

        using (ImAuftrag.Von("IClassifierService"))
            await emitter.EmitAsync(cmd, k, CancellationToken.None);
        await emitter.EmitAsync(cmd, k with { Diskriminator = "y" }, CancellationToken.None);

        umschlaege.Should().HaveCount(2);
        umschlaege[0].UserId.Should().Be("IClassifierService");
        umschlaege[1].UserId.Should().Be("system", "ohne Akteur bleibt es eine reine Automation");
        umschlaege[0].CommandId.Should().Be(Infrastructure.Aggregate.EmitId.Ableiten(k, cmd.AggregateId), "der Akteur ändert die Idempotenz nicht");
    }
}
