using System.Collections.Concurrent;
using System.Threading.Channels;
using Abstractions;
using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Cqrs.Leitung;
using Domain.Datensatz;
using Domain.Projections.Datensatz;
using Microsoft.Extensions.DependencyInjection;

namespace Cqrs.Vermessung;

/// <summary>
/// S6 Clients über gRPC (Beispieldomäne): der Weg echter Clients — je Sender ein eigener C#-Client mit dem Stack des Blazor-Arbeitsplatzes
/// (eigene gRPC-Sitzung, Ausgangskorb, Quittung, Versionen, read-your-writes) und seinem generierten Vertrag. Ein Vorgang: der Client nimmt ein
/// neues Paar in seinen Datensatz auf (<c>NimmPaarAuf</c>) und wartet auf die Quittung, dann fragt er den Datensatz (<c>HoleDatensatz</c>, mit
/// „mindestens S auf v“), bis das Paar darin steht — sichtbar. Gemessen werden so Sitzung, Dispatcher, Verteiler, Quittung und der Stand je Id
/// unter vielen gleichzeitigen Clients. Je Datensatz höchstens <see cref="PaareJeDatensatz"/> Paare, dann legt der Client einen neuen an (die
/// Antwort wächst mit den Mitgliedern).
/// </summary>
internal sealed class ClientSitzungen : ILast, IAsyncDisposable
{
    public const string Token = "vermessung-arbeitsplatz";
    public const int PaareJeDatensatz = 50;
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(30);

    private readonly List<Client> _alle;
    private readonly Channel<Client> _frei = Channel.CreateUnbounded<Client>();

    private ClientSitzungen(List<Client> alle)
    {
        _alle = alle;
        foreach (var c in alle) _frei.Writer.TryWrite(c);
    }

    /// <summary>Latenz vom Senden bis zur Quittung je Vorgang (ms).</summary>
    public ConcurrentBag<double> QuittungMs { get; } = new();

    /// <summary>Was gesendet wurde: je Datensatz die aufgenommenen Paare (für die Exactly-once-Prüfung).</summary>
    public ConcurrentDictionary<Guid, ConcurrentBag<Guid>> Gesendet { get; } = new();

    /// <summary>Commands, die der Client-Stack als unbestätigt meldete (bei stehender Verbindung ohne Antwort) — soll 0 sein.</summary>
    public int Unbestaetigt => _alle.Sum(c => c.Unbestaetigt);

    /// <summary>Wie oft ein Client neu verbinden musste — soll 0 sein.</summary>
    public int Wiederverbunden => _alle.Sum(c => c.Wiederverbunden);

    public int Sitzungen => _alle.Count;

    public static async Task<ClientSitzungen> VerbindeAsync(int grpcPort, int anzahl)
    {
        var alle = await Task.WhenAll(Enumerable.Range(0, anzahl).Select(_ => Client.VerbindeAsync($"http://127.0.0.1:{grpcPort}")));
        return new ClientSitzungen(alle.ToList());
    }

    public async Task StarteAsync(Vorgang v, CancellationToken ct)
    {
        var c = await _frei.Reader.ReadAsync(ct);
        try
        {
            if (c.Datensatz is not { } ds || c.Paare >= PaareJeDatensatz)
            {
                ds = Guid.NewGuid();
                Gesendet[ds] = new ConcurrentBag<Guid>();
                await c.SendeUndWarteAsync(new ErstelleDatensatz(new(ds), $"S6 {ds:N}"[..12]), ct);
                c.Datensatz = ds;
                c.Paare = 0;
            }
            var paar = Guid.NewGuid();
            v.Start = DateTimeOffset.UtcNow;
            await c.SendeUndWarteAsync(new NimmPaarAuf(new(ds), new(paar)), ct);
            QuittungMs.Add((DateTimeOffset.UtcNow - v.Start).TotalMilliseconds);
            Gesendet[ds].Add(paar);
            c.Paare++;

            // Sichtbar: die nächste Frage des Clients zeigt das Paar (read-your-writes) — mit Nachfragen, falls die Leseseite in ihrer Frist nicht
            //   so weit kam („noch nicht“).
            while (!ct.IsCancellationRequested)
            {
                if (await c.FrageAsync(new HoleDatensatz(new(ds))) is DatensatzAntwort a && a.Mitglieder.Any(m => m.Wert == paar))
                {
                    v.Sichtbar = DateTimeOffset.UtcNow;
                    return;
                }
                if (DateTimeOffset.UtcNow - v.Start > Frist) throw new TimeoutException($"S6: Paar {paar} nach {Frist.TotalSeconds:0} s nicht sichtbar");
                await Task.Delay(50, ct);
            }
        }
        finally { _frei.Writer.TryWrite(c); }
    }

