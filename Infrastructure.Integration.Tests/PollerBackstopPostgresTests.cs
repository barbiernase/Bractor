using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 (Store-Semantik, echtes Marten, KEIN Cluster) — Befund 1 des Backend-Audits.
///
/// Der Poll-Backstop ist die letzte Rettung für ein verlorenes Signal vor Stille. Sein Cursor
/// (High-Water-Mark) darf NUR vorrücken, wenn die Verarbeitung des geweckten Streams BESTÄTIGT
/// aufgeholt zurückkommt. Rückt er über einen NICHT verarbeiteten Stream hinaus, weckt auf einem
/// terminalen Stream (keine neuen Events mehr) nie wieder etwas → die Projektion hängt still und
/// dauerhaft zurück. Das war nur gegen echtes Marten sichtbar (globale Sequenz + HWM aus dem
/// realen Event-Log), nie im In-Memory-Store — deshalb Ebene 2.
///
/// Braucht laufendes Postgres auf localhost:5432 (docker-compose.infrastructure.yml).
/// </summary>
public class PollerBackstopPostgresTests : IClassFixture<PollerBackstopPostgresTests.Fixture>
{
    private readonly Fixture _fx;
    public PollerBackstopPostgresTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task Unbestaetigte_Weckung_darf_die_HWM_nicht_ueber_den_Stream_hinaus_schieben()
    {
        // Grace = 0: dieser Test isoliert den Poll-Cursor-Guard (Befund 1) von der Straggler-Karenz
        // (Befund 3) — ein frisch angehängtes Event ist sofort HWM-fähig, sodass die (buggy) naive
        // Advance den Stream überhaupt überspringen KÖNNTE. Genau das verhindert der Fix.
        var store = new MartenEventStore(
            _fx.Store, new NoopFactory(), NullLogger<MartenEventStore>.Instance, stragglerGrace: TimeSpan.Zero);

        // Isolation gegen früher in DIESEM Schema angehängte Events: ab der aktuellen globalen
        // HWM aufsetzen. Ebene-2-Tests laufen sequentiell (xunit.runner.json) → kein Interleave.
        var baseHwm = (await store.ReadChangedStreamsAsync(0, default)).HighWaterMark;

        // Ein terminaler Stream: ein Event, danach STILLE. Sein Signal geht verloren (kein Wake) —
        // der Poll ist die einzige verbleibende Rettung.
        var streamId = Guid.NewGuid();
        await store.AppendEventsAsync(streamId, 0, new IEvent[] { new ImagePairInspiziert() });

        // Stub-Adapter: die Verarbeitung scheitert beim ERSTEN Aufruf (transienter Fehler →
        // nicht caught-up), gelingt danach. Er notiert, welche Streams er wirklich verarbeitet hat.
        var verarbeitet = new List<Guid>();
        var ersterAufruf = true;
        Func<Guid, CancellationToken, Task<bool>> wake = (sid, ct) =>
        {
            if (ersterAufruf)
            {
                ersterAufruf = false;
                return Task.FromResult(false); // Verarbeitung gescheitert → unbestätigt.
            }
            verarbeitet.Add(sid);
            return Task.FromResult(true);
        };

        var poller = new Poller(store, wake, startHighWater: baseHwm);

        // Poll 1: weckt den Stream, aber die Verarbeitung scheitert (unbestätigt).
        await poller.PollOnceAsync();
        verarbeitet.Should().BeEmpty("die erste Verarbeitung ist gescheitert");

        // Poll 2: KEIN neues Event. Der Fix verlangt, dass die HWM in Poll 1 NICHT über den
        // unverarbeiteten Stream hinaus vorgerückt ist → der Stream wird erneut geweckt und jetzt
        // verarbeitet. Bug (Befund 1): die HWM ist bereits vorbei → der Stream wird nie wieder
        // geweckt → 'verarbeitet' bleibt leer und die Projektion hängt für immer zurück.
        await poller.PollOnceAsync();

        verarbeitet.Should().ContainSingle().Which.Should().Be(streamId,
            "ein unbestätigt gebliebener Stream muss beim nächsten Poll erneut geweckt werden");
    }

    /// <summary>Der Store wird im Test nur für Append/ReadChangedStreams genutzt — kein Replay.</summary>
    private sealed class NoopFactory : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state)
            => throw new NotSupportedException("Test nutzt nur Append/ReadChangedStreams.");
    }

    /// <summary>
    /// Eigenes Marten-Schema für diesen Test (Isolation gegen Produktions-/andere Testdaten).
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        public IDocumentStore Store { get; }

        public Fixture()
        {
            Store = DocumentStore.For(opts =>
            {
                opts.Connection(
                    "Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres");
                opts.DatabaseSchemaName = "audit_befund1_it";
                opts.Events.DatabaseSchemaName = "audit_befund1_it";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            });
        }

        public void Dispose() => Store.Dispose();
    }
}
