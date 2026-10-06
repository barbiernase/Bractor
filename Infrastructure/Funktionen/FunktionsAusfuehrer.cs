using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Aggregate;   // KommandoAbgelehnt — die durable Fehlschlag-Marke, die der Prozess-Fold liest

namespace Infrastructure.Funktionen;

/// <summary>Wie eine Funktion auf diesem Knoten ausgeführt wird: gleichzeitige Slots und Wiederholungen bei Ausnahmen.</summary>
public sealed record FunktionsBindung(Type Funktion, int Slots = 1, int Wiederholungen = 0);

/// <summary>Was jede Ausführung mitbekommt (siehe <see cref="IAusfuehrung"/>).</summary>
internal sealed record Ausfuehrung(Guid AusfuehrungsId, Guid Korrelation, CancellationToken Abbruch) : IAusfuehrung;

/// <summary>
/// Der AUSFÜHRER der Katalog-Funktionen auf einem Knoten — die Hülle um den Inhalt (<see cref="IFunktion"/>). Er nimmt
/// Aufträge des Prozess-Managers fire-and-forget entgegen, begrenzt die Gleichzeitigkeit je Funktion (Slots), führt aus und
/// schreibt GENAU EIN Ergebnis in den Ausführungs-Stream (Stream-Id = Vorgang, CausationId = Vorgang): den gewählten
/// OneOf-Fall als Event — oder, wenn die Funktion auch nach den Wiederholungen wirft, die durable Fehlschlag-Marke
/// <see cref="KommandoAbgelehnt"/>. Danach weckt er den Prozess. Der Prozess-Fold liest dieses Ergebnis wie das Event eines
/// Aggregats (Wirkung → Token für <c>Auf&lt;Ergebnis&gt;</c>; Marke → SchrittGescheitert → Kompensation).
///
/// Idempotent auf drei Ebenen: (1) ein Auftrag, der auf DIESEM Knoten schon läuft, wird nicht doppelt gestartet; (2) liegt
/// im Ausführungs-Stream schon ein Ergebnis, wird nicht erneut gerechnet; (3) das Schreiben ist ein StartStream (Version 0) —
/// rechnen zwei Knoten denselben Auftrag, gewinnt genau ein Ergebnis. Deshalb darf der Manager offene Aufträge bei jeder
/// Weckung erneut übergeben: das heilt einen verlorenen Ausführer, ohne doppelt zu wirken.
///
/// Transport-frei: der Aufruf (<c>rufe</c>, live der generierte <c>GeneratedFunktionen.RufeAsync</c>) und die Weckung
/// (<c>weckeProzess</c>, live ein bounded Cluster-Request) sind Delegaten — im Prüfstand in-memory beweisbar.
/// </summary>
public sealed class FunktionsAusfuehrer
{
    private readonly IEventStoreRepository _store;
    private readonly Func<IAuftrag, IAusfuehrung, Task<IEvent>> _rufe;
    private readonly Func<Guid, CancellationToken, Task> _weckeProzess;
    private readonly Func<IAuftrag, FunktionsBindung?> _bindung;
    private readonly TimeSpan _wiederholPause;
    private readonly ConcurrentDictionary<Guid, Task> _laufend = new();
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _slots = new();

