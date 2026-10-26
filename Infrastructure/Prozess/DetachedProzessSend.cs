using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.PubSub;

namespace Infrastructure.Prozess;

/// <summary>
/// Das Feuern einer Transition aus Sicht des Manager-Turns (docs/04 §4.5): der Ziel-Send läuft losgelöst, der Turn kehrt sofort zurück.
/// Den Ausgang bestimmt weiter der Fold (Treiber-Fold, EM-1: Wirkung, <c>KommandoVerarbeitet</c>, <c>KommandoAbgelehnt</c>); der
/// <see cref="EmitAusgang"/> des Sends entscheidet nur, <b>wann</b> der Manager wieder geweckt wird und <b>ob</b> er erneut senden darf (PF-9):
/// <list type="bullet">
///   <item>Zugestellt oder abgelehnt → sofort wecken: der Fold sieht den durablen Ausgang.</item>
///   <item>Unbekannt (Frist abgelaufen, Ziel überlastet, technischer Fehler) → NICHT sofort wecken, sondern nach einem Backoff je Vorgang
///     (1 s, 2 s, 4 s … höchstens 30 s, gestreut). Erst dann faltet der Manager und sendet erneut, falls der Schritt noch offen ist.</item>
///   <item>Solange der Send eines Vorgangs läuft oder sein Backoff wartet, sendet keine andere Weckung (Signal, Poll, Backstop) ihn erneut.</item>
/// </list>
/// So baut ein überlastetes Ziel seinen Rückstau ab, statt dass Timeouts und sofortige Wiederholungen ihn vervielfachen. Mit der Drossel je
/// Ziel im Emitter (<c>EmitDrossel</c>) ist der Retry-Sturm geschlossen. Die Wirkung bleibt genau einmal (deterministische CommandId, Inbox).
///
/// Eine Instanz je Manager-Actor (je Korrelation); der Zustand ist flüchtig — nach einer Reaktivierung darf ein Vorgang einmal sofort
/// erneut senden, was die Inbox verwirft. Transport-agnostisch: Send und Weckung sind Nähte, im Prüfstand Fakes.
/// </summary>
public sealed class DetachedProzessSend
{
    private readonly Func<Guid, ICommand, Guid, CancellationToken, Task<EmitErgebnis>> _emit;
    private readonly Func<Guid, Guid, Guid, int, CancellationToken, Task> _danach;
    private readonly Func<int, TimeSpan> _backoff;
    private readonly Func<TimeSpan, CancellationToken, Task> _warte;

    // Vorgang → Versuche bisher; Gesperrt, solange Send oder Backoff laufen.
    private readonly Dictionary<Guid, (bool Gesperrt, int Versuche)> _vorgaenge = new();

    /// <param name="emit">Der gebundene Ziel-Send — live <c>CommandEmitter.EmitAsync</c> mit dem Vorgang als CommandId.</param>
    /// <param name="danach">
    /// Weckt den Manager neu (out-of-turn), damit er Fortschritt, Terminal oder Fehlschlag aus dem Fold liest. Nötig, weil (a) das
    /// Ergebnis-Event der letzten Transition Auslöser keiner Regel ist — sein Signal abonniert der Router nicht — und (b) die Ablehnungs-Marke
    /// (<see cref="IProzessIntern"/>) kein Signal erzeugt. Bekommt (Korrelation, Ziel-Stream, Vorgang, ab Version): der Manager liest genau den
    /// Ziel-Stream nach — ab der Version, an der dieser Send den Ausgang geschrieben hat (<see cref="EmitErgebnis.ErsteVersion"/>, 0 = unbekannt) —
    /// und nichts, wenn er den Ausgang des Vorgangs schon kennt. Idempotent.
    /// </param>
    /// <param name="backoff">Wartezeit vor der n-ten Wiederholung (n ≥ 1); Standard <see cref="StandardBackoff"/>.</param>
    /// <param name="warte">Die Wartenaht (live <see cref="Task.Delay(TimeSpan, CancellationToken)"/>).</param>
    public DetachedProzessSend(
        Func<Guid, ICommand, Guid, CancellationToken, Task<EmitErgebnis>> emit,
        Func<Guid, Guid, Guid, int, CancellationToken, Task> danach,
        Func<int, TimeSpan>? backoff = null,
        Func<TimeSpan, CancellationToken, Task>? warte = null)
    {
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        _danach = danach ?? throw new ArgumentNullException(nameof(danach));
        _backoff = backoff ?? StandardBackoff;
        _warte = warte ?? Task.Delay;
    }

    /// <summary>1 s, 2 s, 4 s … höchstens 30 s, ±20 % gestreut — damit viele Vorgänge an einem Ziel nicht im Gleichtakt wiederholen.</summary>
    public static TimeSpan StandardBackoff(int versuch)
    {
        var basis = Math.Min(30.0, Math.Pow(2, Math.Max(0, versuch - 1)));
        return TimeSpan.FromSeconds(basis * (0.8 + 0.4 * Random.Shared.NextDouble()));
    }

    /// <summary>Ob der Vorgang gerade gesperrt ist (Send oder Backoff läuft) — Test, Diagnose.</summary>
    public bool IstGesperrt(Guid vorgang)
    {
        lock (_vorgaenge) return _vorgaenge.TryGetValue(vorgang, out var z) && z.Gesperrt;
    }

    /// <summary>Der Dispatch des <see cref="ProzessManager"/>: sendet losgelöst, kehrt sofort zurück; ein gesperrter Vorgang wird übersprungen.</summary>
    public Task SendeAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        lock (_vorgaenge)
        {
            _vorgaenge.TryGetValue(vorgang, out var z);
            if (z.Gesperrt) return Task.CompletedTask;     // läuft schon oder wartet im Backoff — dessen Weckung kommt
            _vorgaenge[vorgang] = (true, z.Versuche);
        }
        _ = LaufeAsync(korrelation, cmd, vorgang, ct);
        return Task.CompletedTask;                          // SOFORT zurück — der Turn läuft weiter
    }

    private async Task LaufeAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        EmitErgebnis ergebnis;
        try
        {
            ergebnis = await _emit(korrelation, cmd, vorgang, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DetachedProzessSend] Send fehlgeschlagen ({cmd.GetType().Name}): {ex.Message}");
            ergebnis = EmitErgebnis.Unbekannt;
        }
        // Wo der Ausgang steht, weiß nur ein entschiedener Send, der ihn selbst geschrieben hat; sonst liest der Manager wie bisher.
        var abVersion = ergebnis.Ausgang.IstEntschieden() ? ergebnis.ErsteVersion : 0;

        if (ergebnis.Ausgang.IstEntschieden())
        {
            lock (_vorgaenge) _vorgaenge.Remove(vorgang);
        }
        else
        {
            int versuche;
            lock (_vorgaenge)
            {
                versuche = _vorgaenge.TryGetValue(vorgang, out var z) ? z.Versuche + 1 : 1;
                _vorgaenge[vorgang] = (true, versuche);      // bleibt gesperrt bis nach dem Backoff
            }
            try { await _warte(_backoff(versuche), ct); }
            catch (OperationCanceledException) { }          // Actor stoppt: Backstop bzw. Reaktivierung heilen
            lock (_vorgaenge) _vorgaenge[vorgang] = (false, versuche);
            if (ct.IsCancellationRequested) return;
        }

        try { await _danach(korrelation, cmd.Ziel, vorgang, abVersion, ct); }
        catch (Exception ex)
        {
            Console.WriteLine($"[DetachedProzessSend] Selbst-Weckung fehlgeschlagen ({korrelation}): {ex.Message}");
        }
    }
}
