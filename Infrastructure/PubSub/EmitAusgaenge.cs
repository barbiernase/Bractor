using Abstractions;

namespace Infrastructure.PubSub;

/// <summary>
/// Die Ausgänge der Commands, die ein Konsument in EINER Weckung ausgibt (docs/04 §4.4). Der Adapter legt je Weckung eine an und
/// reicht sie mit dem gebundenen Token in den Dispatch; das Ausgabe-Routing (<see cref="HandlerOutputRouter.EmitFor"/>) startet jeden
/// Command sofort und trägt seinen Ausgang hier ein — die Commands eines Tails gehen also parallel hinaus. Nach dem Tail wartet der Adapter
/// sie gemeinsam ab (<see cref="AlleEntschiedenAsync"/>): der Emittenten-Cursor rückt nur vor, wenn jeder Command zugestellt oder
/// abgelehnt ist (G-2). Kein Emit läuft mehr losgelöst vom Konsumenten.
/// </summary>
public sealed class EmitAusgaenge
{
    private readonly List<Task<EmitAusgang>> _offen = new();

    public EmitAusgaenge(CancellationToken token) => Token = token;

    /// <summary>Das Token der Weckung — bindet jeden Command-Send (CQRS021); die Sendefrist des Emitters begrenzt zusätzlich.</summary>
    public CancellationToken Token { get; }

    public void Erfasse(Task<EmitAusgang> ausgang)
    {
        lock (_offen) _offen.Add(ausgang);
    }

    /// <summary>Wartet alle erfassten Commands ab; true, wenn keiner „unbekannt“ blieb.</summary>
    public async Task<bool> AlleEntschiedenAsync()
    {
        Task<EmitAusgang>[] alle;
        lock (_offen) alle = _offen.ToArray();
        if (alle.Length == 0) return true;
        var ausgaenge = await Task.WhenAll(alle);
        return ausgaenge.All(a => a.IstEntschieden());
    }
}
