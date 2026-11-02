using System.Collections.Concurrent;
using System.Collections.Immutable;
using Google.Protobuf.WellKnownTypes;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>Wie ein Eingang zu einer Instanz eines Graphen kommt (Naht; live <see cref="FunktionsWege"/>).</summary>
public interface IFunktionsWege
{
    /// <summary>
    /// Stellt den Eingang an die Instanz <see cref="EingangZustellen.Instanz"/> zu — an einen Knoten, der sie hält (der eigene zuerst, sonst
    /// reihum). <c>null</c> = keine Antwort in der Frist (Knoten fort, Frist um); der Absender versucht es wieder, mit derselben Kennung.
    /// </summary>
    Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct);

    /// <summary>Ein Hinweis: verlierbar, ohne Antwort, an einen Knoten, der die Instanz hält.</summary>
    void Melde(EingangZustellen hinweis);
}

/// <summary>
/// Welche Knoten welche Instanzen halten (docs/04 §4.4): jeder Knoten meldet im Gossip-Zustand des Clusters (Schlüssel <see cref="Schluessel"/>) die
/// Instanzen, deren Graph-Actor bei ihm Eingänge nimmt; die Wege lesen daraus je Instanz die Adressen. Keine virtuellen Actors: ein Graph-Actor hat
/// einen festen Namen je Knoten (<see cref="FunktionenWurzel.Name(string)"/>).
/// </summary>
public sealed class FunktionsOrte(Cluster? cluster)
{
    public const string Schluessel = "funktionen";

    private volatile IReadOnlyDictionary<string, string[]> _adressen = ImmutableDictionary<string, string[]>.Empty;
    private readonly ConcurrentDictionary<string, int> _reihum = new();
    private volatile string[] _eigene = System.Array.Empty<string>();

    /// <summary>Die Adresse dieses Knotens.</summary>
    public string? Eigene => cluster?.System.Address;

    /// <summary>Meldet die Funktionen, deren Parent auf diesem Knoten lebt.</summary>
    public void Halte(IEnumerable<string> funktionen)
    {
        _eigene = funktionen.OrderBy(f => f, StringComparer.Ordinal).ToArray();
        cluster?.Gossip.SetState(Schluessel, new StringValue { Value = string.Join(",", _eigene) });
    }

    /// <summary>Hält dieser Knoten die Funktion?</summary>
    public bool HaeltSelbst(string funktion) => Array.IndexOf(_eigene, funktion) >= 0;

    /// <summary>Liest den Gossip-Zustand neu: je Funktion die Adressen der Knoten, die sie halten.</summary>
    public async Task AktualisiereAsync()
    {
        if (cluster is null) return;
        var je = await cluster.Gossip.GetState<StringValue>(Schluessel);
        var mitglieder = cluster.MemberList.GetAllMembers().ToDictionary(m => m.Id, m => m.Address);
        var neu = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (mitglied, wert) in je)
        {
            if (!mitglieder.TryGetValue(mitglied, out var adresse)) continue;   // der Knoten ist fort
            foreach (var f in wert.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                (neu.TryGetValue(f, out var l) ? l : neu[f] = new List<string>()).Add(adresse);
        }
        _adressen = neu.ToDictionary(x => x.Key, x => x.Value.OrderBy(a => a, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Die Adresse für den nächsten Eingang an <paramref name="funktion"/>: der eigene Knoten, wenn er sie hält, sonst reihum; null = niemand.</summary>
    public string? Waehle(string funktion, bool eigeneZuerst = true)
    {
        if (eigeneZuerst && HaeltSelbst(funktion) && Eigene is { } selbst) return selbst;
        if (!_adressen.TryGetValue(funktion, out var alle) || alle.Length == 0) return HaeltSelbst(funktion) ? Eigene : null;
        var n = _reihum.AddOrUpdate(funktion, 0, (_, i) => i + 1);
        return alle[(int)((uint)n % (uint)alle.Length)];
    }
}

/// <summary>Die Wege über den Boten: an den Graph-Actor der Instanz auf dem gewählten Knoten; „später“ oder keine Antwort → beim nächsten Mal ein anderer.</summary>
public sealed class FunktionsWege(Bote bote, FunktionsOrte orte, ILogger? logger = null) : IFunktionsWege
{
    public async Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct)
    {
        var adresse = orte.Waehle(eingang.Instanz);
        if (adresse is null)
        {
            logger?.LogDebug("[Funktionen] {Instanz} hält kein Knoten — {Kennung} später", eingang.Instanz, eingang.Kennung);
            return new EingangQuittung(eingang.Kennung, Quittungsart.Spaeter);
        }
        var q = await bote.FrageAnKnotenAsync<EingangQuittung>(adresse, FunktionenWurzel.Name(eingang.Instanz), eingang, frist, ct);
        return q;
    }

    public void Melde(EingangZustellen hinweis)
    {
        if (orte.Waehle(hinweis.Instanz) is { } adresse) bote.MeldeAnKnoten(adresse, FunktionenWurzel.Name(hinweis.Instanz), hinweis);
    }
}

/// <summary>Liest die Orte jede Sekunde aus dem Gossip-Zustand neu (die Mitgliedschaft ändert sich mit Knoten und Sperren).</summary>
public sealed class FunktionsOrteTakt(FunktionsOrte orte, ILogger<FunktionsOrteTakt>? logger = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await orte.AktualisiereAsync(); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger?.LogDebug(ex, "[Funktionen] Orte nicht gelesen"); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
