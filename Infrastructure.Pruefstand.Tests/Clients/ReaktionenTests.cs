using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Abstractions;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Reaktionen;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Clients;

/// <summary>
/// Reaktionen eines Clients (docs/plan-katalog-und-graphen.md, Schritt 6): der Läufer liest ab dem gesicherten Stand, reagiert je Event-Typ, sendet
/// mit Ids aus Reaktion, Position und Stelle und sichert den Stand erst nach den Antworten. Bewiesen gegen eine Leitung im Speicher: die Ids, der
/// Stand, kein zweites Senden nach einem Neustart ab dem Stand, dieselben Ids ohne gesicherten Stand, das Wiederholen mit derselben Id bei Stille,
/// mehrere Events an einer Position, ein Command ohne Ziel an den Kopf eines Graphen.
/// </summary>
public class ReaktionenTests
{
    private sealed class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Bestellt(Guid Kunde, int Betrag) : IEvent;
    private sealed record Storniert(Guid Kunde) : IEvent;
    private sealed record Buche(Id<Konto> AggregateId, int Betrag) : ICommand<Konto>;
    private sealed record Benachrichtige(Guid Kunde) : ICommand;

    /// <summary>Die Leitung im Speicher: ein Log nach Position, was gesendet wurde, und Quittungen (außer sie schweigt).</summary>
    private sealed class Leitung : IGrpcProxy
    {
        private readonly Channel<EventEnvelope> _events = Channel.CreateUnbounded<EventEnvelope>();
        public readonly List<(long Position, IEvent Event)> Log = new();
        public readonly ConcurrentQueue<(Guid Id, ICommand Command)> Gesendet = new();
        /// <summary>So oft schweigt sie noch (keine Quittung), bevor sie wieder antwortet.</summary>
        public int Schweigt;
        /// <summary>Der Horizont, den die Seite meldet (null = unbekannt).</summary>
        public long? Horizont;
        public readonly ConcurrentQueue<string?> Ursachen = new();

        public ConnectionState CurrentState => ConnectionState.Connected;
        public string? SessionId => "knoten#0001";
        public bool IsConnected => true;
        public ChannelReader<EventEnvelope> Events => _events.Reader;
        public ChannelReader<ConnectionState> StateChanges { get; } = Channel.CreateUnbounded<ConnectionState>().Reader;
        public ChannelReader<IQuery> Aktualisierungen { get; } = Channel.CreateUnbounded<IQuery>().Reader;

        public Task<ClientCapabilities> ConnectAsync(string serverAddress, string vertrag, string vertragHash, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default)
        {
            envelope.Modus.Should().BeOfType<CommandModus.Client>().Which.ExpectedVersion.Should().Be(-1, "eine Reaktion schickt ohne OCC");
            Ursachen.Enqueue(envelope.Ursache);
            return Gesendet_(envelope.CommandId, envelope.Payload, envelope.CorrelationId);
        }

        public Task SendeNachrichtAsync(ICommand nachricht, Guid commandId, string correlationId, CancellationToken ct = default, string? ursache = null) =>
            Gesendet_(commandId, nachricht, correlationId);

        private Task Gesendet_(Guid id, ICommand command, string korrelation)
        {
            Gesendet.Enqueue((id, command));
            if (Schweigt > 0) { Schweigt--; return Task.CompletedTask; }
            _events.Writer.TryWrite(new EventEnvelope { CorrelationId = korrelation, Payload = new CommandQuittung(command.GetType().Name, id, 1, true) });
            return Task.CompletedTask;
        }

        public Task<QueryResponse<TResponse>> QueryAsync<TResponse>(IQuery query, string correlationId, IReadOnlyList<StreamStand>? mindestens = null,
            CancellationToken ct = default) where TResponse : IQueryResponse => throw new NotSupportedException();

        public Task<LogLesung> LiesLogAsync(long ab, int hoechstens = 500, CancellationToken ct = default)
        {
            var neu = Log.Where(e => e.Position > ab)
                .Select(e => new LogEintragLesung(e.Position, new EventEnvelope { AggregateId = Strom, AggregateVersion = (int)e.Position, Payload = e.Event }))
                .ToList();
            return Task.FromResult(new LogLesung(neu, Log.Count == 0 ? ab : Math.Max(ab, Log.Max(e => e.Position)), false, Horizont ?? -1));
        }

