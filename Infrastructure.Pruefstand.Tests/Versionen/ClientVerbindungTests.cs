using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Abstractions;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Domain.Datensatz;
using FluentAssertions;
using Xunit;

namespace Infrastructure.Pruefstand.Versionen;

/// <summary>
/// Der C#-Client kennt den Ausgang jedes Commands und hält die Verbindung (docs/08 §8.4, FB-22): eine <see cref="CommandQuittung"/> beendet das
/// Warten und bringt die Version des Aggregats, auch ohne gehörtes Event; reißt die Verbindung, verbindet der <see cref="ConnectionModule"/>
/// selbst neu und sendet den Ausgangskorb nach — auch einen Command, der ohne Verbindung kam.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ClientVerbindungTests
{
    /// <summary>Eine Leitung ohne Server: zählt, was gesendet wird, antwortet mit einer Quittung (oder schweigt), kann reißen und Versuche ablehnen.</summary>
    private sealed class Leitung : IGrpcProxy
    {
        private readonly Channel<EventEnvelope> _events = Channel.CreateUnbounded<EventEnvelope>();
        private readonly Channel<ConnectionState> _zustaende = Channel.CreateUnbounded<ConnectionState>();
        private int _verbindung;
        public ConcurrentQueue<(string Sitzung, CommandEnvelope Envelope)> Gesendet { get; } = new();
        public int Verbindungsversuche;
        public int AbgelehnteVersuche { get; set; }
        public bool Schweigt { get; set; }
        public int QuittungsVersion { get; set; } = 7;

        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;
        public string? SessionId { get; private set; }
        public bool IsConnected => CurrentState == ConnectionState.Connected;
        public ChannelReader<EventEnvelope> Events => _events.Reader;
        public ChannelReader<ConnectionState> StateChanges => _zustaende.Reader;
        public ChannelReader<IQuery> Aktualisierungen { get; } = Channel.CreateUnbounded<IQuery>().Reader;

        private void Setze(ConnectionState s)
        {
            CurrentState = s;
            _zustaende.Writer.TryWrite(s);
        }

        /// <summary>Der Server schließt den Stream: die Verbindung ist abgebrochen.</summary>
        public void Reisse() => Setze(ConnectionState.Failed);

        public Task<ClientCapabilities> ConnectAsync(string serverAddress, string vertrag, string vertragHash, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Verbindungsversuche);
            if (AbgelehnteVersuche > 0)
            {
                AbgelehnteVersuche--;
                Setze(ConnectionState.Failed);
                throw new InvalidOperationException("Server nicht erreichbar");
            }
            SessionId = $"knoten#{Interlocked.Increment(ref _verbindung):D4}";
            Setze(ConnectionState.Connected);
            return Task.FromResult(new ClientCapabilities
            {
                SessionId = SessionId, AllowedCommands = [], SubscribedEvents = [], SupportedQueries = [], Vertrag = vertrag, VertragHash = vertragHash,
            });
        }

        public Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");
            Gesendet.Enqueue((SessionId!, envelope));
            if (Schweigt) return Task.CompletedTask;
            // Die Antwort des Servers: nur die Quittung (ein Noop — kein Event, das der Client hören könnte).
            _events.Writer.TryWrite(new EventEnvelope
            {
                AggregateId = envelope.AggregateId, AggregateVersion = QuittungsVersion, CorrelationId = envelope.CorrelationId,
                AggregateType = "Datensatz", ZielSitzung = envelope.OriginSessionId,
                Payload = new CommandQuittung(envelope.Payload.GetType().Name, envelope.AggregateId, QuittungsVersion, Geschrieben: false),
            });
            return Task.CompletedTask;
        }

        public Task<QueryResponse<TResponse>> QueryAsync<TResponse>(IQuery query, string correlationId, IReadOnlyList<StreamStand>? mindestens = null,
            CancellationToken ct = default) where TResponse : IQueryResponse => throw new NotSupportedException();

        public Task<LogLesung> LiesLogAsync(long ab, int hoechstens = 500, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DisconnectAsync()
        {
            if (CurrentState != ConnectionState.Disconnected) Setze(ConnectionState.Disconnected);
            return Task.CompletedTask;
        }
    }

    private static readonly TimeSpan[] Kurz = [TimeSpan.FromMilliseconds(20)];

    private static async Task BisAsync(Func<bool> bedingung, string meldung, double sekunden = 5)
    {
        var bis = DateTime.UtcNow + TimeSpan.FromSeconds(sekunden);
        while (!bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException(meldung);
            await Task.Delay(10);
        }
    }

    private static (ConnectionModule Verbindung, ClientBus Bus, ConcurrentQueue<object> Gemeldet) Baue(Leitung leitung, VersioningModule versionen,
        IAusgangskorb korb)
    {
        var bus = new ClientBus();
        var gemeldet = new ConcurrentQueue<object>();
        foreach (var t in new[] { typeof(CommandUnbestaetigt), typeof(CommandSendFailed), typeof(CommandWartetAufVerbindung), typeof(ReconnectAttempt),
                     typeof(ConnectionEstablished), typeof(ConnectionLost) })
            bus.Subscribe(t, (m, _) => gemeldet.Enqueue(m));
        var verbindung = new ConnectionModule(leitung, versionen, korb: korb, pausen: Kurz);
        verbindung.Activate(bus, [typeof(NimmPaarAuf)], new Dictionary<Type, string> { [typeof(NimmPaarAuf)] = "Datensatz" });
        return (verbindung, bus, gemeldet);
    }

    [Fact]
    public async Task Die_Quittung_beendet_das_Warten_und_bringt_die_Version_auch_ohne_gehoertes_Event()
    {
        var leitung = new Leitung { QuittungsVersion = 7 };
        var versionen = new VersioningModule();
        var korb = new Ausgangskorb();
        var (verbindung, bus, gemeldet) = Baue(leitung, versionen, korb);
        await verbindung.ConnectAsync("http://nirgends", "Arbeitsplatz", "h");

        var datensatz = Guid.NewGuid();
        bus.Publish(new NimmPaarAuf(new(datensatz), new(Guid.NewGuid())), MessageContext.Local() with { CorrelationId = "k-1" });

        await BisAsync(() => korb.Offen().Count == 0 && leitung.Gesendet.Count == 1, "die Quittung hat den Command nicht erledigt");
        await BisAsync(() => versionen.GetVersion(datensatz) == 7, "die Version aus der Quittung kam nicht im Versionsmodul an");
        await Task.Delay(300);
        leitung.Gesendet.Should().ContainSingle("mit der Quittung ist der Ausgang bekannt — kein zweiter Versuch");
        gemeldet.OfType<CommandUnbestaetigt>().Should().BeEmpty();
        await verbindung.DisposeAsync();
    }

    [Fact]
    public async Task Reisst_die_Verbindung_verbindet_der_Client_selbst_neu_und_sendet_was_ohne_Verbindung_kam()
    {
        var leitung = new Leitung();
        var korb = new Ausgangskorb();
        var (verbindung, bus, gemeldet) = Baue(leitung, new VersioningModule(), korb);
        await verbindung.ConnectAsync("http://nirgends", "Arbeitsplatz", "h");
        var ersteSitzung = leitung.SessionId;

        // Der Server ist fort, jeder Versuch scheitert; währenddessen kommt ein Command — er wartet im Korb. Dann ist der Server wieder da.
        leitung.AbgelehnteVersuche = int.MaxValue;
        leitung.Reisse();
        await BisAsync(() => leitung.Verbindungsversuche >= 3, "der Client versuchte nicht, wieder zu verbinden");
        var datensatz = Guid.NewGuid();
        bus.Publish(new NimmPaarAuf(new(datensatz), new(Guid.NewGuid())), MessageContext.Local() with { CorrelationId = "k-2" });
        await BisAsync(() => gemeldet.OfType<CommandWartetAufVerbindung>().Any(), "der Command meldete nicht, dass er auf die Verbindung wartet");
        leitung.Gesendet.Should().BeEmpty();
        leitung.AbgelehnteVersuche = 0;

        await BisAsync(() => leitung.Gesendet.Count == 1 && korb.Offen().Count == 0, "der Command ging nach dem Wiederverbinden nicht hinaus");
        var (sitzung, gesendet) = leitung.Gesendet.Single();
        sitzung.Should().NotBe(ersteSitzung, "er geht an die neue Sitzung");
        gesendet.OriginSessionId.Should().Be(sitzung, "ihre Quittung und eine Ablehnung sollen die neue Sitzung erreichen");
        gesendet.CorrelationId.Should().Be("k-2");
        gemeldet.OfType<ReconnectAttempt>().Should().HaveCountGreaterThanOrEqualTo(3, "Versuch um Versuch, bis der Server wieder da ist");
        gemeldet.OfType<ConnectionEstablished>().Should().HaveCount(2, "nach dem Wiederverbinden fragen die Module ihren Stand neu");
        gemeldet.OfType<CommandWartetAufVerbindung>().Should().ContainSingle();
        gemeldet.OfType<CommandSendFailed>().Should().BeEmpty("ein Command ohne Verbindung wartet, er scheitert nicht");
        await verbindung.DisposeAsync();
    }

    [Fact]
    public async Task Reisst_die_Verbindung_waehrend_ein_Command_auf_Antwort_wartet_geht_er_nach_dem_Wiederverbinden_noch_einmal()
    {
        var leitung = new Leitung { Schweigt = true };
        var korb = new Ausgangskorb();
        var (verbindung, bus, gemeldet) = Baue(leitung, new VersioningModule(), korb);
        await verbindung.ConnectAsync("http://nirgends", "Arbeitsplatz", "h");

        bus.Publish(new NimmPaarAuf(new(Guid.NewGuid()), new(Guid.NewGuid())), MessageContext.Local() with { CorrelationId = "k-3" });
        await BisAsync(() => leitung.Gesendet.Count == 1, "der Command ging nicht hinaus");
        leitung.Schweigt = false;
        leitung.Reisse();

        // Gleich nach dem Wiederverbinden, nicht erst, wenn seine 5 s Wartezeit abgelaufen sind — und an die neue Sitzung.
        await BisAsync(() => leitung.Gesendet.Count >= 2 && korb.Offen().Count == 0, "der Command ging nach dem Wiederverbinden nicht gleich noch einmal",
            sekunden: 2);
        leitung.Gesendet.Last().Envelope.OriginSessionId.Should().Be(leitung.SessionId, "die Antwort soll die neue Sitzung erreichen");
        leitung.Gesendet.Select(g => g.Envelope.CommandId).Distinct().Should().ContainSingle("dieselbe Command-Id — die Inbox lässt ihn einmal wirken");
        gemeldet.OfType<CommandUnbestaetigt>().Should().BeEmpty("die Stille war der Abbruch, nicht ein unbekannter Ausgang");
        await verbindung.DisposeAsync();
    }
}
