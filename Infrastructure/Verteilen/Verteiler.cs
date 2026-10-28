using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Verteilen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// VERTEILEN (Plan Pub/Sub-Neustrukturierung §2.7, Phase 7): was an Clients geht — Pushes der Events, die ein Client hören darf, gezielte
// Antworten an EINE Sitzung (Ablehnung, CommandFailed), Hinweise von Clients — geht je Commit als EINE Nachricht an den Verteiler jedes Knotens
// (gezielt: nur an den Knoten, den die Sitzungs-Id nennt). Der Verteiler kennt die Sitzungen seines Knotens und was jede hört, und reicht weiter.
// Kein Broker, keine Abos im Cluster, kein Dauer-Abo auf CommandFailed. Verlierbar (Invariante 6): verlässlich liest ein Client nur ab Position.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>An den Verteiler eines Knotens: die Envelopes eines Commits (bzw. eine gezielte Antwort), in Log-Reihenfolge.</summary>
public sealed record Verteile(IReadOnlyList<EventEnvelope> Envelopes) : IWireMessage;

/// <summary>Die Sitzung meldet sich beim Verteiler ihres Knotens an (beim Start ohne Gehörtes, nach dem Handshake mit ihrer wirksamen Liste).</summary>
public sealed record SitzungMeldetAn(string SitzungsId, PID Sitzung, IReadOnlySet<Type> Hoert, Rueckstau Rueckstau);

/// <summary>Vom Verteiler an eine Sitzung: was sie aus einem Commit hört bzw. was ihr gilt.</summary>
public sealed record Zustellung(IReadOnlyList<EventEnvelope> Envelopes);

/// <summary>
/// Wie viele Zustellungen einer Sitzung noch nicht geschrieben sind. Liest ein Client nicht schnell genug, verwirft der Verteiler über der
/// <see cref="Grenze"/> — statt die Mailbox der Sitzung ohne Ende wachsen zu lassen. Der Verteiler zählt hoch, die Sitzung nach dem Schreiben herunter.
/// </summary>
public sealed class Rueckstau
{
    /// <summary>So viele Zustellungen dürfen offen sein.</summary>
    public const int Grenze = 1_000;

    private int _offen;
    private long _verworfen;

    /// <summary>Darf noch eine Zustellung hinein? Wenn ja, zählt sie als offen.</summary>
    public bool Nimm()
    {
        if (Interlocked.Increment(ref _offen) <= Grenze) return true;
        Interlocked.Decrement(ref _offen);
        Interlocked.Increment(ref _verworfen);
        return false;
    }

    /// <summary>Eine Zustellung ist geschrieben (oder verworfen, weil der Stream zu ist).</summary>
    public void Erledigt() => Interlocked.Decrement(ref _offen);

    /// <summary>Wie viele Zustellungen bisher verworfen wurden.</summary>
    public long Verworfen => Interlocked.Read(ref _verworfen);
}

/// <summary>
/// Der Verteiler eines Knotens (lokaler Actor mit festem Namen <see cref="Name"/>): Tabelle Typ → Sitzungen dieses Knotens, dazu Sitzungs-Id →
/// Sitzung für gezielte Antworten. Beobachtet jede Sitzung (<c>Watch</c>) und trägt sie aus, wenn sie endet. Fasst je Commit und Sitzung zu einer
/// Zustellung zusammen; über dem <see cref="Rueckstau"/> einer Sitzung verwirft er.
/// </summary>
public sealed class VerteilerActor : RahmenActor
{
    /// <summary>Der feste Name des Verteilers auf jedem Knoten.</summary>
    public const string Name = "verteiler";

    private sealed record Eintrag(PID Sitzung, IReadOnlySet<Type> Hoert, Rueckstau Rueckstau);

    private readonly Dictionary<string, Eintrag> _sitzungen = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, HashSet<string>> _hoerer = new();
    private readonly Dictionary<PID, string> _nachPid = new();
    private readonly ILogger? _logger;

    public VerteilerActor(ILogger? logger = null) : base(logger) => _logger = logger;

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case SitzungMeldetAn a:
                MeldeAn(context, a);
                break;
            case Terminated t when _nachPid.Remove(t.Who, out var id):
                TrageAus(id);
                break;
            case Verteile v:
                Verteile(context, v.Envelopes);
                break;
        }
        return Task.CompletedTask;
    }

    private void MeldeAn(IContext context, SitzungMeldetAn a)
    {
        if (_sitzungen.ContainsKey(a.SitzungsId)) TrageAus(a.SitzungsId);
        else context.Watch(a.Sitzung);
        _sitzungen[a.SitzungsId] = new Eintrag(a.Sitzung, a.Hoert, a.Rueckstau);
        _nachPid[a.Sitzung] = a.SitzungsId;
        foreach (var typ in a.Hoert)
        {
            if (!_hoerer.TryGetValue(typ, out var menge)) _hoerer[typ] = menge = new HashSet<string>(StringComparer.Ordinal);
            menge.Add(a.SitzungsId);
        }
    }

    private void TrageAus(string id)
    {
        if (!_sitzungen.Remove(id, out var e)) return;
        foreach (var typ in e.Hoert)
            if (_hoerer.TryGetValue(typ, out var menge) && menge.Remove(id) && menge.Count == 0) _hoerer.Remove(typ);
    }

    private void Verteile(IContext context, IReadOnlyList<EventEnvelope> envelopes)
    {
        Dictionary<string, List<EventEnvelope>>? je = null;
        void Fuer(string id, EventEnvelope e)
        {
            je ??= new Dictionary<string, List<EventEnvelope>>(StringComparer.Ordinal);
            if (!je.TryGetValue(id, out var liste)) je[id] = liste = new List<EventEnvelope>();
            liste.Add(e);
        }

        foreach (var e in envelopes)
        {
            if (e.ZielSitzung is { } ziel)
            {
                if (_sitzungen.ContainsKey(ziel)) Fuer(ziel, e);
            }
            else if (_hoerer.TryGetValue(e.Payload.GetType(), out var hoerer))
                foreach (var id in hoerer) Fuer(id, e);
        }
        if (je is null) return;

        foreach (var (id, liste) in je)
        {
            var eintrag = _sitzungen[id];
            if (eintrag.Rueckstau.Nimm())
                context.Send(eintrag.Sitzung, new Zustellung(liste));
            else if (eintrag.Rueckstau.Verworfen % 100 == 1)
                _logger?.LogWarning("[Verteiler] Sitzung {Sitzung} liest nicht nach — {Verworfen} Zustellungen verworfen (über {Grenze} offen)",
                    id, eintrag.Rueckstau.Verworfen, Rueckstau.Grenze);
        }
    }
}

/// <summary>Spawnt beim Start den Verteiler dieses Knotens unter seinem festen Namen (nach dem Cluster-Start, vor den ersten Sitzungen).</summary>
public sealed class VerteilerStart(ActorSystem system, ILogger<VerteilerActor>? logger = null) : Microsoft.Extensions.Hosting.IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        system.Root.SpawnNamed(Props.FromProducer(() => new VerteilerActor(logger)), VerteilerActor.Name);
        Console.WriteLine($"  ✓ Verteiler {system.Address}/{VerteilerActor.Name}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
