using Abstractions;

namespace Domain.Benchmark;

/// <summary>Simpelste denkbare Quell-Nachricht: eine Sequenznummer, sonst nichts (Kennung = Seq). Gemessen vom Fluss <c>Benchmark</c>.</summary>
public sealed record BenchPing(int Seq) : IQuellNachricht
{
    public string Kennung => Seq.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
