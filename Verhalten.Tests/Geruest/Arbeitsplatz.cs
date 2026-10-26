using System.Collections.Concurrent;
using System.Diagnostics;
using Abstractions;
using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Connection;
using Client.Infrastructure.Versioning;
using Cqrs.Leitung;
using Microsoft.Extensions.DependencyInjection;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Der Blazor-Arbeitsplatz ohne Oberfläche: derselbe Client-Stack (Bus, Verbindung mit Wiederholung bei Stille, Query-Brücke mit
/// read-your-writes, Versionen aus den Pushes) und dasselbe generierte Wiring (<c>GeneratedWiring_Domain_Client_Modules_Blazor</c>: Vertrag,
/// Hash, gesendete Commands, gefragte Queries) — nur ohne Stores und Razor. Was ankommt, sammelt er mit Kontext und Zeit.
/// </summary>
public sealed class Arbeitsplatz : IAsyncDisposable
{
    private readonly ServiceProvider _sp;
    private readonly ConcurrentQueue<Empfangen> _empfangen = new();

    public sealed record Empfangen(object Nachricht, MessageContext Kontext, DateTimeOffset Zeit);

    private Arbeitsplatz(ServiceProvider sp)
    {
        _sp = sp;
        Bus = sp.GetRequiredService<ClientBus>();
        Versionen = sp.GetRequiredService<VersioningModule>();
    }

    public ClientBus Bus { get; }
    public VersioningModule Versionen { get; }
    public IReadOnlyCollection<Empfangen> Alles => _empfangen;

    /// <summary>Verbindet wie der Blazor-Client (<c>ClientStartupService</c> Schritte 2–4) mit dem gegebenen Token.</summary>
    public static async Task<Arbeitsplatz> VerbindeAsync(Hostlauf host, string token)
    {
        var s = new ServiceCollection();
        s.AddSingleton<INutzlastTabelle>(Domain.Leitung.GeneratedNutzlast.Instanz);
        s.AddClientInfrastructure();
        var a = new Arbeitsplatz(s.BuildServiceProvider());
        a._sp.GetRequiredService<GrpcProxy>().AkteurToken = token;

        var verbindung = a._sp.GetRequiredService<ConnectionModule>();
        GeneratedWiring_Domain_Client_Modules_Blazor.RegisterQueries(a._sp.GetRequiredService<QueryBridge>(), a.Bus);
        verbindung.Activate(a.Bus, GeneratedWiring_Domain_Client_Modules_Blazor.CommandTypes,
            GeneratedWiring_Domain_Client_Modules_Blazor.CommandAggregateTypes);
        a.Versionen.Activate(a.Bus, GeneratedWiring_Domain_Client_Modules_Blazor.ServerEventTypes);

        // Mithören: alles, was der Vertrag hört, dazu was der Client-Stack selbst meldet.
        foreach (var t in GeneratedWiring_Domain_Client_Modules_Blazor.ServerEventTypes.Append(typeof(CommandFailed)))
            a.Bus.Subscribe(t, (m, ctx) => a._empfangen.Enqueue(new Empfangen(m, ctx, DateTimeOffset.UtcNow)));
        foreach (var t in new[] { typeof(CommandUnbestaetigt), typeof(CommandSendFailed), typeof(QueryFailed), typeof(ConnectionLost) })
            a.Bus.Subscribe(t, (m, ctx) => a._empfangen.Enqueue(new Empfangen(m, ctx, DateTimeOffset.UtcNow)));

        await verbindung.ConnectAsync(host.GrpcAdresse, GeneratedWiring_Domain_Client_Modules_Blazor.Vertrag,
            GeneratedWiring_Domain_Client_Modules_Blazor.VertragHash);
        return a;
    }

    /// <summary>Ein Command, wie ihn ein IntentHandler auf den Bus legt — mit der Korrelation, über die die Antwort quittiert.</summary>
    public string Sende(ICommand command)
    {
        var korrelation = Guid.NewGuid().ToString();
        Bus.Publish(command, MessageContext.Local() with { CorrelationId = korrelation });
        return korrelation;
    }

    /// <summary>Die erste Nachricht mit dieser Korrelation (Event oder CommandFailed) — die Quittung des Commands.</summary>
    public Task<Empfangen> QuittungAsync(string korrelation, TimeSpan frist) =>
        WarteAufAsync(e => e.Kontext.CorrelationId == korrelation, frist, $"keine Quittung für Korrelation {korrelation}");

    /// <summary>Stellt eine Query über die Query-Brücke und wartet auf die nächste Antwort eines der Typen.</summary>
    public async Task<object> FrageAsync(IQuery query, TimeSpan frist, params Type[] antwortTypen)
    {
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var abos = antwortTypen.Append(typeof(QueryFailed))
            .Select(t => Bus.Subscribe(t, (m, _) => tcs.TrySetResult(m))).ToList();
        try
        {
            Bus.Publish(query, MessageContext.Local());
            var fertig = await Task.WhenAny(tcs.Task, Task.Delay(frist));
            if (fertig != tcs.Task) throw new TimeoutException($"keine Antwort auf {query.GetType().Name} nach {frist.TotalSeconds:0} s");
            return await tcs.Task;
        }
        finally { foreach (var a in abos) a.Dispose(); }
    }

    public async Task<Empfangen> WarteAufAsync(Func<Empfangen, bool> bedingung, TimeSpan frist, string meldung)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < frist)
        {
            if (_empfangen.FirstOrDefault(bedingung) is { } e) return e;
            await Task.Delay(50);
        }
        throw new TimeoutException($"{meldung} (nach {frist.TotalSeconds:0} s); empfangen: "
                                   + string.Join(", ", _empfangen.Select(x => $"{x.Nachricht.GetType().Name}[{x.Kontext.CorrelationId}]")));
    }

    public async ValueTask DisposeAsync()
    {
        await _sp.GetRequiredService<ConnectionModule>().DisposeAsync();
        await _sp.DisposeAsync();
    }
}
