using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Aggregate;     // KommandoVerarbeitet / KommandoAbgelehnt
using Infrastructure.Funktionen;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Funktionen;

// ── Die Test-Domäne: ein Bild kommt an, zwei Katalog-Funktionen laufen parallel, ein Join meldet das Ergebnis ──

public sealed record BildEingegangen(string Pfad) : IEvent;

public sealed record VorAuftrag(string Pfad) : IAuftrag<IVorverarbeitung>;
public sealed record Vorverarbeitet(string Vorschau) : IEvent;
public sealed record Unlesbar(string Grund) : IEvent;
public interface IVorverarbeitung : IFunktion
{
    Task<OneOf<Vorverarbeitet, Unlesbar>> RufeAsync(VorAuftrag a, IAusfuehrung x);
}

public sealed record MetaAuftrag(string Pfad) : IAuftrag<IMetadaten>;
public sealed record MetaGelesen(string Kamera) : IEvent;
public interface IMetadaten : IFunktion
{
    Task<OneOf<MetaGelesen>> RufeAsync(MetaAuftrag a, IAusfuehrung x);
}

public sealed record MeldeFertig(Guid AggregateId, string Vorschau, string Kamera) : ICommand;
public sealed record Fertig(string Vorschau, string Kamera) : IEvent;
public sealed record MeldeUnbrauchbar(Guid AggregateId, string Grund) : ICommand;
public sealed record Unbrauchbar(string Grund) : IEvent;
public sealed record RaeumeVorschauAuf(Guid AggregateId, string Vorschau) : ICommand;
public sealed record VorschauAufgeraeumt(string Vorschau) : IEvent;

/// <summary>Das Ziel-Aggregat der Meldungen (ein fester Stream).</summary>
public static class Bildakte
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
}

/// <summary>Fork (zwei Funktionen auf denselben Auslöser) → Join (beide Ergebnisse) → Command; Zweig bei Unlesbar.</summary>
public sealed class BildAufbereitung(TimeSpan? limit = null, bool mitKompensation = false) : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<BildEingegangen>.Definiere(p =>
    {
        var vor = p.Auf<BildEingegangen>().Rufe<IVorverarbeitung>(b => new VorAuftrag(b.Pfad));
        if (limit is { } l) vor = vor.Zeitlimit(l);
        if (mitKompensation) vor.RückgängigDurch<RaeumeVorschauAuf>(b => new RaeumeVorschauAuf(Bildakte.Id, b.Pfad));

        p.Auf<BildEingegangen>().Rufe<IMetadaten>(b => new MetaAuftrag(b.Pfad));

        p.Auf<Vorverarbeitet>().Und<MetaGelesen>()
         .Sende<MeldeFertig>((v, m) => new MeldeFertig(Bildakte.Id, v.Vorschau, m.Kamera));
        p.Auf<Unlesbar>().Sende<MeldeUnbrauchbar>(u => new MeldeUnbrauchbar(Bildakte.Id, u.Grund));
    });
}

/// <summary>
/// In-Memory-Log mit OCC wie Marten (StartStream bei Version 0 wirft, wenn es den Stream schon gibt) und
/// DB-Zeitstempeln aus einer steuerbaren Uhr — nur für die store-freie Logik des Managers/Ausführers (Prüfstand).
/// </summary>
internal sealed class LogImSpeicher : IEventStoreRepository
{
    private readonly Dictionary<Guid, List<EventEnvelope>> _streams = new();
    private readonly object _lock = new();
    public DateTimeOffset Jetzt { get; set; } = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
    {
        lock (_lock)
        {
            IReadOnlyList<EventEnvelope> res = _streams.TryGetValue(streamId, out var evs)
                ? evs.Where(e => e.AggregateVersion >= fromVersion).ToList()
                : Array.Empty<EventEnvelope>();
            return Task.FromResult(res);
        }
    }