    public FunktionsAusfuehrer(
        IEventStoreRepository store,
        Func<IAuftrag, IAusfuehrung, Task<IEvent>> rufe,
        Func<Guid, CancellationToken, Task> weckeProzess,
        Func<IAuftrag, FunktionsBindung?> bindung,
        TimeSpan? wiederholPause = null)
    {
        _store = store;
        _rufe = rufe;
        _weckeProzess = weckeProzess;
        _bindung = bindung;
        _wiederholPause = wiederholPause ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Wie viele Aufträge auf diesem Knoten gerade laufen (Beobachtung/Test).</summary>
    public int Laufend => _laufend.Count;

    /// <summary>
    /// Übergibt einen Auftrag (fire-and-forget). Kehrt sofort zurück — die Ausführung läuft entkoppelt vom Manager-Turn,
    /// ihr Ende meldet die Weckung des Prozesses.
    /// </summary>
    public Task BeauftrageAsync(Guid korrelation, IAuftrag auftrag, Guid vorgang, string? akteur, CancellationToken ct)
    {
        _laufend.GetOrAdd(vorgang, v => Task.Run(async () =>
        {
            try { await FuehreAusAsync(korrelation, auftrag, v, akteur, CancellationToken.None); }
            finally { _laufend.TryRemove(v, out _); }
        }));
        return Task.CompletedTask;
    }

    /// <summary>Wartet, bis alle gerade laufenden Aufträge fertig sind (Prüfstand/geordnetes Herunterfahren).</summary>
    public async Task WarteAufAlleAsync()
    {
        while (!_laufend.IsEmpty)
            await Task.WhenAll(_laufend.Values.ToArray());
    }

    /// <summary>Eine Ausführung: schon erledigt? → nur wecken. Sonst im Slot rechnen, genau ein Ergebnis schreiben, wecken.</summary>
    public async Task FuehreAusAsync(Guid korrelation, IAuftrag auftrag, Guid vorgang, string? akteur, CancellationToken ct)
    {
        if ((await _store.ReadStreamAsync(vorgang, 0, ct)).Count > 0)
        {
            await WeckeAsync(korrelation, ct);
            return;
        }

        var bindung = _bindung(auftrag);
        var name = bindung?.Funktion.Name ?? auftrag.GetType().Name;
        IEvent ergebnis;
        if (bindung is null)
        {
            ergebnis = new KommandoAbgelehnt(vorgang, $"Funktion für '{auftrag.GetType().Name}' ist nicht gebunden");
        }
        else
        {
            var slot = _slots.GetOrAdd(bindung.Funktion, _ => new SemaphoreSlim(Math.Max(1, bindung.Slots)));
            await slot.WaitAsync(ct);
            try { ergebnis = await RechneAsync(auftrag, new Ausfuehrung(vorgang, korrelation, ct), bindung, name, vorgang); }
            finally { slot.Release(); }
        }

        try
        {
            await _store.AppendEventsAsync(vorgang, 0, new[] { ergebnis },
                correlationId: korrelation.ToString(), causationId: vorgang.ToString(),
                aggregateType: $"Funktion:{name}", akteur: akteur);
        }
        catch (Exception ex)
        {
            // Hat ein anderer Knoten denselben Auftrag zuerst abgeschlossen, liegt sein Ergebnis schon da — gut so.
            // Sonst bleibt der Auftrag offen; der Manager übergibt ihn bei der nächsten Weckung erneut (Backstop heilt).
            if ((await _store.ReadStreamAsync(vorgang, 0, ct)).Count == 0)
                Console.WriteLine($"[Funktion] Ergebnis von {name} ({vorgang}) nicht schreibbar: {ex.Message}");
        }

        await WeckeAsync(korrelation, ct);
    }

    private async Task<IEvent> RechneAsync(IAuftrag auftrag, IAusfuehrung x, FunktionsBindung bindung, string name, Guid vorgang)
    {
        for (var versuch = 0; ; versuch++)
        {
            try
            {
                return await _rufe(auftrag, x);
            }
            catch (Exception ex) when (versuch < bindung.Wiederholungen)
            {
                Console.WriteLine($"[Funktion] {name} Versuch {versuch + 1} fehlgeschlagen ({ex.Message}) — wiederhole");
                await Task.Delay(_wiederholPause * (versuch + 1));
            }
            catch (Exception ex)
            {
                return new KommandoAbgelehnt(vorgang, $"{name}: {ex.Message}");
            }
        }
    }

    private async Task WeckeAsync(Guid korrelation, CancellationToken ct)
    {
        try { await _weckeProzess(korrelation, ct); }
        catch (Exception ex) { Console.WriteLine($"[Funktion] Weckung des Prozesses {korrelation} fehlgeschlagen: {ex.Message} — Backstop heilt"); }
    }
}
