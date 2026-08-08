using Abstractions;

namespace Domain.Pipeline.Benchmark;

// Bewusst DUMME Benchmark-Pipeline: misst den reinen eingehenden Trigger→Ack-Pfad OHNE
// Persistenz. Der Trigger trägt nur eine Zahl; der Handler tut NICHTS (Task.CompletedTask) —
// kein Command yielden ⇒ kein Aggregate-Send ⇒ kein Event-Store-Append.
//
// Nur für den LoadHarness (--mode pipeline). Kein Domänen-Wert.

/// <summary>Simpelster denkbarer Pipeline-Trigger: eine Sequenznummer, sonst nichts.</summary>
public record BenchPing(int Seq) : IPipelineTrigger;

/// <summary>
/// No-Op-Pipeline. Ein Actor pro PipelineId (serielle Mailbox) — der gemessene Durchsatz ist
/// der einer EINZELNEN Pipeline. Parameterloser Ctor ⇒ vom generierten AddSingleton auflösbar.
/// </summary>
public partial class BenchmarkPipeline : IPipelineHandler
{
    public string PipelineId => "benchmark";

    // Task-Rückgabe (kein yield) ⇒ der Dispatch-Generator erzeugt reines "await Handle(...)":
    // kein Command, kein Broadcast, keine Persistenz.
    public Task Handle(BenchPing ping, PipelineContext ctx) => Task.CompletedTask;
}