    public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events,
        string? correlationId = null, string? causationId = null, string? aggregateType = null, string? akteur = null)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(aggregateId, out var stream)) { stream = new List<EventEnvelope>(); _streams[aggregateId] = stream; }
            if (expectedVersion != stream.Count)
                throw new ConcurrencyException($"OCC: erwartet {expectedVersion}, ist {stream.Count} (Stream {aggregateId}).");
            foreach (var e in events)
                stream.Add(new EventEnvelope
                {
                    AggregateId = aggregateId, AggregateVersion = stream.Count + 1, Payload = e,
                    CausationId = causationId ?? Guid.NewGuid().ToString(), CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                    AggregateType = aggregateType ?? "", UserId = akteur ?? "system", CreatedAtUtc = Jetzt,
                });
            return Task.CompletedTask;
        }
    }

    public IReadOnlyList<Guid> AlleStreamIds() { lock (_lock) return _streams.Keys.ToList(); }

    public IReadOnlyList<EventEnvelope> Stream(Guid id) { lock (_lock) return _streams.TryGetValue(id, out var s) ? s.ToList() : new List<EventEnvelope>(); }

    public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct) => throw new NotSupportedException();
    public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => throw new NotSupportedException();
}

/// <summary>Marking-Cache im Speicher (für den Cursor-Pfad des Managers).</summary>
internal sealed class MarkingImSpeicher : IProzessMarkingStore
{
    private readonly ConcurrentDictionary<Guid, ProzessMarking> _d = new();
    public Task<ProzessMarking?> LadeAsync(Guid k, CancellationToken ct) => Task.FromResult(_d.TryGetValue(k, out var m) ? m : null);
    public Task SchreibeAsync(ProzessMarking m, CancellationToken ct) { _d[m.Id] = m; return Task.CompletedTask; }
    public Task LöscheAsync(Guid k, CancellationToken ct) { _d.TryRemove(k, out _); return Task.CompletedTask; }
}

/// <summary>
/// Die Welt eines Laufs: echter <see cref="ProzessManager"/>, echter <see cref="FunktionsAusfuehrer"/>, ein Log im Speicher.
/// Die Funktionen sind Fakes (Delegaten je Auftrag); das Ziel-Aggregat beantwortet Commands wie der emittierte Pfad des
/// AggregateActorBase (Event + KommandoVerarbeitet mit CausationId = Vorgang, oder die Ablehnungs-Marke).
/// </summary>
internal sealed class FunktionsWelt
{
    public LogImSpeicher Log { get; } = new();
    public ProzessManager Manager { get; }
    public FunktionsAusfuehrer Ausfuehrer { get; }
    public List<ICommand> Gesendet { get; } = new();
    public ConcurrentQueue<Guid> Weckungen { get; } = new();
    public HashSet<Type> LehneAb { get; } = new();
    public Func<VorAuftrag, IAusfuehrung, Task<OneOf<Vorverarbeitet, Unlesbar>>> Vor { get; set; } =
        (a, _) => Task.FromResult<OneOf<Vorverarbeitet, Unlesbar>>(new Vorverarbeitet(a.Pfad + ".png"));
    public Func<MetaAuftrag, IAusfuehrung, Task<OneOf<MetaGelesen>>> Meta { get; set; } =
        (_, _) => Task.FromResult<OneOf<MetaGelesen>>(new MetaGelesen("K1"));
    public int Uebergaben;

    public readonly Guid Korrelation = Guid.NewGuid();
    private readonly Guid _auslöserStream = Guid.NewGuid();

