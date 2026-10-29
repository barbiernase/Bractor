namespace Abstractions;

/// <summary>
/// Optionaler LEBENSZYKLUS einer Funktion im Host (docs/04 §4.4, docs/10 E10): was eine Aktivierung laden muss, bevor sie rechnet (ein
/// neuronales Netz auf die GPU, eine Verbindung, einen Cache), und was sie vor dem Sichern ihres Zustands zurückschreiben muss. Beide Methoden sind
/// vorbelegt — eine Funktion überschreibt nur, was sie braucht. Das Ende ist <see cref="IAsyncDisposable"/> (bzw. <see cref="IDisposable"/>) der
/// Funktion und ihrer Dienste.
/// <code>
/// public sealed class Klassifikator(IZustand&lt;Netz&gt; netz, IGpu gpu) : IFunktion, ILebenszyklus, IAsyncDisposable
/// {
///     private readonly GpuModell _modell = new();                                                     // Ressource der Aktivierung, kein Zustand
///     public Task BeimAktivierenAsync(CancellationToken ct) => _modell.LadeAsync(gpu, netz.Wert.Gewichte, ct);
///     public Task VorDemSichernAsync(CancellationToken ct) { netz.Wert.Gewichte = _modell.Gewichte(); return Task.CompletedTask; }
///     public ValueTask DisposeAsync() => _modell.DisposeAsync();
///     public KlassifiziereBildPaarDurchKi Auf(Merkmale m) => …;
/// }
/// </code>
/// Der Funktions-Actor ruft je Aktivierung: bauen → Zustand laden → <see cref="BeimAktivierenAsync"/> → erst dann Eingänge und Hinweise; vor jeder
/// Sicherung (im Ruhepunkt, auch beim Stoppen) <see cref="VorDemSichernAsync"/>; am Ende die Freigabe. Wirft
/// <see cref="BeimAktivierenAsync"/>, ist die Aktivierung gescheitert, nicht ein Eingang: sie beginnt neu, kein Stream wird geparkt. Eine Funktion
/// mit Lebenszyklus ist nie gleichbleibend (sie lädt etwas, R12). Nur an einer Funktions-Klasse — draußen gehört der Lebenszyklus dem Worker
/// (CQRS107).
/// </summary>
public interface ILebenszyklus
{
    /// <summary>Nach dem Bau und nach dem Laden des Zustands, vor dem ersten <c>Auf</c> — einmal je Aktivierung.</summary>
    Task BeimAktivierenAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Nur mit Zustand (<see cref="IZustand{T}"/>): direkt vor jeder Sicherung, im Ruhepunkt (kein Eingang läuft) — etwa Gewichte von der GPU nach
    /// <c>Zustand.Wert</c> zurückholen. Gesichert wird, was danach in <c>Wert</c> steht.
    /// </summary>
    Task VorDemSichernAsync(CancellationToken ct) => Task.CompletedTask;
}
