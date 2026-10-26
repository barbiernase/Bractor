using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Posten;
using Proto.Cluster;
using static Pruefdomaene.Integration.Tests.Pruefstand;
using PostenState = Pruefdomaene.Posten.Posten;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Garantien der Schreibseite (docs/11 §11.2, §11.3) unter Störung, im echten Cluster gegen echtes Marten: doppelte Zustellung desselben
/// Commands, eine alte CommandId jenseits des Inbox-Caches, eine Wiederholung nach dem Neustart, ein Duplikat mitten im Group-Commit, der
/// Horizont der Inbox, ein Versionskonflikt durch einen zweiten Schreiber, ein mehrdeutiger Commit (committet, Quittung verloren).
/// Beweiskraft: der Zustand aus dem Log (Voll-Fold), die Events im Stream und die Inbox-Tabelle.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class SchreibGarantieTests
{
    private static async Task<Guid> PostenAsync(IHost host, int bestand = 100)
    {
        var p = Guid.NewGuid();
        (await FrageAsync(host, new EroeffnePosten(new(p), bestand), Guid.NewGuid()))!.Success.Should().BeTrue();
        return p;
    }

    private static async Task<int> ReserviertImLog(IHost host, Guid p)
        => (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenReserviert);

    [Fact]
    public async Task Doppelt_und_gleichzeitig_zugestellter_Command_wirkt_genau_einmal()
    {
        using var host = Cluster("garantie-doppelt");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            var id = Guid.NewGuid();
            var erste = await FrageAsync(host, new ReservierePosten(new(p), 5), id);
            var wiederholt = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => FrageAsync(host, new ReservierePosten(new(p), 5), id)));

            erste!.Success.Should().BeTrue();
            wiederholt.Should().OnlyContain(r => r != null && r.Success, "eine Wiederholung bekommt idempotent dasselbe Ergebnis");
            (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(5);
            (await ReserviertImLog(host, p)).Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_alte_CommandId_jenseits_der_Inbox_Grenze_wirkt_nicht_erneut()
    {
        // G-2: früher vergaß die Inbox (im Snapshot, gedeckelt) die ältesten CommandIds. Jetzt ist der Actor-Cache klein (3), die Wahrheit
        //   die Inbox-Tabelle: die alte CommandId fällt aus dem Cache und scheitert beim Append am Schlüssel.
        using var host = Cluster("garantie-inbox", inboxCache: 3);
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            var alt = Guid.NewGuid();
            (await FrageAsync(host, new ReservierePosten(new(p), 1), alt))!.Success.Should().BeTrue();
            for (var i = 0; i < 3; i++) (await FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid()))!.Success.Should().BeTrue();

            await FrageAsync(host, new ReservierePosten(new(p), 1), alt);   // ein Emittent gibt die alte CommandId erneut aus

            (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(4, "die alte CommandId hat schon gewirkt");
            (await ReserviertImLog(host, p)).Should().Be(4);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Wiederholung_nach_dem_Neustart_wirkt_nicht_erneut_auch_nicht_als_Ablehnung()
    {
        // Nach dem Neustart kennt kein Actor-Cache etwas, und der Stand ist weitergegangen: eine Wiederholung der ersten Reservierung würde jetzt
        //   abgelehnt (Bestand erschöpft), eine Wiederholung der Ablehnung würde jetzt wirken (Bestand zugebucht). Beide bekommen den Ausgang
        //   des ersten Mals, keine schreibt etwas.
        var schema = "pruef_inbox_neustart";
        Guid p, gewirkt = Guid.NewGuid(), abgelehnt = Guid.NewGuid();
        using (var host = Cluster("garantie-neustart-1", schema: schema))
        {
            await host.StartAsync();
            try
            {
                p = await PostenAsync(host, bestand: 5);
                (await FrageAsync(host, new ReservierePosten(new(p), 5), gewirkt))!.Success.Should().BeTrue();
                var erst = await FrageAsync(host, new ReservierePosten(new(p), 3), abgelehnt);
                erst!.Success.Should().BeFalse();
                erst.Abgelehnt.Should().BeTrue();
            }
            finally { await host.StopAsync(); }
        }

        using (var host = Cluster("garantie-neustart-2", schema: schema))
        {
            await host.StartAsync();
            try
            {
                (await FrageAsync(host, new BuchePostenZu(new(p), 10), Guid.NewGuid()))!.Success.Should().BeTrue();   // jetzt reichte der Bestand
                var store = host.Services.GetRequiredService<IEventStoreRepository>();
                var vorher = (await store.ReadStreamAsync(p, 0, default)).Count;

                var w1 = await FrageAsync(host, new ReservierePosten(new(p), 5), gewirkt);
                var w2 = await FrageAsync(host, new ReservierePosten(new(p), 3), abgelehnt);

                w1!.Success.Should().BeTrue("die erste Reservierung hat gewirkt");
                w2!.Success.Should().BeFalse("die zweite war abgelehnt — auch wenn sie jetzt wirken könnte");
                w2.Abgelehnt.Should().BeTrue();
                (await store.ReadStreamAsync(p, 0, default)).Count.Should().Be(vorher, "eine Wiederholung schreibt nichts");
                (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(5);
            }
            finally { await host.StopAsync(); }
        }
    }

    [Fact]
    public async Task Ein_Duplikat_mitten_im_Group_Commit_faellt_allein()
    {
        // Ein Cache von 1: die Wiederholung kennt der Actor nicht mehr, sie scheitert im Group-Commit am Schlüssel der Inbox — der ganze
        //   Batch rollt zurück, der Appender schreibt einzeln: die gleichzeitigen frischen Commands anderer Posten wirken, die Wiederholung nicht.
        using var host = Cluster("garantie-batch", inboxCache: 1);
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            var alt = Guid.NewGuid();
            (await FrageAsync(host, new ReservierePosten(new(p), 1), alt))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid()))!.Success.Should().BeTrue();   // verdrängt alt aus dem Cache
            var andere = new List<Guid>();
            for (var i = 0; i < 20; i++) andere.Add(await PostenAsync(host));

            var frisch = andere.Select(a => FrageAsync(host, new ReservierePosten(new(a), 2), Guid.NewGuid()));
            var ergebnisse = await Task.WhenAll(frisch.Append(FrageAsync(host, new ReservierePosten(new(p), 1), alt)));

            ergebnisse.Should().OnlyContain(r => r != null && r.Success);
            (await ReserviertImLog(host, p)).Should().Be(2, "die Wiederholung hat nicht gewirkt");
            foreach (var a in andere) (await ReserviertImLog(host, a)).Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Die_Inbox_vergisst_erst_jenseits_des_Horizonts()
    {
        using var host = Cluster("garantie-horizont", schema: "pruef_inbox_horizont");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            var id = Guid.NewGuid();
            (await FrageAsync(host, new ReservierePosten(new(p), 1), id))!.Success.Should().BeTrue();
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var marten = host.Services.GetRequiredService<Marten.IDocumentStore>();

            await Infrastructure.Persistence.InboxAufraeumer.RaeumeAsync(marten, TimeSpan.FromHours(1), default);
            (await store.AusgangAsync(id, default)).Should().Be(KommandoAusgang.Gewirkt, "innerhalb des Horizonts bleibt die Quittung");

            await Task.Delay(50);
            (await Infrastructure.Persistence.InboxAufraeumer.RaeumeAsync(marten, TimeSpan.FromMilliseconds(10), default)).Should().BeGreaterThan(0);
            (await store.AusgangAsync(id, default)).Should().BeNull("jenseits des Horizonts ist sie vergessen");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Wie viele Transaktionen die Events eines Streams geschrieben haben (xmin) — weniger als Commands heißt: gebündelt.</summary>
    private static async Task<int> TransaktionenImStream(IHost host, Guid stream)
    {
        var marten = host.Services.GetRequiredService<Marten.IDocumentStore>();
        await using var q = marten.QuerySession();
        var r = await q.QueryAsync<int>(
            $"select count(distinct xmin::text)::int from {marten.Options.Events.DatabaseSchemaName}.mt_events where stream_id = ?", CancellationToken.None, stream);
        return r[0];
    }

    [Fact]
    public async Task Ein_heisser_Posten_buendelt_und_jeder_Command_wirkt_genau_einmal()
    {
        // Bündel (docs/03 §3.1): während ein Commit läuft, sammelt der Actor; danach entscheidet er die gesammelten nacheinander und hängt sie
        //   in einem Append an. Gleichzeitig kommen Wiederholungen derselben CommandIds (vor, in und nach ihrem Bündel).
        using var host = Cluster("garantie-buendel");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host, bestand: 10_000);
            var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();
            var sends = ids.Select(id => FrageAsync(host, new ReservierePosten(new(p), 1), id))
                .Concat(ids.Where((_, i) => i % 4 == 0).Select(id => FrageAsync(host, new ReservierePosten(new(p), 1), id)));
            var ergebnisse = await Task.WhenAll(sends);

            ergebnisse.Should().OnlyContain(r => r != null && r.Success);
            (await ReserviertImLog(host, p)).Should().Be(200, "jede CommandId genau einmal");
            (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(200);
            (await TransaktionenImStream(host, p)).Should().BeLessThan(150, "gleichzeitige Commands an ein Aggregat teilen sich Commits");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Im_Buendel_entscheidet_jeder_Command_auf_dem_Stand_nach_dem_vorigen()
    {
        // Der Bestand reicht für die Hälfte: genau die Hälfte wirkt, der Rest wird abgelehnt — auch wenn alle im selben Bündel liegen.
        using var host = Cluster("garantie-buendel-stand");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host, bestand: 40);
            var ergebnisse = await Task.WhenAll(Enumerable.Range(0, 80).Select(_ => FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid())));

            ergebnisse.Count(r => r!.Success).Should().Be(40);
            ergebnisse.Count(r => !r!.Success && r.Abgelehnt).Should().Be(40);
            (await ReserviertImLog(host, p)).Should().Be(40);
            var zustand = await Zustand<PostenState>(host.Services, p);
            zustand.Reserviert.Should().Be(40);
            zustand.Verfuegbar.Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_zweiter_Schreiber_mitten_im_Ansturm_laesst_kein_Buendel_doppelt_oder_gar_nicht_wirken()
    {
        // Ein Versionskonflikt lässt das laufende Bündel scheitern: der Actor lädt frisch und behandelt jeden Command einzeln.
        using var host = Cluster("garantie-buendel-konflikt");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host, bestand: 1_000);
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var ansturm = Enumerable.Range(0, 100).Select(_ => FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid())).ToList();
            var version = (await store.ReadStreamAsync(p, 0, default)).Max(e => e.AggregateVersion);
            try { await store.AppendEventsAsync(p, version, new IEvent[] { new PostenZugebucht(10) }); }
            catch (ConcurrencyException) { /* das Bündel war schneller — dann schreibt der zweite Schreiber eben nicht */ }
            var ergebnisse = await Task.WhenAll(ansturm);

            var gewirkt = ergebnisse.Count(r => r != null && r.Success);
            (await ReserviertImLog(host, p)).Should().Be(gewirkt, "was als Erfolg quittiert wurde, steht genau einmal im Log");
            (await Zustand<PostenState>(host.Services, p)).Reserviert.Should().Be(gewirkt);
            // Wer keinen Erfolg bekam (der Einzel-Pfad scheitert nach einem Konflikt höchstens einmal), wirkt mit derselben CommandId beim
            //   zweiten Versuch — und keiner doppelt.
            gewirkt.Should().BeGreaterThan(90);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Nach_einem_Versionskonflikt_durch_einen_zweiten_Schreiber_heilt_der_Actor_ohne_Neustart()
    {
        using var host = Cluster("garantie-konflikt");
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            (await FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid()))!.Success.Should().BeTrue();   // der Actor ist aktiv, Stand v4

            // Ein zweiter Schreiber (zweite Aktivierung, anderer Knoten) hängt an denselben Stream an.
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var version = (await store.ReadStreamAsync(p, 0, default)).Max(e => e.AggregateVersion);
            await store.AppendEventsAsync(p, version, new IEvent[] { new PostenZugebucht(10) });

            await FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid());          // trifft den veralteten Stand — darf scheitern
            var danach = await FrageAsync(host, new ReservierePosten(new(p), 1), Guid.NewGuid());

            danach!.Success.Should().BeTrue("der Actor rehydriert nach dem Konflikt frisch und entscheidet auf dem aktuellen Stand");
            (await Zustand<PostenState>(host.Services, p)).Bestand.Should().Be(110);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_mehrdeutiger_Commit_wird_als_Erfolg_quittiert_und_wirkt_genau_einmal()
    {
        var störung = new MehrdeutigerCommit();
        using var host = Cluster("garantie-mehrdeutig", dienste: s => Dekoriere<IEventStoreRepository>(s, (_, innen) => störung.Um(innen)));
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);
            störung.Scharf = p;
            var id = Guid.NewGuid();

            var ergebnis = await FrageAsync(host, new ReservierePosten(new(p), 5), id);

            störung.Ausgeloest.Should().BeTrue("der Commit ist durch, nur die Quittung ging verloren");
            ergebnis!.Success.Should().BeTrue("was im Log steht, hat gewirkt (P10) — kein falsches Negativ");
            (await FrageAsync(host, new ReservierePosten(new(p), 5), id))!.Success.Should().BeTrue();
            (await ReserviertImLog(host, p)).Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Command_an_eine_fremde_Identitaet_wird_abgelehnt_bevor_etwas_geladen_ist()
    {
        // Die Id des Aggregat-Actors ist seine Cluster-Identität (Plan §1.7, Phase 1). Erreicht ihn ein Command mit einem anderen Ziel (falsch
        // geroutet), lehnt er ab, ohne einen Stream zu lesen — früher hätte der erste Envelope die Aktivierung initialisiert und den fremden
        // Stream unter dieser Identität geladen (zwei Schreiber für einen Stream).
        var lesen = new StreamLeser();
        using var host = Cluster("garantie-fremde-id", dienste: s => Dekoriere<IEventStoreRepository>(s, (_, innen) => lesen.Um(innen)));
        await host.StartAsync();
        try
        {
            var p = await PostenAsync(host);                    // das Ziel des Commands, mit Geschichte
            var fremd = Guid.NewGuid();                          // die Identität, die ihn bekommt
            lesen.Beobachte(p, fremd);

            var cluster = host.Services.GetRequiredService<Proto.ActorSystem>().Cluster();
            var huelle = new CommandEnvelope
            {
                CommandId = Guid.NewGuid(), AggregateType = "Posten", Modus = new CommandModus.Emittiert(), Payload = new ReservierePosten(new(p), 5),
            };
            using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ergebnis = await cluster.RequestAsync<CommandResult>(ClusterIdentity.Create(fremd.ToString(), "Posten"), huelle, frist.Token);

            ergebnis.Success.Should().BeFalse("die Identität des Actors ist nicht das Ziel des Commands");
            ergebnis.ErrorMessage.Should().Contain(fremd.ToString());
            lesen.Gelesen.Should().BeEmpty("abgelehnt, bevor die Aktivierung etwas lädt");
            (await ReserviertImLog(host, p)).Should().Be(0, "nichts gewirkt");
            (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(fremd, 0, default)).Should().BeEmpty("nichts geschrieben");

            (await FrageAsync(host, new ReservierePosten(new(p), 5), Guid.NewGuid()))!.Success.Should().BeTrue("an die richtige Identität wirkt er");
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Störungs-Seam: merkt sich jedes Lesen eines beobachteten Streams (ab dem Scharfschalten).</summary>
    private sealed class StreamLeser
    {
        private HashSet<Guid> _beobachtet = new();
        public System.Collections.Concurrent.ConcurrentQueue<Guid> Gelesen { get; } = new();

        public void Beobachte(params Guid[] streams) => _beobachtet = streams.ToHashSet();

        public IEventStoreRepository Um(IEventStoreRepository innen) => new Laeufer(this, innen);

        private sealed class Laeufer(StreamLeser s, IEventStoreRepository innen) : IEventStoreRepository
        {
            private void Merke(Guid stream) { if (s._beobachtet.Contains(stream)) s.Gelesen.Enqueue(stream); }

            public Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null,
                string? causationId = null, string? aggregateType = null, string? akteur = null, IReadOnlyList<Beilage>? beilagen = null)
                => innen.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType, akteur, beilagen);
            public Task AppendTeileAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<AppendTeil> teile, string? aggregateType)
                => innen.AppendTeileAsync(aggregateId, expectedVersion, teile, aggregateType);
            public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
            {
                Merke(streamId);
                return innen.ReadStreamAsync(streamId, fromVersion, ct);
            }
            public Task<EventEnvelope?> ReadEventAsync(Guid streamId, int version, CancellationToken ct)
            {
                Merke(streamId);
                return innen.ReadEventAsync(streamId, version, ct);
            }
            public Task<KorrelierteEvents> ReadKorreliertAsync(Guid streamId, int fromVersion, string korrelation, Guid beginn, CancellationToken ct)
            {
                Merke(streamId);
                return innen.ReadKorreliertAsync(streamId, fromVersion, korrelation, beginn, ct);
            }
            public Task<StreamChanges> ReadChangedStreamsAsync(long after, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct) => innen.ReadChangedStreamsAsync(after, typen, limit, ct);
            public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
                => innen.ReadPartitionAsync(nach, partition, partitionen, typen, ausser, limit, ct);
            public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
            {
                Merke(id);
                return innen.LoadStateAsync<TState>(id);
            }
            public Task<KommandoAusgang?> AusgangAsync(Guid commandId, CancellationToken ct) => innen.AusgangAsync(commandId, ct);
            public Task<IReadOnlyCollection<Guid>> StreamsVorhandenAsync(IReadOnlyCollection<Guid> streams, CancellationToken ct) => innen.StreamsVorhandenAsync(streams, ct);
        }
    }

    /// <summary>Störungs-Seam: der nächste Append auf den scharfen Stream committet — dann geht die Quittung verloren (Ausnahme).</summary>
    private sealed class MehrdeutigerCommit
    {
        public Guid? Scharf { get; set; }
        public bool Ausgeloest { get; private set; }

        public IEventStoreRepository Um(IEventStoreRepository innen) => new Laeufer(this, innen);

        private sealed class Laeufer(MehrdeutigerCommit s, IEventStoreRepository innen) : IEventStoreRepository
        {
            public async Task AppendEventsAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events, string? correlationId = null,
                string? causationId = null, string? aggregateType = null, string? akteur = null, IReadOnlyList<Beilage>? beilagen = null)
            {
                await innen.AppendEventsAsync(aggregateId, expectedVersion, events, correlationId, causationId, aggregateType, akteur, beilagen);
                Stoere(aggregateId);
            }

            public async Task AppendTeileAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<AppendTeil> teile, string? aggregateType)
            {
                await innen.AppendTeileAsync(aggregateId, expectedVersion, teile, aggregateType);
                Stoere(aggregateId);
            }

            private void Stoere(Guid aggregateId)
            {
                if (s.Scharf == aggregateId && !s.Ausgeloest)
                {
                    s.Ausgeloest = true;
                    throw new TimeoutException("Verbindung nach dem Commit verloren — Quittung fehlt");
                }
            }

            public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct) => innen.ReadStreamAsync(streamId, fromVersion, ct);
            public Task<StreamChanges> ReadChangedStreamsAsync(long after, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct) => innen.ReadChangedStreamsAsync(after, typen, limit, ct);
            public Task<PartitionsSeite> ReadPartitionAsync(long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit, CancellationToken ct)
                => innen.ReadPartitionAsync(nach, partition, partitionen, typen, ausser, limit, ct);
            public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => innen.LoadStateAsync<TState>(id);
            public Task<KommandoAusgang?> AusgangAsync(Guid commandId, CancellationToken ct) => innen.AusgangAsync(commandId, ct);
        }
    }
}
