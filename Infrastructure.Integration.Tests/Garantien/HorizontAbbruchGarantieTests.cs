using System.Collections.Concurrent;
using System.Text;
using Abstractions;
using Domain.ImagePair;
using Domain.Projections.ImagePair;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit.Abstractions;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Integration.Tests.Garantien;

/// <summary>
/// G-11 (docs/13), die Lage des roten Störungslaufs: die übersprungenen Events entstanden, während am selben Postgres ein ANDERER Host hart
/// beendet und neu gestartet wurde. Dieser Test erzeugt die Last von <see cref="HorizontStressGarantieTests"/> (Group-Commit mit parallelem
/// Drain, Bündel, langsame Batch-Leser mit Neustarts) und stört sie zweifach:
/// <list type="bullet">
///   <item><b>Ein zweiter „Prozess“</b> in einer eigenen Datenbank (wie Beispiel- und Prüfdomäne): eigener Store, Schema-Anlage beim Start,
///     Group-Commit, Bündel, Transaktionen, die hängen und nie abgewartet werden (weggeworfene Verbindungen). Alle 1–4 s endet er hart —
///     <c>pg_terminate_backend</c> auf alle seine Backends, mitten in den Transaktionen — und startet neu. Transaktionsnummern gelten im
///     ganzen Cluster: seine Transaktionen halten den Horizont der Beispiel-Seite an und geben ihn beim Abbruch frei.</item>
///   <item><b>Abbrüche auf der Beispiel-Seite selbst:</b> Schreib-Transaktionen, die <c>nextval</c> schon gezogen haben (Append, danach in derselben
///     Transaktion ein <c>pg_sleep</c>), und beliebige Schreiber mitten in ihrer Transaktion (auch im Group-Commit).</item>
/// </list>
/// Das Soll ist das Log: je Stream so viele Historien-Einträge, wie Events in <c>mt_events</c> stehen (ein Schreiber weiß nach einem Abbruch
/// nicht sicher, ob sein Commit durchkam). Fehlt etwas, nennt die Meldung je fehlendem Event seine Transaktion und Commit-Zeit und den Commit, der
/// die Partition darüber schob — so ist ablesbar, ob die Partition über eine noch unsichtbare Transaktion rückte (Horizont) oder über ein
/// sichtbares Event (Batch-Leser).
/// </summary>
[Trait("Kategorie", "Garantie")]
[Trait("Kategorie", "Last")]   // Minuten unter Last: in der Abnahme (scripts/tests.sh voll), nicht in jedem Teilschritt
public class HorizontAbbruchGarantieTests : IClassFixture<HorizontAbbruchGarantieTests.Fixture>
{
    private readonly Fixture _fx;
    private readonly ITestOutputHelper _aus;
    public HorizontAbbruchGarantieTests(Fixture fx, ITestOutputHelper aus) { _fx = fx; _aus = aus; }

    private const int Schreiber = 16, Langsame = 2, StreamsJeSchreiber = 25, Partitionen = 8;
    private static readonly TimeSpan Dauer = TimeSpan.FromSeconds(45);
    private const string AppSchreiber = "g11-schreiber", AppZweiter = "g11-zweiter", AppAbbrecher = "g11-abbrecher";

    /// <summary>Zähler der Störungen (für die Meldung und die Prüfung, dass der Test wirklich stört).</summary>
    private sealed class Stoerungen
    {
        public int NachNextval, MittenDrin, SchreibFehler, ZweiterNeustarts, ZweiterBackendsMitTransaktion, ZweiterBackends, Weggeworfen;
        public override string ToString() =>
            $"Beispiel-Seite: {NachNextval} Abbrüche nach nextval, {MittenDrin} mitten in der Transaktion, {SchreibFehler} Schreibfehler; "
            + $"zweiter Prozess: {ZweiterNeustarts} harte Neustarts, {ZweiterBackends} Backends beendet ({ZweiterBackendsMitTransaktion} mit "
            + $"offener Schreib-Transaktion), {Weggeworfen} weggeworfene Transaktionen";
    }

