using Abstractions;
using Infrastructure.Akteure;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace Infrastructure.Verteilen;

/// <summary>
/// Schickt, was an Clients geht, an die Verteiler (<see cref="VerteilerActor"/>): je Aufruf (= je Commit) eine Nachricht je Knoten, nur mit
/// Typen, die irgendein Client hören darf (<see cref="Hoerbar"/>, aus den generierten Tabellen der Akteure und Client-Verträge); gezielte
/// Envelopes (<see cref="EventEnvelope.ZielSitzung"/>) nur an den Knoten, den die Sitzungs-Id nennt. Verlierbar, ohne Warten (Invariante 6).
/// </summary>
public sealed class Verteilung
{
    private readonly Cluster _cluster;
    private readonly Bote _bote;
    private readonly IReadOnlySet<Type> _hoerbar;

    public Verteilung(Cluster cluster, IReadOnlySet<Type> hoerbar, ILogger? logger = null)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _bote = new Bote(cluster, logger);
        _hoerbar = hoerbar;
    }

    /// <summary>
    /// Die Typen, die irgendein Client hören darf: das Gehörte jedes Akteurs (abgeleitet) und jedes Client-Vertrags (<c>IHoert</c>). Nur sie
    /// gehen als Push hinaus.
    /// </summary>
    public static IReadOnlySet<Type> Hoerbar(IReadOnlyDictionary<string, AkteurRechte> akteure, IReadOnlyDictionary<string, ClientVertrag> clients) =>
        akteure.Values.SelectMany(a => a.Hoert).Concat(clients.Values.SelectMany(c => c.Hoert)).ToHashSet();

    /// <summary>Die Sitzungs-Id: der Knoten, auf dem sie lebt, und ihre Nummer dort — so findet eine gezielte Antwort ihren Knoten.</summary>
    public static string SitzungsId(string knoten, int nummer) => $"{knoten}#{nummer:D4}";

    /// <summary>Der Knoten einer Sitzungs-Id (null = keine Sitzungs-Id dieser Form).</summary>
    public static string? Knoten(string sitzungsId)
    {
        var i = sitzungsId.LastIndexOf('#');
        return i > 0 ? sitzungsId[..i] : null;
    }

    /// <summary>
    /// „P hat S weiterverarbeitet“ (nach jedem Commit einer Projektion, der Events anwandte): an den Verteiler jedes Knotens, der es an die
    /// Sitzungen weiterreicht, die eine Antwort mit einem dieser Streams (bzw. ohne Ids von P) halten — sie fragen neu. Verlierbar.
    /// </summary>
    public void MeldeVerarbeitet(string projektion, IReadOnlyCollection<Guid> streams)
    {
        var nachricht = new Verarbeitet(projektion, streams.ToArray());
        foreach (var m in _cluster.MemberList.GetAllMembers())
            _bote.MeldeAnKnoten(m.Address, VerteilerActor.Name, nachricht);
    }

    /// <summary>Ein Envelope (gezielt oder an alle, die ihn hören).</summary>
    public void Melde(EventEnvelope envelope) => Melde([envelope]);

    /// <summary>Die Envelopes eines Commits, in Log-Reihenfolge.</summary>
    public void Melde(IReadOnlyList<EventEnvelope> envelopes)
    {
        List<EventEnvelope>? alle = null;
        Dictionary<string, List<EventEnvelope>>? gezielt = null;
        foreach (var e in envelopes)
        {
            if (e.ZielSitzung is { } ziel)
            {
                if (Knoten(ziel) is not { } knoten) continue;
                gezielt ??= new Dictionary<string, List<EventEnvelope>>(StringComparer.Ordinal);
                if (!gezielt.TryGetValue(knoten, out var liste)) gezielt[knoten] = liste = new List<EventEnvelope>();
                liste.Add(e);
            }
            else if (_hoerbar.Contains(e.Payload.GetType()))
                (alle ??= new List<EventEnvelope>()).Add(e);
        }

        if (gezielt is not null)
            foreach (var (knoten, liste) in gezielt)
                _bote.MeldeAnKnoten(knoten, VerteilerActor.Name, new Verteile(liste));
        if (alle is not null)
        {
            var nachricht = new Verteile(alle);
            foreach (var m in _cluster.MemberList.GetAllMembers())
                _bote.MeldeAnKnoten(m.Address, VerteilerActor.Name, nachricht);
        }
    }
}
