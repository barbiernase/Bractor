using Abstractions;
using Infrastructure.Projections;   // IClusterKindContributor
using Proto;
using Proto.Cluster;

namespace Infrastructure.Quellen;

/// <summary>Ein Knoten bittet darum, die Schleife einer Quelle zu halten (oder verlängert sein Halten).</summary>
public sealed record HalteQuelle(string Knoten) : IWireMessage;

/// <summary>Antwort: hält dieser Knoten die Quelle (jetzt) — und bis wann.</summary>
public sealed record QuelleGehalten(bool Haelt, string Halter) : IWireMessage;

/// <summary>
/// Der store-freie Kern von <c>Laufmodus: EinmalImCluster</c>: eine Lease. Der erste Knoten bekommt sie, nur er verlängert sie; läuft
/// sie ab (Knoten weg), bekommt sie der nächste Fragende. Ein kurzer Doppellauf beim Wechsel ist harmlos — die Vorgangs-Id ist
/// deterministisch, StartStream gewinnt genau einmal.
/// </summary>
public sealed class QuellenLease(TimeSpan dauer)
{
    private string? _halter;
    private DateTimeOffset _bis;

    public QuelleGehalten Halte(string knoten, DateTimeOffset jetzt)
    {
        if (_halter == null || _halter == knoten || jetzt >= _bis)
        {
            _halter = knoten;
            _bis = jetzt + dauer;
        }
        return new QuelleGehalten(_halter == knoten, _halter);
    }
}

/// <summary>Cluster-Identität <c>quelle/&lt;Typ&gt;</c>: hält die Lease einer Quelle; wandert bei Ausfall eines Knotens mit.</summary>
public sealed class QuellenHalterActor : IActor
{
    public const string KindName = "quelle";
    public static readonly TimeSpan Dauer = TimeSpan.FromSeconds(15);
    private readonly QuellenLease _lease = new(Dauer);

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is HalteQuelle h) context.Respond(_lease.Halte(h.Knoten, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public static ClusterIdentity Identitaet(QuellTyp q) => ClusterIdentity.Create(q.Abschnitt, KindName);
}

internal sealed class QuellenHalterKind : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
        => new(QuellenHalterActor.KindName, Props.FromProducer(() => new QuellenHalterActor()));
}