    [Fact]
    public async Task Unter_harten_Abbruechen_am_selben_Postgres_wirkt_jedes_Event_genau_einmal()
    {
        var schema = Fixture.Schema;
        var es = new MartenEventStore(_fx.Store, new KeineFabrik(), NullLogger<MartenEventStore>.Instance);
        var esSchreib = new MartenEventStore(_fx.SchreibStore, new KeineFabrik(), NullLogger<MartenEventStore>.Instance);
        await using var appender = new BatchingEventAppender(esSchreib,
            new MartenEventBatchWriter(_fx.SchreibStore, NullLogger<MartenEventBatchWriter>.Instance),
            NullLogger<BatchingEventAppender>.Instance, maxBatch: 256, drainParallelism: 4);
        var konsum = new Konsum(Partitionen, 200);
        const string proj = "it-horizont-abbruch";
        await BatchHilfe.StarteAmKopfAsync(_fx.Store, proj, konsum);

        var st = new Stoerungen();
        using var schreibStopp = new CancellationTokenSource(Dauer);
        using var leseStopp = new CancellationTokenSource();

        // ── Beispiel-Seite: Schreiber (Group-Commit, Bündel) und langsame Schreiber (pg_sleep nach den Events, also nach nextval) ──
        var schreiber = Enumerable.Range(0, Schreiber + Langsame).Select(w => Task.Run(async () =>
        {
            var zufall = new Random(w);
            var streams = Enumerable.Range(0, StreamsJeSchreiber).Select(_ => Guid.NewGuid()).ToArray();
            var version = new int[streams.Length];
            while (!schreibStopp.IsCancellationRequested)
            {
                var i = zufall.Next(streams.Length);
                var n = 1 + zufall.Next(3);
                var evs = Enumerable.Range(0, n).Select(_ => (IEvent)new ImagePairInspiziert()).ToList();
                try
                {
                    if (w >= Schreiber)
                    {
                        // Wie ein Commit, der hinter seinen Events hängt: Stream-Zeile, Events mit nextval, dann schläft die Transaktion.
                        await using var s = _fx.SchreibStore.LightweightSession();
                        if (version[i] == 0) s.Events.StartStream(streams[i], evs.Cast<object>().ToArray());
                        else s.Events.Append(streams[i], version[i] + n, evs.Cast<object>().ToArray());
                        s.QueueSqlCommand("select pg_sleep(?)", zufall.Next(20, 200) / 1000.0);
                        await s.SaveChangesAsync();
                    }
                    else if (w % 4 == 0 && version[i] > 0)
                        await esSchreib.AppendTeileAsync(streams[i], version[i], [
                            new AppendTeil(evs.Take(1).ToList(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), null, null),
                            new AppendTeil(evs.Skip(1).ToList(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), null, null)], "ImagePair");
                    else
                        await appender.AppendEventsAsync(streams[i], version[i], evs, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "ImagePair");
                    version[i] += n;
                }
                catch (Exception)
                {
                    // Abgebrochen oder mehrdeutig: was im Log steht, gilt.
                    Interlocked.Increment(ref st.SchreibFehler);
                    version[i] = await VersionImLogAsync(streams[i]);
                }
                await Task.Delay(zufall.Next(2, 8));
            }
        })).ToList();

        // ── Abbrecher der Beispiel-Seite: nach nextval (schlafende Commits) oder ein beliebiger Schreiber mitten in seiner Transaktion ──
        var abbrecher = Task.Run(async () =>
        {
            var zufall = new Random(4711);
            while (!schreibStopp.IsCancellationRequested)
            {
                try { await Task.Delay(zufall.Next(300, 1500), schreibStopp.Token); } catch (OperationCanceledException) { break; }
                var nachNextval = zufall.Next(2) == 0;
                var bedingung = nachNextval
                    ? $"application_name = '{AppSchreiber}' and state = 'active' and query like '%pg_sleep%' and backend_xid is not null"
                    : $"application_name = '{AppSchreiber}' and backend_xid is not null";
                var n = await BeendeAsync(bedingung, nachNextval ? 2 : 1);
                if (nachNextval) Interlocked.Add(ref st.NachNextval, n);
                else Interlocked.Add(ref st.MittenDrin, n);
            }
        });

        // ── Der zweite „Prozess“: eigene Datenbank, startet, schreibt, endet hart, startet neu ──
        using var zweiterStopp = new CancellationTokenSource();
        var zweiter = Task.Run(() => ZweiterProzessAsync(st, zweiterStopp.Token));

        // ── Leser: je Partition ein Batch-Leser mit langsamen Stellen und Neustarts; jeder Commit wird mitgeschrieben ──
        var commits = new ConcurrentQueue<Commit>();
        var leser = Enumerable.Range(0, Partitionen).Select(p => Task.Run(async () =>
        {
            var zufall = new Random(1000 + p);
            BatchLeser Neu()
            {
                // Je Aktivierung ein eigener Co-Commit-Store (er puffert die Effekte eines Batches) — wie im Host.
                var store = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
                return new BatchLeser(es, new Mitschreiber(store, p, commits), proj, p, konsum,
                    BatchLeser.TypenAus(ImagePairHistorieProjektion.SubscribedTypes),
                    BatchHilfe.HistorieDispatch(store, async _ => { if (zufall.Next(200) == 0) await Task.Delay(zufall.Next(50, 400)); }),
                    absturzVorCommit: async () => { if (zufall.Next(30) == 0) await Task.Delay(zufall.Next(50, 600)); });
            }
            var l = Neu();
            while (!leseStopp.IsCancellationRequested)
            {
                try { await l.LaufAsync(); } catch (Exception) when (!leseStopp.IsCancellationRequested) { l = Neu(); }
                if (zufall.Next(20) == 0) l = Neu();                       // Neustart: frische Aktivierung vom durablen Stand
                await Task.Delay(zufall.Next(30));
            }
        })).ToList();

        await Task.WhenAll(schreiber);
        await abbrecher;
        zweiterStopp.Cancel();
        await zweiter;   // endet mit einem letzten harten Ende — danach hält nichts mehr den Horizont

        // Ausklingen: bis alle Partitionen am Kopf stehen (höchstens 120 s), dann noch eine Sekunde.
        var kopf = await BatchHilfe.KopfAsync(_fx.Store);
        var bis = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < bis)
        {
            var stände = await Task.WhenAll(Enumerable.Range(0, Partitionen).Select(p => BatchHilfe.PartitionAsync(_fx.Store, proj, p)));
            if (stände.All(s => s!.Position >= kopf)) break;
            await Task.Delay(200);
        }
        await Task.Delay(1_000);
        leseStopp.Cancel();
        await Task.WhenAll(leser);