    public FunktionsWelt(int wiederholungen = 0, bool mitCursor = false, TimeSpan? limit = null, bool mitKompensation = false,
        Func<Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task>, Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task>>? umRufe = null)
    {
        var bindungen = new Dictionary<Type, FunktionsBindung>
        {
            [typeof(IVorverarbeitung)] = new(typeof(IVorverarbeitung), Slots: 2, Wiederholungen: wiederholungen),
            [typeof(IMetadaten)] = new(typeof(IMetadaten), Slots: 2, Wiederholungen: wiederholungen),
        };
        Ausfuehrer = new FunktionsAusfuehrer(Log,
            async (auftrag, x) => auftrag switch
            {
                VorAuftrag v => (IEvent)(await Vor(v, x)).Value,
                MetaAuftrag m => (IEvent)(await Meta(m, x)).Value,
                _ => throw new NotSupportedException(),
            },
            (korr, _) => { Weckungen.Enqueue(korr); return Task.CompletedTask; },
            a => a switch { VorAuftrag => bindungen[typeof(IVorverarbeitung)], MetaAuftrag => bindungen[typeof(IMetadaten)], _ => null },
            wiederholPause: TimeSpan.FromMilliseconds(1));

        Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task> rufe = (k, a, v, akt, ct) =>
        {
            Interlocked.Increment(ref Uebergaben);
            return Ausfuehrer.BeauftrageAsync(k, a, v, akt, ct);
        };
        if (umRufe is not null) rufe = umRufe(rufe);

        Manager = new ProzessManager(Log,
            new Dictionary<string, ProzessRegeln> { [nameof(BildAufbereitung)] = new BildAufbereitung(limit, mitKompensation).Regeln },
            DispatchAsync, markingStore: mitCursor ? new MarkingImSpeicher() : null, markingSchreibIntervall: 1,
            rufe: rufe, jetzt: _ => Task.FromResult(Log.Jetzt));
    }

    /// <summary>Das Ziel-Aggregat: Command → Event (+ KommandoVerarbeitet) bzw. Ablehnungs-Marke, CausationId = Vorgang.</summary>
    private async Task DispatchAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        Gesendet.Add(cmd);
        var stream = Log.Stream(cmd.AggregateId);
        if (stream.Any(e => e.CausationId == vorgang.ToString())) return;   // Framework-Inbox
        IEvent[] evs = LehneAb.Contains(cmd.GetType())
            ? new IEvent[] { new KommandoAbgelehnt(vorgang, cmd.GetType().Name + " abgelehnt") }
            : cmd switch
            {
                MeldeFertig f => new IEvent[] { new Fertig(f.Vorschau, f.Kamera), new KommandoVerarbeitet(vorgang) },
                MeldeUnbrauchbar u => new IEvent[] { new Unbrauchbar(u.Grund), new KommandoVerarbeitet(vorgang) },
                RaeumeVorschauAuf r => new IEvent[] { new VorschauAufgeraeumt(r.Vorschau), new KommandoVerarbeitet(vorgang) },
                _ => throw new NotSupportedException(cmd.GetType().Name),
            };
        await Log.AppendEventsAsync(cmd.AggregateId, stream.Count, evs, korrelation.ToString(), vorgang.ToString(), "Bildakte");
    }

    /// <summary>Startet den Prozess mit einem Bild.</summary>
    public async Task StarteAsync(string pfad = "bild.tiff")
    {
        await Log.AppendEventsAsync(_auslöserStream, 0, new IEvent[] { new BildEingegangen(pfad) }, Korrelation.ToString(), null, "Eingang");
        await Manager.StarteAsync(Korrelation, nameof(BildAufbereitung), _auslöserStream, 1);
    }

    /// <summary>Weckt den Manager, bis er terminal ist oder nichts mehr passiert (wartet dabei auf die Ausführungen).</summary>
    public async Task<ProzessManager.ManagerStatus> TreibeAsync(bool warteAufFunktionen = true, int max = 30)
    {
        for (var i = 0; i < max; i++)
        {
            if (warteAufFunktionen) await Ausfuehrer.WarteAufAlleAsync();
            while (Weckungen.TryDequeue(out _)) { }
            var st = await Manager.LadeStatusAsync(Korrelation);
            if (st.Beendet) return st;
            await Manager.WakeAsync(Korrelation);
        }
        return await Manager.LadeStatusAsync(Korrelation);
    }

    public IReadOnlyList<EventEnvelope> ManagerLog => Log.Stream(Korrelation);
}
