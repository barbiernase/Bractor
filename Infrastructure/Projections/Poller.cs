using Abstractions;

namespace Infrastructure.Projections;

/// <summary>
/// Poll-Backstop der Emittenten (docs/04 §4.1): scannt periodisch das globale Event-Log jenseits seiner High-Water-Mark und weckt die
/// Adapter der Streams, die ein abonniertes Event bekommen haben.
///
/// Er heilt den einen Fall, den das Coalescing NICHT auffängt — das letzte verlorene Signal vor Stille: danach kommt nie wieder eine
/// Weckung, und ausgerechnet Abschluss-Events sind oft die wichtigsten. Der Poll wandelt „für immer verloren" in „höchstens ein
/// Poll-Intervall Latenz". Signal = Geschwindigkeit, Poll = Sicherheit.
///
/// Der Poller ist bewusst dumm: er sagt nur „Stream X hat sich bewegt, schau nach". Ob dort wirklich etwas zu tun ist, entscheidet der
/// Adapter über seinen Cursor. Er liest seitenweise und nur Streams mit einem Event der abonnierten Typen (PF-12): früher weckte er jeden
/// geänderten Stream des Logs, und der Adapter legte für jeden einen Cursor an.
/// </summary>
public sealed class Poller
{
    /// <summary>Abonnierte Events je Seite.</summary>
    public const int SeitenGroesse = 1000;

    private readonly IEventStoreRepository _eventStore;
    private readonly Func<Guid, CancellationToken, Task<bool>> _wake;
    private readonly IReadOnlyCollection<string>? _typen;
    private long _highWater;

    /// <param name="wake">Weckt den Adapter eines Streams und meldet zurück, ob die
    /// Verarbeitung <b>bestätigt aufgeholt</b> ist (<c>true</c>) oder unbestätigt blieb
    /// (<c>false</c>: Timeout, Dispatch-Fehler, Cluster-Churn, kein WakeAck). Nur ein
    /// bestätigtes Aufholen darf den Cursor vorrücken lassen (Befund 1).</param>
    /// <param name="typen">Speicher-Diskriminatoren der abonnierten Typen samt Upcasting-Herkunft (<see cref="BatchLeser.TypenAus(IEnumerable{Type})"/>);
    /// null = jeder Typ.</param>
    public Poller(
        IEventStoreRepository eventStore,
        Func<Guid, CancellationToken, Task<bool>> wake,
        long startHighWater = 0,
        IReadOnlyCollection<string>? typen = null)
    {
        _eventStore = eventStore;
        _wake = wake;
        _highWater = startHighWater;
        _typen = typen;
    }

    public long HighWater => _highWater;

    /// <summary>Ein Poll-Durchlauf: Seite um Seite die geänderten Streams holen, jeden wecken, High-Water vorrücken.</summary>
    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        while (true)
        {
            var changes = await _eventStore.ReadChangedStreamsAsync(_highWater, _typen, SeitenGroesse, ct);

            // Befund 1: Der Cursor darf NUR vorrücken, wenn JEDE Weckung dieser Seite als aufgeholt BESTÄTIGT zurückkommt. Eine
            // unbestätigt gebliebene Weckung (Timeout, Dispatch-/Store-Fehler, Cluster-Churn) darf die HWM nicht über einen noch nicht
            // verarbeiteten Stream hinaus schieben — sonst weckt auf einem terminalen Stream nie wieder etwas und die Projektion hängt still
            // zurück. Bleibt auch nur EINE Weckung unbestätigt, hält der Poller die HWM: der nächste Durchlauf re-scannt dasselbe Fenster
            // (folgenlos für bereits aufgeholte Streams — deren Cursor macht den Read leer).
            var alleBestaetigt = true;
            foreach (var streamId in changes.StreamIds)
            {
                bool aufgeholt;
                try
                {
                    aufgeholt = await _wake(streamId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // Shutdown: sauber abbrechen, nicht als Fehlversuch zählen.
                }
                catch
                {
                    aufgeholt = false; // Weckung geworfen → unbestätigt.
                }

                if (!aufgeholt) alleBestaetigt = false;   // die übrigen trotzdem wecken (Latenz), nur die Marke hält
            }
            if (!alleBestaetigt) return;

            var vorher = _highWater;
            _highWater = Math.Max(_highWater, changes.HighWaterMark);
            if (!changes.Voll || _highWater == vorher) return;
        }
    }
}