        // ── Soll aus dem Log, Ist aus der Historie ──
        var soll = new Dictionary<Guid, int>();
        await using (var c = new NpgsqlConnection(Fixture.Beispiel(AppAbbrecher)))
        {
            await c.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                $"select stream_id, count(*)::int from {schema}.mt_events where type <> 'tombstone' group by stream_id", c);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) soll[r.GetGuid(0)] = r.GetInt32(1);
        }
        var historie = new Domain.Infrastructure.ImagePair.ImagePairHistorieStore(_fx.Store);
        var abweichend = new List<(Guid Stream, int Ist, int Soll)>();
        foreach (var (stream, n) in soll)
        {
            var ist = (await historie.GetByPairIdAsync(stream))?.Eintraege.Count ?? 0;
            if (ist != n) abweichend.Add((stream, ist, n));
        }
        var stand = await Task.WhenAll(Enumerable.Range(0, Partitionen).Select(p => BatchHilfe.PartitionAsync(_fx.Store, proj, p)));
        var events = soll.Values.Sum();
        _aus.WriteLine($"{events} Events in {soll.Count} Streams, Kopf {kopf}, {commits.Count} Partitions-Commits; {st}");

        events.Should().BeGreaterThan(10_000, "der Test muss Last erzeugen");
        st.NachNextval.Should().BeGreaterThan(0, $"der Test muss Transaktionen nach nextval abbrechen ({st})");
        st.MittenDrin.Should().BeGreaterThan(0, $"der Test muss Schreiber mitten in der Transaktion abbrechen ({st})");
        st.ZweiterNeustarts.Should().BeGreaterThan(5, $"der zweite Prozess muss mehrfach hart enden ({st})");
        st.ZweiterBackendsMitTransaktion.Should().BeGreaterThan(0, $"der zweite Prozess muss mitten in Transaktionen enden ({st})");
        stand.Should().OnlyContain(s => s!.Geparkt.Count == 0, "kein Stream geparkt");
        stand.Should().OnlyContain(s => s!.Position >= kopf, "alle Partitionen haben den Kopf erreicht (sonst misst der Test nur Rückstand)");
        if (abweichend.Count > 0)
            abweichend.Should().BeEmpty($"jedes Event genau einmal ({events} Events in {soll.Count} Streams, Kopf {kopf}; {st}). "
                + $"{abweichend.Count} Streams abweichend:\n{await DiagnoseAsync(abweichend, proj, commits)}");
    }

    // ─────────────────────────────────────────────────────────────
    // Der zweite Prozess
    // ─────────────────────────────────────────────────────────────

    private async Task ZweiterProzessAsync(Stoerungen st, CancellationToken stopp)
    {
        var zufall = new Random(77);
        while (true)
        {
            var store = Fixture.ZweiterStore();
            using var lebt = new CancellationTokenSource();
            var aufgaben = new List<Task>();
            BatchingEventAppender? appender = null;
            try
            {
                // Wie ein Host beim Start: das ganze Schema anlegen bzw. prüfen.
                await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
                await store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent));
                var es = new MartenEventStore(store, new KeineFabrik(), NullLogger<MartenEventStore>.Instance);
                appender = new BatchingEventAppender(es, new MartenEventBatchWriter(store, NullLogger<MartenEventBatchWriter>.Instance),
                    NullLogger<BatchingEventAppender>.Instance, maxBatch: 256, drainParallelism: 4);
                for (var w = 0; w < 6; w++)
                {
                    var (ww, a) = (w, appender);
                    aufgaben.Add(Task.Run(() => ZweiterSchreiberAsync(ww, es, a, lebt.Token)));
                }
                aufgaben.Add(Task.Run(() => WegwerfenAsync(store, st, lebt.Token)));
            }
            catch (Exception) { /* der Start selbst kann in einen Abbruch laufen */ }

            var ende = false;
            try { await Task.Delay(zufall.Next(1_000, 4_000), stopp); } catch (OperationCanceledException) { ende = true; }

            // Hartes Ende: erst die Backends (mitten in ihren Transaktionen), dann ist der „Prozess“ weg.
            Interlocked.Add(ref st.ZweiterBackendsMitTransaktion, await BeendeAsync($"application_name = '{AppZweiter}' and backend_xid is not null", 1_000));
            Interlocked.Add(ref st.ZweiterBackends, await BeendeAsync($"application_name = '{AppZweiter}'", 1_000));
            lebt.Cancel();
            try { await Task.WhenAll(aufgaben).WaitAsync(TimeSpan.FromSeconds(10)); } catch (Exception) { }
            if (appender is not null) try { await appender.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
            store.Dispose();
            Interlocked.Increment(ref st.ZweiterNeustarts);
            if (ende || stopp.IsCancellationRequested) return;
            await Task.Delay(zufall.Next(100, 500));
        }
    }

    private static async Task ZweiterSchreiberAsync(int w, MartenEventStore es, BatchingEventAppender appender, CancellationToken ct)
    {
        var zufall = new Random(500 + w);
        var streams = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var version = new int[streams.Length];
        while (!ct.IsCancellationRequested)
        {
            var i = zufall.Next(streams.Length);
            var n = 1 + zufall.Next(3);
            var evs = Enumerable.Range(0, n).Select(_ => (IEvent)new ImagePairInspiziert()).ToList();
            try
            {
                if (w % 3 == 0 && version[i] > 0)
                    await es.AppendTeileAsync(streams[i], version[i], [
                        new AppendTeil(evs.Take(1).ToList(), null, null, null, null),
                        new AppendTeil(evs.Skip(1).ToList(), null, null, null, null)], "ImagePair");
                else
                    await appender.AppendEventsAsync(streams[i], version[i], evs, null, null, "ImagePair");
                version[i] += n;
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) return;
                streams[i] = Guid.NewGuid();   // nach einem Abbruch einfach ein neuer Stream — der zweite Prozess wird nicht geprüft
                version[i] = 0;
            }
            await Task.Delay(zufall.Next(1, 5));
        }
    }

    /// <summary>Transaktionen, die hängen und nie abgewartet werden — wie Verbindungen eines Prozesses, der gerade stirbt.</summary>
    private static async Task WegwerfenAsync(IDocumentStore store, Stoerungen st, CancellationToken ct)
    {
        var zufall = new Random();
        for (var k = 0; k < 5 && !ct.IsCancellationRequested; k++)
        {
            try { await Task.Delay(zufall.Next(100, 600), ct); } catch (OperationCanceledException) { return; }
            var s = store.LightweightSession();
            s.Events.StartStream(Guid.NewGuid(), new ImagePairInspiziert(), new ImagePairInspiziert());
            s.QueueSqlCommand("select pg_sleep(30)");
            _ = s.SaveChangesAsync().ContinueWith(t => { _ = t.Exception; s.Dispose(); }, TaskScheduler.Default);
            Interlocked.Increment(ref st.Weggeworfen);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Hilfen
    // ─────────────────────────────────────────────────────────────

    /// <summary>Beendet höchstens <paramref name="hoechstens"/> Backends nach <paramref name="bedingung"/> (zufällige Auswahl); liefert die Zahl.</summary>
    private static async Task<int> BeendeAsync(string bedingung, int hoechstens)
    {
        try
        {
            await using var c = new NpgsqlConnection(Fixture.Beispiel(AppAbbrecher));
            await c.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                $"select count(*) filter (where pg_terminate_backend(pid))::int from (select pid from pg_stat_activity where {bedingung} "
                + $"and pid <> pg_backend_pid() order by random() limit {hoechstens}) x", c);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        catch (Exception) { return 0; }
    }

    private static async Task<int> VersionImLogAsync(Guid stream)
    {
        for (var versuch = 0; ; versuch++)
        {
            try
            {
                await using var c = new NpgsqlConnection(Fixture.Beispiel(AppAbbrecher));
                await c.OpenAsync();
                await using var cmd = new NpgsqlCommand($"select coalesce(max(version), 0)::int from {Fixture.Schema}.mt_events where stream_id = $1", c);
                cmd.Parameters.Add(new NpgsqlParameter { Value = stream });
                return (int)(await cmd.ExecuteScalarAsync())!;
            }
            catch (Exception) when (versuch < 20) { await Task.Delay(50); }
        }
    }

    private sealed record Commit(int Partition, DateTimeOffset Beginn, DateTimeOffset Ende, long Position);

    /// <summary>Reicht an den Store durch und schreibt jeden gelungenen Partitions-Commit mit (Zeit vor und nach dem Commit, neue Position).</summary>
    private sealed class Mitschreiber(IProjectionTracker innen, int partition, ConcurrentQueue<Commit> log) : IProjectionTracker
    {
        public Task<PartitionsCheckpoint> LadePartitionAsync(string projektionsId, int p, CancellationToken ct) => innen.LadePartitionAsync(projektionsId, p, ct);
        public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string projektionsId, IReadOnlyCollection<Guid> streams, CancellationToken ct)
            => innen.LadeGuardsAsync(projektionsId, streams, ct);
        public void BeginneBatch() => innen.BeginneBatch();
        public void Stream(Guid streamId) => innen.Stream(streamId);
        public Task ResetAllAsync(string projektionsId, CancellationToken ct) => innen.ResetAllAsync(projektionsId, ct);

        public async Task<int> CommitAsync(PartitionsCheckpoint stand, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct)
        {
            var beginn = DateTimeOffset.UtcNow;
            var v = await innen.CommitAsync(stand, guards, ct);
            log.Enqueue(new Commit(partition, beginn, DateTimeOffset.UtcNow, stand.Position));
            return v;
        }
    }

    /// <summary>
    /// Je abweichendem Stream (höchstens fünf): Guard, Events mit Sequenz, Transaktion und Commit-Zeit, und je nicht angewandtem Event der erste
    /// Partitions-Commit, der die Position darüber schob — committete das Event erst danach, rückte die Partition über eine noch unsichtbare
    /// Transaktion (Horizont), sonst über ein sichtbares Event (Batch-Leser).
    /// </summary>
    private async Task<string> DiagnoseAsync(List<(Guid Stream, int Ist, int Soll)> abweichend, string proj, ConcurrentQueue<Commit> commits)
    {
        var sb = new StringBuilder();
        await using var c = new NpgsqlConnection(Fixture.Beispiel(AppAbbrecher));
        await c.OpenAsync();
        bool commitZeit;
        await using (var cmd = new NpgsqlCommand("select current_setting('track_commit_timestamp') = 'on'", c))
            commitZeit = (bool)(await cmd.ExecuteScalarAsync())!;
        var log = commits.ToList();
        foreach (var (stream, ist, soll) in abweichend.Take(5))
        {
            var guard = await BatchHilfe.GuardAsync(_fx.Store, proj, stream);
            var p = Partitionierung.Von(stream, Partitionen);
            sb.AppendLine($"Stream {stream} (Partition {p}): {ist} statt {soll} Einträge, Guard v{guard}");
            await using var cmd = new NpgsqlCommand(
                $"select version, seq_id, xmin::text, {(commitZeit ? "pg_xact_commit_timestamp(xmin)" : "null::timestamptz")}, timestamp "
                + $"from {Fixture.Schema}.mt_events where stream_id = $1 order by version", c);
            cmd.Parameters.Add(new NpgsqlParameter { Value = stream });
            var zeilen = new List<(long Version, long Seq, string Xid, DateTimeOffset? Commit, DateTimeOffset Zeit)>();
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    zeilen.Add((r.GetInt64(0), r.GetInt64(1), r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4)));
            foreach (var z in zeilen)
            {
                sb.Append($"  v{z.Version} seq {z.Seq} xid {z.Xid} commit {z.Commit:HH:mm:ss.ffffff} zeit {z.Zeit:HH:mm:ss.ffffff}");
                if (z.Version > guard)
                {
                    var ueber = log.Where(k => k.Partition == p && k.Position >= z.Seq).OrderBy(k => k.Ende).FirstOrDefault();
                    if (ueber is null) sb.Append(" — kein Commit der Partition reichte darüber");
                    else
                    {
                        var urteil = z.Commit is not { } ct ? "Commit-Zeit unbekannt"
                            : ct > ueber.Ende ? "Partition rückte über eine noch UNSICHTBARE Transaktion (Horizont)"
                            : ct < ueber.Beginn ? "Event war vor dem Commit sichtbar (Batch-Leser)"
                            : "Commit des Events fiel in den Partitions-Commit (unentschieden)";
                        sb.Append($" — Position {ueber.Position} committet {ueber.Beginn:HH:mm:ss.ffffff}…{ueber.Ende:HH:mm:ss.ffffff}: {urteil}");
                    }
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private sealed class KeineFabrik : IAggregateHandlerFactory
    {
        public IAggregateHandler CreateHandler(IState state) => throw new NotSupportedException();
    }

    public sealed class Fixture : IAsyncLifetime
    {
        public const string Schema = "horizont_abbruch_it";
        public const string ZweiteDatenbank = "cqrs_g11_zweiter";
        private const string Server = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

        public static string Beispiel(string app) => $"{Server};Database=cqrs_events;Application Name={app}";

        private static IDocumentStore Baue(string verbindung, string schema, int pool) => DocumentStore.For(opts =>
        {
            opts.Connection($"{verbindung};Max Auto Prepare=20;Maximum Pool Size={pool}");
            opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
            opts.Events.MetadataConfig.HeadersEnabled = true;
            opts.DatabaseSchemaName = schema;
            opts.Events.DatabaseSchemaName = schema;
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
            Core.KonsumSchema.Registriere(opts);
        });

        /// <summary>Die Leser (nie abgebrochen).</summary>
        public IDocumentStore Store { get; } = Baue(Beispiel("g11-leser"), Schema, 100);

        /// <summary>Die Schreiber der Beispiel-Seite — eigene Verbindungen, damit der Abbrecher nur sie trifft.</summary>
        public IDocumentStore SchreibStore { get; } = Baue(Beispiel(AppSchreiber), Schema, 100);

        /// <summary>Ein neuer Store des zweiten Prozesses (je Start einer).</summary>
        public static IDocumentStore ZweiterStore() => Baue($"{Server};Database={ZweiteDatenbank};Application Name={AppZweiter}", "es", 50);

        public async Task InitializeAsync()
        {
            await using (var c = new NpgsqlConnection(Beispiel(AppAbbrecher)))
            {
                await c.OpenAsync();
                await using var cmd = new NpgsqlCommand($"drop schema if exists {Schema} cascade", c);
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var c = new NpgsqlConnection($"{Server};Database=postgres"))
            {
                await c.OpenAsync();
                await using (var drop = new NpgsqlCommand($"drop database if exists {ZweiteDatenbank} with (force)", c)) await drop.ExecuteNonQueryAsync();
                await using (var create = new NpgsqlCommand($"create database {ZweiteDatenbank}", c)) await create.ExecuteNonQueryAsync();
            }
            await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            await Store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent));
            await SchreibStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        }

        public Task DisposeAsync() { Store.Dispose(); SchreibStore.Dispose(); return Task.CompletedTask; }
    }
}
