namespace Abstractions;

/// <summary>
/// Optionaler LEBENSZYKLUS einer Funktion im Host (docs/04 §4.4, docs/10 E10): was eine Aktivierung laden muss, bevor sie rechnet (ein
/// neuronales Netz auf die GPU, eine Verbindung, einen Cache). Vorbelegt — eine Funktion überschreibt es nur, wenn sie es braucht. Das Ende ist
/// <see cref="IAsyncDisposable"/> (bzw. <see cref="IDisposable"/>) der Funktion und ihrer Dienste.
/// <code>
/// public sealed class Klassifikator(IGpu gpu) : IFunktion, ILebenszyklus, IAsyncDisposable
/// {
///     private readonly GpuModell _modell = new();                                                     // Ressource der Aktivierung, kein Zustand
///     public Task BeimAktivierenAsync(CancellationToken ct) => _modell.LadeAsync(gpu, ct);
///     public ValueTask DisposeAsync() => _modell.DisposeAsync();
///     public KlassifiziereBildPaarDurchKi Auf(PaarZuBeurteilen p) => …;
/// }
/// </code>
/// Das Kind ruft je Aktivierung: bauen → <see cref="BeimAktivierenAsync"/> → erst dann Eingänge; am Ende die Freigabe. Wirft
/// <see cref="BeimAktivierenAsync"/>, ist die Aktivierung gescheitert, nicht ein Eingang. Nur an einer Funktions-Klasse — draußen gehört der
/// Lebenszyklus dem Worker (CQRS107).
/// </summary>
public interface ILebenszyklus
{
    /// <summary>Nach dem Bau, vor dem ersten <c>Auf</c> — einmal je Aktivierung.</summary>
    Task BeimAktivierenAsync(CancellationToken ct) => Task.CompletedTask;
}
