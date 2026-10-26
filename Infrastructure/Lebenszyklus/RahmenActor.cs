using Abstractions;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Lebenszyklus;

/// <summary>
/// Der Lebenszyklus jedes Framework-Actors (Plan Pub/Sub-Neustrukturierung §1.7, Phase 2) — einmal hier statt je Actor von Hand:
/// <list type="number">
/// <item><b>Bereich:</b> mit einer <see cref="IFaehigkeitsFabrik"/> öffnet der Rahmen beim <see cref="Started"/> einen Fähigkeits-Bereich
///   (<see cref="Bereich"/>, ein DI-Scope: jede Fähigkeit eines Stores liefert darin dieselbe Instanz) und gibt ihn frei, wenn der Actor
///   gestoppt oder neu gestartet ist <b>und</b> keine Arbeit ihn mehr nutzt.</item>
/// <item><b>Stopp:</b> beim <see cref="Stopping"/> (und vor einem Neustart) bricht er <see cref="Stopp"/> ab und wartet begrenzt
///   (<see cref="StandardAbwarten"/>) auf die Arbeit neben der Mailbox (<see cref="Neben"/>); was länger läuft, behält den Bereich bis zu
///   seinem Ende. Erst dann läuft <see cref="BeimStoppenAsync"/> (nur beim Stopp, nicht vor einem Neustart).</item>
/// <item><b>Passivierung:</b> mit einem Leerlauf &gt; 0 stoppt sich der Actor, wenn er so lange keine Nachricht bekam — nie, solange er
///   <see cref="Beschaeftigt"/> ist (PF-5; der durable Zustand liegt nie im Actor).</item>
/// <item><b>Fehler:</b> wirft die Behandlung einer Nachricht, bekommt ein wartender Absender die ehrliche Antwort aus
///   <see cref="Fehlerantwort"/> (etwa „nicht aufgeholt“) statt in seine Frist zu laufen; danach geht der Fehler an die Supervision von
///   Proto.Actor (Neustart mit frischem Zustand, wie ohne Rahmen). Ein Actor, der einen Fehler selbst beantwortet, wirft ihn nicht weiter.</item>
/// <item><b>Zusammenfassen:</b> <see cref="Wecke"/> startet einen Durchlauf neben der Mailbox; läuft schon einer, merkt sich der Actor
///   „noch einmal“ — beliebig viele Weckungen während eines Durchlaufs ergeben genau einen weiteren.</item>
/// </list>
/// Cluster-Nachrichten verschickt ein Actor nur über den <see cref="Bote"/> (<c>Melde</c>, <c>Frage</c>).
/// </summary>
public abstract class RahmenActor : IActor, IAsyncDisposable
{
    /// <summary>Wie lange ein Stopp höchstens auf laufende Arbeit wartet.</summary>
    public static readonly TimeSpan StandardAbwarten = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _leerlauf;
    private readonly IFaehigkeitsFabrik? _fabrik;
    private readonly TimeSpan _abwarten;
    private readonly ILogger? _rahmenLogger;
    private readonly CancellationTokenSource _stopp = new();
    private readonly List<Task> _laeufe = new();       // Arbeit neben der Mailbox, bis ihre Fortsetzung im Actor gelaufen ist
    private IFaehigkeitsBereich? _bereich;
    private bool _imDurchlauf;
    private bool _nochmal;

    /// <param name="logger">für Fehler des Rahmens (Behandlung, Stopp).</param>
    /// <param name="leerlauf">Passivierung nach so viel Stille; <see cref="TimeSpan.Zero"/> = nie.</param>
    /// <param name="faehigkeiten">öffnet den <see cref="Bereich"/>; null = der Actor braucht keinen.</param>
    /// <param name="abwarten">wie lange ein Stopp auf laufende Arbeit wartet (Standard <see cref="StandardAbwarten"/>).</param>
    protected RahmenActor(ILogger? logger = null, TimeSpan leerlauf = default, IFaehigkeitsFabrik? faehigkeiten = null, TimeSpan? abwarten = null)
    {
        _rahmenLogger = logger;
        _leerlauf = leerlauf;
        _fabrik = faehigkeiten;
        _abwarten = abwarten ?? StandardAbwarten;
    }

    /// <summary>Abgebrochen, sobald der Actor stoppt oder neu startet — das Token jeder Arbeit dieses Actors.</summary>
    protected CancellationToken Stopp => _stopp.Token;

    /// <summary>Der Fähigkeits-Bereich dieser Aktivierung (ab <see cref="Started"/>).</summary>
    protected IFaehigkeiten Bereich => _bereich
        ?? throw new InvalidOperationException($"{GetType().Name}: kein Fähigkeits-Bereich (ohne Fabrik oder vor dem Start)");

    /// <summary>Läuft gerade Arbeit, die eine Passivierung verlöre? Standard: Arbeit neben der Mailbox.</summary>
    protected virtual bool Beschaeftigt => _laeufe.Count > 0;

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                if (_fabrik is not null) _bereich ??= _fabrik.Oeffne();
                if (_leerlauf > TimeSpan.Zero) context.SetReceiveTimeout(_leerlauf);
                await BeginneAsync(context);                 // wirft es, startet die Supervision neu (wie ohne Rahmen)
                return;