        public Task DisconnectAsync() => Task.CompletedTask;
    }

    private static readonly Guid Kunde = Guid.NewGuid();
    private static readonly Guid Strom = Guid.NewGuid();

    private static Reaktionen Laeufer(Leitung l, IReaktionsStand stand) => new Reaktionen(l, stand, "buchhaltung", takt: TimeSpan.FromMilliseconds(50),
            antwortfrist: TimeSpan.FromMilliseconds(200))
        .Auf<Bestellt>((e, k) => { k.Sende(new Buche(new Id<Konto>(e.Kunde), e.Betrag)); k.Sende(new Benachrichtige(e.Kunde)); return Task.CompletedTask; });

    /// <summary>Lässt den Läufer laufen, bis der Stand <paramref name="bis"/> gesichert ist.</summary>
    private static async Task LaufeBisAsync(Reaktionen r, IReaktionsStand stand, long bis)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var lauf = r.LaufeAsync(cts.Token);
        while (await stand.LadeAsync("buchhaltung", default) < bis && !cts.IsCancellationRequested) await Task.Delay(20);
        await cts.CancelAsync();
        await lauf;
        (await stand.LadeAsync("buchhaltung", default)).Should().Be(bis);
    }

    [Fact]
    public async Task Eine_Reaktion_sendet_mit_abgeleiteten_Ids_und_sichert_den_Stand_danach()
    {
        var l = new Leitung();
        l.Log.AddRange([(3, new Bestellt(Kunde, 10)), (4, new Storniert(Kunde)), (5, new Bestellt(Kunde, 20))]);
        var stand = new ReaktionsStandImSpeicher();
        await LaufeBisAsync(Laeufer(l, stand), stand, 5);

        l.Gesendet.Select(x => x.Id).Should().Equal(
            Reaktionen.CommandId("buchhaltung", 3, 0, 0), Reaktionen.CommandId("buchhaltung", 3, 0, 1),
            Reaktionen.CommandId("buchhaltung", 5, 0, 0), Reaktionen.CommandId("buchhaltung", 5, 0, 1));
        l.Gesendet.Select(x => x.Command).Should().Equal(new Buche(new Id<Konto>(Kunde), 10), new Benachrichtige(Kunde),
            new Buche(new Id<Konto>(Kunde), 20), new Benachrichtige(Kunde));
    }

    [Fact]
    public async Task Ein_Command_an_ein_Aggregat_traegt_das_Event_als_Ursache()
    {
        var l = new Leitung();
        l.Log.Add((3, new Bestellt(Kunde, 10)));
        var stand = new ReaktionsStandImSpeicher();
        await LaufeBisAsync(Laeufer(l, stand), stand, 3);

        l.Ursachen.Should().Equal(Spur.Ursache(Strom, 3));
    }

    [Fact]
    public async Task Ein_Log_unter_dem_eigenen_Stand_ist_neu_der_Laeufer_liest_von_vorn()
    {
        var l = new Leitung { Horizont = 3 };
        l.Log.Add((3, new Bestellt(Kunde, 10)));
        var stand = new ReaktionsStandImSpeicher();
        await stand.SichereAsync("buchhaltung", 50, default);   // der Stand einer früheren Datenbank
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var lauf = Laeufer(l, stand).LaufeAsync(cts.Token);
        while (l.Gesendet.Count < 2 && !cts.IsCancellationRequested) await Task.Delay(20);
        while (await stand.LadeAsync("buchhaltung", default) != 3 && !cts.IsCancellationRequested) await Task.Delay(20);
        await cts.CancelAsync();
        await lauf;

        l.Gesendet.Select(x => x.Id).Should().Equal(Reaktionen.CommandId("buchhaltung", 3, 0, 0), Reaktionen.CommandId("buchhaltung", 3, 0, 1));
        (await stand.LadeAsync("buchhaltung", default)).Should().Be(3);
    }

    [Fact]
    public async Task Ein_Neustart_ab_dem_gesicherten_Stand_sendet_nichts_noch_einmal_neue_Events_schon()
    {
        var l = new Leitung();
        l.Log.Add((3, new Bestellt(Kunde, 10)));
        var stand = new ReaktionsStandImSpeicher();
        await LaufeBisAsync(Laeufer(l, stand), stand, 3);
        l.Log.Add((8, new Bestellt(Kunde, 30)));
        await LaufeBisAsync(Laeufer(l, stand), stand, 8);

        l.Gesendet.Select(x => x.Id).Should().Equal(
            Reaktionen.CommandId("buchhaltung", 3, 0, 0), Reaktionen.CommandId("buchhaltung", 3, 0, 1),
            Reaktionen.CommandId("buchhaltung", 8, 0, 0), Reaktionen.CommandId("buchhaltung", 8, 0, 1));
    }

    [Fact]
    public async Task Ohne_gesicherten_Stand_rechnet_ein_neuer_Client_dieselben_Ids()
    {
        var l = new Leitung();
        l.Log.Add((3, new Bestellt(Kunde, 10)));
        var erst = new ReaktionsStandImSpeicher();
        await LaufeBisAsync(Laeufer(l, erst), erst, 3);
        var verloren = new ReaktionsStandImSpeicher();   // der Stand ging verloren (Absturz vor dem Sichern)
        await LaufeBisAsync(Laeufer(l, verloren), verloren, 3);

        var ids = l.Gesendet.Select(x => x.Id).ToList();
        ids.Should().HaveCount(4);
        ids.Distinct().Should().HaveCount(2, "derselbe Client rechnet nach dem Absturz dieselben Ids — die Inbox des Ziels verwirft das Zweite");
    }

    [Fact]
    public async Task Bleibt_die_Antwort_aus_geht_der_Command_mit_derselben_Id_noch_einmal()
    {
        var l = new Leitung { Schweigt = 1 };
        l.Log.Add((3, new Bestellt(Kunde, 10)));
        var stand = new ReaktionsStandImSpeicher();
        await LaufeBisAsync(Laeufer(l, stand), stand, 3);

        var erste = Reaktionen.CommandId("buchhaltung", 3, 0, 0);
        l.Gesendet.Select(x => x.Id).Should().Equal(erste, erste, Reaktionen.CommandId("buchhaltung", 3, 0, 1));
    }

    [Fact]
    public async Task Mehrere_Events_an_einer_Position_zaehlen_der_Reihe_nach()
    {
        var l = new Leitung();
        l.Log.AddRange([(7, new Bestellt(Kunde, 1)), (7, new Bestellt(Kunde, 2))]);
        var stand = new ReaktionsStandImSpeicher();
        await LaufeBisAsync(Laeufer(l, stand), stand, 7);

        l.Gesendet.Select(x => x.Id).Should().Equal(
            Reaktionen.CommandId("buchhaltung", 7, 0, 0), Reaktionen.CommandId("buchhaltung", 7, 0, 1),
            Reaktionen.CommandId("buchhaltung", 7, 1, 0), Reaktionen.CommandId("buchhaltung", 7, 1, 1));
    }

    [Fact]
    public async Task Der_Dateistand_uebersteht_einen_Neustart()
    {
        var verzeichnis = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "reaktionen-" + Guid.NewGuid().ToString("N"));
        try
        {
            await new DateiReaktionsStand(verzeichnis).SichereAsync("buchhaltung", 42, default);
            (await new DateiReaktionsStand(verzeichnis).LadeAsync("buchhaltung", default)).Should().Be(42);
            (await new DateiReaktionsStand(verzeichnis).LadeAsync("andere", default)).Should().Be(0);
        }
        finally { if (System.IO.Directory.Exists(verzeichnis)) System.IO.Directory.Delete(verzeichnis, true); }
    }

    [Fact]
    public void Die_Id_ist_in_CSharp_und_Python_dieselbe() =>
        Reaktionen.CommandId("a", 3, 0, 1).Should().Be(Guid.Parse("d0825625-5785-b876-04cb-8b1b44d5a2e6"), "test_reaktionen.py prüft denselben Wert");

    [Fact]
    public void Je_Event_Typ_eine_Reaktion()
    {
        var r = new Reaktionen(new Leitung(), new ReaktionsStandImSpeicher(), "x").Auf<Bestellt>((_, _) => Task.CompletedTask);
        r.Invoking(x => x.Auf<Bestellt>((_, _) => Task.CompletedTask)).Should().Throw<InvalidOperationException>();
    }
}
