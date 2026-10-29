using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
/// Der Ausgangskorb des Clients (docs/11, „Client → Aggregat“): ein Command liegt darin, bevor er gesendet wird;
/// bricht die Verbindung, geht er nach dem Wiederverbinden mit DERSELBEN Command-Id und Korrelation an die neue Sitzung — die Inbox des Ziels
/// lässt ihn genau einmal wirken. Mit der Antwort ist er erledigt. Der Korb als Verzeichnis übersteht einen Neustart des Clients.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AusgangskorbTests
{
    /// <summary>Eine Leitung ohne Server: zählt, was gesendet wird; die erste Sitzung bricht beim Senden ab.</summary>
    private sealed class Leitung : IGrpcProxy
    {
        private readonly Channel<EventEnvelope> _events = Channel.CreateUnbounded<EventEnvelope>();
        private int _verbindung;
        public ConcurrentQueue<(string Sitzung, CommandEnvelope Envelope)> Gesendet { get; } = new();
        public bool BrichtAb { get; set; }

        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;
        public string? SessionId { get; private set; }
        public bool IsConnected => CurrentState == ConnectionState.Connected;
        public ChannelReader<EventEnvelope> Events => _events.Reader;
        public ChannelReader<ConnectionState> StateChanges { get; } = Channel.CreateUnbounded<ConnectionState>().Reader;
        public ChannelReader<IQuery> Aktualisierungen { get; } = Channel.CreateUnbounded<IQuery>().Reader;

        public Task<ClientCapabilities> ConnectAsync(string serverAddress, string vertrag, string vertragHash, CancellationToken ct = default)
        {
            CurrentState = ConnectionState.Connected;
            SessionId = $"knoten#{Interlocked.Increment(ref _verbindung):D4}";
            return Task.FromResult(new ClientCapabilities
            {
                SessionId = SessionId, AllowedCommands = [], SubscribedEvents = [], SupportedQueries = [], Vertrag = vertrag, VertragHash = vertragHash,
            });
        }

        public Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default)
        {
            Gesendet.Enqueue((SessionId!, envelope));
            if (BrichtAb) throw new IOException("Verbindung abgerissen");
            // Die Antwort des Servers: das Event mit der Korrelation des Commands.
            _events.Writer.TryWrite(new EventEnvelope
            {
                AggregateId = envelope.AggregateId, AggregateVersion = 1, CorrelationId = envelope.CorrelationId, AggregateType = "Datensatz",
                Payload = new DatensatzErstellt("x"),
            });
            return Task.CompletedTask;
        }

        public Task<QueryResponse<TResponse>> QueryAsync<TResponse>(IQuery query, string correlationId, IReadOnlyList<StreamStand>? mindestens = null,
            CancellationToken ct = default) where TResponse : IQueryResponse => throw new NotSupportedException();

        public Task<LogLesung> LiesLogAsync(long ab, int hoechstens = 500, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DisconnectAsync()
        {
            CurrentState = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }
    }

    private static async Task BisAsync(Func<bool> bedingung)
    {
        var bis = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException("Bedingung nicht erreicht");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Nach_dem_Abriss_geht_der_Command_mit_derselben_Id_an_die_neue_Sitzung_und_ist_mit_der_Antwort_erledigt()
    {
        var leitung = new Leitung { BrichtAb = true };
        var korb = new Ausgangskorb();
        var bus = new ClientBus();
        var verbindung = new ConnectionModule(leitung, new VersioningModule(), korb: korb);
        verbindung.Activate(bus, [typeof(ErstelleDatensatz)], new Dictionary<Type, string> { [typeof(ErstelleDatensatz)] = "Datensatz" });
        await verbindung.ConnectAsync("http://nirgends", "Arbeitsplatz", "h");

        var datensatz = Guid.NewGuid();
        bus.Publish(new ErstelleDatensatz(new(datensatz), "x"), MessageContext.Local() with { CorrelationId = "k-1" });
        await BisAsync(() => leitung.Gesendet.Count == 1);
        korb.Offen().Should().ContainSingle("die Verbindung riss beim Senden ab — der Ausgang ist unbekannt");

        leitung.BrichtAb = false;
        await verbindung.DisconnectAsync();
        await verbindung.ConnectAsync("http://nirgends", "Arbeitsplatz", "h");

        await BisAsync(() => leitung.Gesendet.Count == 2 && korb.Offen().Count == 0);
        var (ersteSitzung, erster) = leitung.Gesendet.First();
        var (zweiteSitzung, zweiter) = leitung.Gesendet.Last();
        zweiter.CommandId.Should().Be(erster.CommandId, "nachgesendet wird mit derselben Command-Id — die Inbox lässt ihn genau einmal wirken");
        zweiter.CorrelationId.Should().Be("k-1");
        zweiteSitzung.Should().NotBe(ersteSitzung);
        zweiter.OriginSessionId.Should().Be(zweiteSitzung, "eine Ablehnung soll die neue Sitzung erreichen");
        await verbindung.DisposeAsync();
    }

    [Fact]
    public void Der_Korb_als_Verzeichnis_uebersteht_einen_Neustart_in_der_Reihenfolge_des_Ablegens()
    {
        var verzeichnis = Path.Combine(Path.GetTempPath(), "ausgangskorb-" + Guid.NewGuid().ToString("N"));
        try
        {
            var tabelle = Domain.Leitung.GeneratedNutzlast.Instanz;
            var korb = new DateiAusgangskorb(verzeichnis, tabelle);
            var a = new CommandEnvelope
            {
                Payload = new ErstelleDatensatz(new(Guid.NewGuid()), "a"), Modus = new CommandModus.Client(0), CorrelationId = "k-a", AggregateType = "Datensatz",
            };
            var b = a with { CommandId = Guid.NewGuid(), Payload = new ErstelleDatensatz(new(Guid.NewGuid()), "b"), CorrelationId = "k-b" };
            korb.Lege(a);
            korb.Lege(b);
            korb.Erledigt(a.CommandId);

            var nachNeustart = new DateiAusgangskorb(verzeichnis, tabelle).Offen();
            nachNeustart.Should().ContainSingle();
            var offen = nachNeustart.Single();
            offen.CommandId.Should().Be(b.CommandId);
            offen.CorrelationId.Should().Be("k-b");
            offen.Payload.Should().Be(b.Payload);
            offen.Modus.Should().Be(new CommandModus.Client(0));
        }
        finally
        {
            if (Directory.Exists(verzeichnis)) Directory.Delete(verzeichnis, true);
        }
    }
}
