using Abstractions;
using Domain.Benchmark;

namespace Domain.Pipeline.Benchmark;

// Bewusst DUMMER Benchmark-Fluss: misst den reinen Eingang einer Quelle — Quell-Nachricht genau einmal ins Log (StartStream je
// Kennung), Dirigent starten, Vorgang ohne weiteren Knoten beenden. Kein Command, kein Aggregat. Kein Domänen-Wert.

/// <summary>Nur die Quelle — gemessen wird der Weg Quelle → Log → Dirigent → beendet.</summary>
public sealed class Benchmark : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var ping = p.Quelle<BenchPing>();
    });
}
