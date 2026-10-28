using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>Die Frage an einen Aggregat-Actor (Plan Pub/Sub-Neustrukturierung §2.6): die Query, nach der seine <c>Frage</c> antwortet, und ihr Ziel
/// (die Id aus ihrem Glied <c>Id&lt;TAggregat&gt;</c>) — eine Frage an eine fremde Identität beantwortet der Actor nicht.</summary>
public sealed record AggregatFrage(IQuery Query, Guid Ziel) : IWireMessage;

/// <summary>
/// Die Antwort des Aggregat-Actors: die Antwort seiner <c>Frage</c> und die Version, auf der er sie gab — der Stand, auf dem er den nächsten Command
/// entschieden hätte (der OCC-Stand für einen Client). <see cref="Fehler"/> statt einer Antwort: falsch geroutet, nicht rehydrierbar, die Frage warf.
/// </summary>
public sealed record AggregatAntwort(IQueryResponse? Antwort, int Version, string? Fehler = null) : IWireMessage;

/// <summary>
/// Der Weg einer Query zu ihrem Aggregat (Plan Pub/Sub-Neustrukturierung §2.6): eine Frage des <see cref="Bote"/> mit Pflicht-Frist an die
/// Cluster-Identität aus der Id der Query. Der generierte Query-Dienst ruft ihn für jede Query, deren Ziel ein Aggregat ist (R4); die Antwort trägt
/// als Stand je gezeigter Id genau dieses Aggregat mit seiner Version — exakt, ohne Warten auf eine Projektion.
/// </summary>
public sealed class AggregatAbfrage(Proto.ActorSystem system, ILogger<AggregatAbfrage>? logger = null)
{
    /// <summary>Wie lange eine Frage auf den Aggregat-Actor wartet (Aktivierung samt Rehydration und ein laufender Commit eingeschlossen).</summary>
    public static readonly TimeSpan Frist = TimeSpan.FromSeconds(10);

    private Bote? _bote;   // erst bei der ersten Frage: der Cluster ist bei der Registrierung noch nicht fertig

    public async Task<QueryResponse<IQueryResponse>> FrageAsync(Guid id, string aggregat, IQuery query, CancellationToken ct = default)
    {
        _bote ??= new Bote(system.Cluster(), logger);
        var antwort = await _bote.FrageAsync<AggregatAntwort>(ClusterIdentity.Create(id.ToString(), aggregat), new AggregatFrage(query, id), Frist, ct)
            ?? throw new TimeoutException($"{aggregat}/{id} hat die Query {query.GetType().Name} nicht in {Frist.TotalSeconds:0} s beantwortet");
        if (antwort.Fehler != null || antwort.Antwort == null)
            throw new InvalidOperationException($"{aggregat}/{id} beantwortet {query.GetType().Name} nicht: {antwort.Fehler ?? "keine Antwort"}");
        return new QueryResponse<IQueryResponse>
        {
            Data = antwort.Antwort,
            Deps = new[] { new AggregateMeta(id, aggregat, antwort.Version) },
        };
    }
}
