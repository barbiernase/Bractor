using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor, WakeAck
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der Vermittler einer Funktion als virtueller Cluster-Actor (Identität = voller Name der Funktion). Er hält die offenen Eingänge
/// (<see cref="Vermittlung{TArbeit}"/>); eine <see cref="HoleEingaenge"/>-Anfrage ohne Arbeit PARKT er bis zur Wartezeit (Long-Poll) und bedient
/// sie, sobald ein Angebot kommt — so wartet kein Träger im Takt, und neue Arbeit geht sofort raus.
/// </summary>
public sealed class FunktionsVermittlerActor : RahmenActor
{
    public const string KindName = "funktions-vermittler";

    private sealed record Tick;
    private sealed record GeparktEingang(PID Antwort, HoleEingaenge Anfrage, DateTimeOffset Bis);

    private readonly Vermittlung<EingangAnbieten> _eingaenge;
    private readonly Func<DateTimeOffset> _jetzt;
    private readonly LinkedList<GeparktEingang> _geparktEingang = new();

    public FunktionsVermittlerActor(TimeSpan lease, Func<DateTimeOffset>? jetzt = null)
    {
        _eingaenge = new Vermittlung<EingangAnbieten>(lease);
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
    }

    protected override Task BeginneAsync(IContext context)
    {
        PlaneTick(context);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case EingangAnbieten a:
                _eingaenge.Biete(a);
                BedieneEingaenge(context);
                context.Respond(new WakeAck());
                break;

            case HoleEingaenge h:
                var eingaenge = _eingaenge.Hole(h.Arbeiter, Math.Max(0, h.Max), _jetzt());
                if (eingaenge.Count > 0 || h.WarteMs <= 0 || context.Sender is null)
                    context.Respond(new EingaengeZugeteilt(eingaenge));
                else
                {
                    for (var n = _geparktEingang.First; n is not null;)
                    {
                        var weiter = n.Next;
                        if (n.Value.Anfrage.Arbeiter == h.Arbeiter) _geparktEingang.Remove(n);
                        n = weiter;
                    }
                    _geparktEingang.AddLast(new GeparktEingang(context.Sender, h, _jetzt().AddMilliseconds(h.WarteMs)));
                }
                break;

            case ArbeitLebt l:
                _eingaenge.Lebenszeichen(l.Vorgang, l.Arbeiter, _jetzt());
                context.Respond(new WakeAck());
                break;

            case ArbeitErledigt e:
                _eingaenge.Erledigt(e.Vorgang);
                context.Respond(new WakeAck());
                break;

            case TraegerAbgemeldet t:
                // Seine Adressen sind frei (A8) — wer jetzt wartet, darf sie nehmen.
                _eingaenge.Abmelden(t.Arbeiter);
                for (var n = _geparktEingang.First; n is not null;)
                {
                    var weiter = n.Next;
                    if (n.Value.Anfrage.Arbeiter == t.Arbeiter) _geparktEingang.Remove(n);
                    n = weiter;
                }
                BedieneEingaenge(context);
                if (context.Sender is not null) context.Respond(new WakeAck());
                break;

            case ListeOffene:
                context.Respond(new OffeneAuftraege(_eingaenge.Offen().ToList()));
                break;

            case Tick:
                // abgelaufene Leases zurückgeben (und neu verteilen), abgelaufene Wartende leer beantworten
                _eingaenge.GibAbgelaufeneZurück(_jetzt());
                BedieneEingaenge(context);
                var jetzt = _jetzt();
                for (var n = _geparktEingang.First; n is not null;)
                {
                    var weiter = n.Next;
                    if (n.Value.Bis <= jetzt)
                    {
                        context.Send(n.Value.Antwort, new EingaengeZugeteilt(Array.Empty<EingangAnbieten>()));
                        _geparktEingang.Remove(n);
                    }
                    n = weiter;
                }
                PlaneTick(context);
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Geparkte Hol-Anfragen nach Eingängen der Reihe nach bedienen, solange Arbeit da ist — jede wird versucht: was eine nicht nehmen darf (die Adresse
    /// gehört einem anderen Träger, A8), nimmt vielleicht die nächste.
    /// </summary>
    private void BedieneEingaenge(IContext context)
    {
        for (var n = _geparktEingang.First; n is not null && _eingaenge.Wartend > 0;)
        {
            var weiter = n.Next;
            var los = _eingaenge.Hole(n.Value.Anfrage.Arbeiter, n.Value.Anfrage.Max, _jetzt());
            if (los.Count > 0)
            {
                _geparktEingang.Remove(n);
                context.Send(n.Value.Antwort, new EingaengeZugeteilt(los));
            }
            n = weiter;
        }
    }

    private void PlaneTick(IContext context)
        => Neben(context, stopp => Task.Delay(TimeSpan.FromSeconds(1), stopp), _ =>
        {
            if (!Stopp.IsCancellationRequested) context.Send(context.Self, new Tick());
            return Task.CompletedTask;
        });

    /// <summary>Die Cluster-Identität des Vermittlers einer Funktion.</summary>
    public static ClusterIdentity Identitaet(Type funktion) => ClusterIdentity.Create(funktion.FullName ?? funktion.Name, KindName);
}

/// <summary>Das Cluster-Kind des Vermittlers (eine Instanz je Funktion).</summary>
internal sealed class FunktionsVermittlerKind : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
        => new(FunktionsVermittlerActor.KindName, Props.FromProducer(() => new FunktionsVermittlerActor(FunktionsAnbieter.Lease)));
}

/// <summary>Die Vermittler im Cluster: Lease und Einblick.</summary>
public static class FunktionsAnbieter
{
    /// <summary>Wie lange ein geholter Eingang ohne Lebenszeichen einem Träger gehört.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Einblick (Werkbank P4.7): die offenen Eingänge je Funktion — vom Vermittler jeder Funktion, bounded. Ein Vermittler, der nicht antwortet,
    /// fehlt in der Liste (er ist nicht maßgeblich; der Funktions-Actor bietet ohnehin neu an).
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<OffenerAuftrag>>> ListeOffeneAsync(
        ActorSystem system, IEnumerable<Type> funktionen, CancellationToken ct)
    {
        var bote = new Bote(system.Cluster());
        var anfragen = funktionen.Distinct().Select(async f =>
            (f.Name, (await bote.FrageAsync<OffeneAuftraege>(FunktionsVermittlerActor.Identitaet(f), new ListeOffene(), TimeSpan.FromSeconds(3), ct))?.Auftraege));
        return (await Task.WhenAll(anfragen)).Where(x => x.Item2 != null).ToDictionary(x => x.Item1, x => x.Item2!);
    }
}