    /// <summary>
    /// Exactly-once (§0.2) über die Leitung: je Datensatz genau ein Anlegen, jedes gesendete Paar genau einmal aufgenommen, keines doppelt, und
    /// kein Command blieb für den Client unbestätigt.
    /// </summary>
    public async Task<List<Pruefung>> PruefeAsync(Datenbank db, string titel, bool vollstaendig)
    {
        var s = db.Schema;
        var datensaetze = Gesendet.Keys.ToArray();
        var paare = Gesendet.Values.Sum(p => p.Count);
        return new List<Pruefung>
        {
            Pruefung.Erwartet($"{titel}: je Datensatz genau ein DatensatzErstellt", datensaetze.Length, await db.ZahlAsync(
                $"select count(*) from {s}.mt_events where stream_id = any(@d) and type = 'datensatz_erstellt'", ("d", datensaetze)), vollstaendig),
            Pruefung.Erwartet($"{titel}: je gesendetem Paar genau ein PaarAufgenommen", paare, await db.ZahlAsync(
                $"select count(*) from {s}.mt_events where stream_id = any(@d) and type = 'paar_aufgenommen'", ("d", datensaetze)), vollstaendig),
            Pruefung.Gleich($"{titel}: kein Paar zweimal aufgenommen", 0, await db.ZahlAsync($"""
                select count(*) from (select stream_id, data->>'ImagePairId' from {s}.mt_events
                  where stream_id = any(@d) and type = 'paar_aufgenommen' group by 1, 2 having count(*) > 1) x
                """, ("d", datensaetze))),
            Pruefung.Gleich($"{titel}: kein Command blieb unbestätigt (Quittung kam)", 0, Unbestaetigt),
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _alle) await c.DisposeAsync();
    }

    /// <summary>Ein Client mit dem Stack des Blazor-Arbeitsplatzes, ohne Oberfläche: Bus, Verbindung, Versionen, generierter Vertrag.</summary>
    private sealed class Client : IAsyncDisposable
    {
        private readonly ServiceProvider _sp;
        private readonly ClientBus _bus;
        private readonly GrpcProxy _proxy;
        private readonly VersioningModule _versionen;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _offen = new();
        private int _unbestaetigt, _wiederverbunden;

        public Guid? Datensatz { get; set; }
        public int Paare { get; set; }
        public int Unbestaetigt => _unbestaetigt;
        public int Wiederverbunden => _wiederverbunden;

        private Client(ServiceProvider sp)
        {
            _sp = sp;
            _bus = sp.GetRequiredService<ClientBus>();
            _proxy = sp.GetRequiredService<GrpcProxy>();
            _versionen = sp.GetRequiredService<VersioningModule>();
        }

        public static async Task<Client> VerbindeAsync(string adresse)
        {
            var s = new ServiceCollection();
            s.AddSingleton<INutzlastTabelle>(Domain.Leitung.GeneratedNutzlast.Instanz);
            s.AddClientInfrastructure();
            var c = new Client(s.BuildServiceProvider());
            c._proxy.AkteurToken = Token;
            var verbindung = c._sp.GetRequiredService<ConnectionModule>();
            verbindung.Activate(c._bus, GeneratedWiring_Domain_Client_Modules_Blazor.CommandTypes,
                GeneratedWiring_Domain_Client_Modules_Blazor.CommandAggregateTypes);
            c._versionen.Activate(c._bus, GeneratedWiring_Domain_Client_Modules_Blazor.ServerEventTypes);
            // Die Antwort auf einen Command: die Quittung, ein gehörtes Event oder CommandFailed — über die Korrelation.
            foreach (var t in new[] { typeof(CommandQuittung), typeof(CommandFailed), typeof(DatensatzErstellt), typeof(PaarAufgenommen) })
                c._bus.Subscribe(t, (m, ctx) =>
                {
                    if (ctx.CorrelationId is { Length: > 0 } k && c._offen.TryRemove(k, out var tcs)) tcs.TrySetResult(m);
                });
            c._bus.Subscribe(typeof(CommandUnbestaetigt), (_, _) => Interlocked.Increment(ref c._unbestaetigt));
            c._bus.Subscribe(typeof(ReconnectAttempt), (_, _) => Interlocked.Increment(ref c._wiederverbunden));
            await verbindung.ConnectAsync(adresse, GeneratedWiring_Domain_Client_Modules_Blazor.Vertrag,
                GeneratedWiring_Domain_Client_Modules_Blazor.VertragHash);
            return c;
        }

        /// <summary>Legt den Command auf den Bus (wie ein IntentHandler) und wartet auf seine Antwort; <c>CommandFailed</c> wirft.</summary>
        public async Task SendeUndWarteAsync(ICommand command, CancellationToken ct)
        {
            var korrelation = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _offen[korrelation] = tcs;
            _bus.Publish(command, MessageContext.Local() with { CorrelationId = korrelation });
            var fertig = await Task.WhenAny(tcs.Task, Task.Delay(Frist, ct));
            _offen.TryRemove(korrelation, out _);
            if (fertig != tcs.Task) throw new TimeoutException($"S6: keine Antwort auf {command.GetType().Name} nach {Frist.TotalSeconds:0} s");
            if (await tcs.Task is CommandFailed f) throw new InvalidOperationException($"S6: {f.CommandType} gescheitert: {f.Reason}");
        }

        /// <summary>Fragt wie die Query-Brücke: mit „mindestens S auf v“ für die zuletzt beschriebenen Aggregate; der Stand je Id fließt zurück.</summary>
        public async Task<IQueryResponse?> FrageAsync(IQuery query)
        {
            var antwort = await _proxy.QueryAsync<IQueryResponse>(query, Guid.NewGuid().ToString(), _versionen.Mindestens);
            if (antwort.Deps is { } deps) _versionen.TrackFromDeps(deps.Select(d => new AggregateDep(d.Id, d.AggregateType, d.Version)));
            return antwort.Data;
        }

        public ValueTask DisposeAsync() => _sp.DisposeAsync();
    }
}