            case ReceiveTimeout:
                if (!Beschaeftigt) context.Stop(context.Self);
                return;

            case Stopping:
                await BeendeLaeufeAsync();
                try { await BeimStoppenAsync(context); }
                catch (Exception ex) { _rahmenLogger?.LogWarning(ex, "[{Actor}] Fehler beim Stoppen", GetType().Name); }
                return;

            case Restarting:
                // Nach einem Fehler: kein BeimStoppen (der Zustand ist nicht mehr verlässlich), nur abbrechen, abwarten, freigeben.
                await BeendeLaeufeAsync();
                return;

            case Stopped:
                GibFrei();
                return;
        }

        try
        {
            await BehandleAsync(context);
        }
        catch (Exception ex)
        {
            _rahmenLogger?.LogWarning(ex, "[{Actor}] {Nachricht} gescheitert", GetType().Name, context.Message?.GetType().Name);
            if (context.Sender is not null && context.Message is { } nachricht && Fehlerantwort(nachricht, ex) is { } antwort)
                context.Respond(antwort);
            throw;
        }
    }

    /// <summary>Eine Nachricht (alles außer den Lebenszyklus-Nachrichten).</summary>
    protected abstract Task BehandleAsync(IContext context);

    /// <summary>Nach dem Öffnen des Bereichs, bevor die erste Nachricht kommt.</summary>
    protected virtual Task BeginneAsync(IContext context) => Task.CompletedTask;

    /// <summary>Beim Stopp, nachdem die laufende Arbeit beendet ist (oder die Wartezeit um ist), bevor der Bereich frei wird.</summary>
    protected virtual Task BeimStoppenAsync(IContext context) => Task.CompletedTask;

    /// <summary>Die ehrliche Antwort an einen wartenden Absender, wenn die Behandlung von <paramref name="nachricht"/> warf; null = keine.</summary>
    protected virtual object? Fehlerantwort(object nachricht, Exception fehler) => null;

    /// <summary>
    /// Arbeit neben der Mailbox (<c>ReenterAfter</c>): sie bekommt <see cref="Stopp"/>, hält Passivierung und Freigabe auf, und
    /// <paramref name="danach"/> läuft wieder im Actor (mit dem erledigten Task).
    /// </summary>
    protected void Neben(IContext context, Func<CancellationToken, Task> arbeit, Func<Task, Task>? danach = null)
    {
        var lauf = arbeit(Stopp);
        _laeufe.Add(lauf);
        context.ReenterAfter(lauf, async t =>
        {
            _laeufe.Remove(lauf);
            if (danach is not null) await danach(t);
        });
    }

    /// <summary>
    /// Weckt den Durchlauf: läuft keiner, startet er neben der Mailbox; läuft einer, folgt ihm genau ein weiterer — gleich, wie viele
    /// Weckungen dazwischen kommen (der laufende hat das, was sie meldeten, womöglich nicht mehr gesehen). Nach dem Stopp keiner mehr.
    /// </summary>
    protected void Wecke(IContext context, Func<CancellationToken, Task> durchlauf)
    {
        if (Stopp.IsCancellationRequested) return;
        if (_imDurchlauf) { _nochmal = true; return; }
        _imDurchlauf = true;
        _nochmal = false;
        Neben(context, durchlauf, _ =>
        {
            _imDurchlauf = false;
            if (_nochmal) Wecke(context, durchlauf);
            return Task.CompletedTask;
        });
    }

    private async Task BeendeLaeufeAsync()
    {
        _stopp.Cancel();
        var offen = Offen();
        if (offen.Length == 0) return;
        try
        {
            await Task.WhenAll(offen).WaitAsync(_abwarten);
        }
        catch (TimeoutException)
        {
            _rahmenLogger?.LogWarning("[{Actor}] {Anzahl} Läufe nach {Sekunden} s noch nicht beendet — ihr Bereich wird frei, sobald sie enden",
                GetType().Name, offen.Count(t => !t.IsCompleted), _abwarten.TotalSeconds);
        }
        catch { /* ein gescheiterter Lauf hat seinen Fehler selbst gemeldet */ }
    }

    private Task[] Offen() => _laeufe.Where(t => !t.IsCompleted).ToArray();

    /// <summary>Gibt den Bereich frei — sofort, oder wenn die letzte Arbeit endet, die ihn noch nutzt.</summary>
    private void GibFrei()
    {
        var bereich = Interlocked.Exchange(ref _bereich, null);
        if (bereich is null) return;
        var offen = Offen();
        if (offen.Length == 0) bereich.Dispose();
        else _ = GibFreiNachAsync(bereich, offen);
    }

    private static async Task GibFreiNachAsync(IDisposable bereich, Task[] laeufe)
    {
        try { await Task.WhenAll(laeufe).ConfigureAwait(false); }
        catch { /* gemeldet vom Lauf selbst */ }
        bereich.Dispose();
    }

    /// <summary>Proto.Actor ruft das nach <see cref="Stopped"/> und vor einem Neustart — der Bereich wird in beiden Fällen frei.</summary>
    public ValueTask DisposeAsync()
    {
        GibFrei();
        _stopp.Cancel();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
