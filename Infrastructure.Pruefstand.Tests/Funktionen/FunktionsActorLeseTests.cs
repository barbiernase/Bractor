using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Projections;
using Infrastructure.Pruefstand.BatchKonsum;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Der Funktions-Actor liest eine Seite für alle ihre Eingänge (G-18, docs/13): mit einem Slot je Partition las er nach jedem fertigen Eingang eine
/// volle Seite neu, um genau einen zu starten — eine Partition schaffte so unter Last ≈ 100 Eingänge/s, und nach dem harten Neustart holte die
/// heißeste ihren Rückstand nicht in der Frist auf. Lokales <see cref="ActorSystem"/> ohne Cluster, store-freies Log mit Lese-Zähler
/// (<see cref="PartitionsLog"/>), eine gleichbleibende Funktion ohne Ausgabe.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsActorLeseTests
{
    private const int Seite = 50;

    /// <summary>Der Stand in einem Wörterbuch (eine Aktivierung, keine Konflikte).</summary>
    private sealed class SpeicherStand : IFunktionsStand
    {
        private readonly ConcurrentDictionary<string, PartitionsCheckpoint> _staende = new();
        public long Position(string standId) => _staende.TryGetValue(PartitionsCheckpoint.MakeId(standId, 0), out var s) ? s.Position : 0;

        public Task<PartitionsCheckpoint> LadeAsync(string standId, int partition, CancellationToken ct) =>
            Task.FromResult(_staende.TryGetValue(PartitionsCheckpoint.MakeId(standId, partition), out var s)
                ? new PartitionsCheckpoint { Id = s.Id, ProjektionsId = s.ProjektionsId, Partition = s.Partition, Position = s.Position, Version = s.Version }
                : PartitionsCheckpoint.Neu(standId, partition));

        public Task<int> SchreibeAsync(PartitionsCheckpoint stand, CancellationToken ct)
        {
            var neu = new PartitionsCheckpoint
                { Id = stand.Id, ProjektionsId = stand.ProjektionsId, Partition = stand.Partition, Position = stand.Position, Version = stand.Version + 1 };
            _staende[stand.Id] = neu;
            return Task.FromResult(neu.Version);
        }

        public Task<FunktionsZustandsDokument?> LadeZustandAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<(int? Stand, int Zustand)> SichereAsync(PartitionsCheckpoint? stand, FunktionsZustandsDokument zustand, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class KeineAblage : IAusgabeAblage
    {
        public Task<Festgehalten> LiesAsync(FunktionsEingabe eingabe, CancellationToken ct) => throw new NotSupportedException("gleichbleibend: nichts festhalten");
        public Task<bool> HalteFestAsync(FunktionsEingabe eingabe, int ab, IReadOnlyList<IAusgabe> ausgaben, bool abgeschlossen, CancellationToken ct) =>
            throw new NotSupportedException("gleichbleibend: nichts festhalten");
    }

    private static async IAsyncEnumerable<(int, IAusgabe)> KeineSeite([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    [Fact]
    public async Task Mit_einem_Slot_liest_der_Actor_eine_Seite_fuer_alle_ihre_Eingaenge()
    {
        var log = new PartitionsLog();
        var streams = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
        const int n = 200;
        for (var i = 0; i < n; i++) log.Haenge(streams[i % streams.Length], i / streams.Length + 1);

        var gerechnet = new ConcurrentDictionary<(Guid, int), int>();
        var baustein = new FunktionsBaustein(typeof(object), "Probe", Draussen: false, Gleichbleibend: true,
            new[] { new FunktionsEingang(typeof(Testereignis), false, false, false, Array.Empty<Type>(), Array.Empty<Type>()) },
            Array.Empty<FunktionsUmsetzung>(), Array.Empty<Type>(),
            (_, _, h, _, _) => { gerechnet.AddOrUpdate((h.Stream, h.Version), 1, (_, x) => x + 1); return Ausgaben.Leer; },
            (_, _, _, ct) => KeineSeite(ct));
        var arbeit = new FunktionsArbeit(baustein, () => new object(), new KeineAblage(),
            (_, _, _, _) => throw new InvalidOperationException("die Probe sendet nichts"), partitionen: 1);
        var staende = new SpeicherStand();
        var actor = new FunktionsActor(baustein, arbeit, log, staende, new Konsum(1, Seite), slots: 1, wiederholungen: 0);

        var system = new ActorSystem();
        try
        {
            var pid = system.Root.Spawn(Props.FromProducer(() => actor));
            await system.Root.RequestAsync<WakeAck>(pid, new Wake(), TimeSpan.FromSeconds(5));

            var frist = DateTime.UtcNow + TimeSpan.FromSeconds(20);
            while (gerechnet.Count < n && DateTime.UtcNow < frist) await Task.Delay(20);
            await system.Root.StopAsync(pid);   // beim Stopp schreibt der Actor den Stand zuletzt

            gerechnet.Should().HaveCount(n, "jeder Eingang wird gerechnet");
            gerechnet.Values.Should().OnlyContain(x => x == 1, "jeder genau einmal");
            staende.Position(baustein.StandId).Should().Be(n);
            log.Lesungen.Should().BeLessThanOrEqualTo(n / Seite + 3,
                "eine Seite trägt alle ihre Eingänge — neu gelesen wird erst, wenn ihr Vorrat erschöpft ist (vorher eine Seite je Eingang)");
        }
        finally
        {
            await system.ShutdownAsync();
        }
    }
}
