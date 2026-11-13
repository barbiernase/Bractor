using System.Security.Cryptography;
using System.Text;
using Abstractions;
using Infrastructure.Quellen;
using Microsoft.Extensions.Logging;

namespace Client.Infrastructure.Quellen;

/// <summary>Die Antwort der Annahme auf einen Aufruf: der HTTP-Status, die Kennung des Absenders, das Element (seine Kennung im System) oder der Grund.</summary>
public sealed record AnnahmeAntwort(int Status, string? Kennung = null, Guid? Element = null, string? Fehler = null)
{
    public bool Ok => Status == 202;
}

/// <summary>
/// Die ANNAHME im Rechnerdienst (docs/04 §4.7, docs/10 E18): die annehmenden Quellen (<c>IAnnahme&lt;Aufruf&gt;</c>, der Webhook)
/// der App-Instanzen dieses Rechners — <c>POST /eingang/webhook/&lt;Pfad&gt;</c> auf dem Port der Instanz. Je Aufruf:
/// <list type="number">
/// <item>die Quelle über den Pfad und den Port, auf dem er ankam (sonst 404);</item>
/// <item>nennt die Quelle einen Schlüssel, muss der Kopf <c>schluessel</c> ihn tragen (zeitkonstant verglichen; sonst 401);</item>
/// <item>die Kennung aus dem Kopf <c>kennung</c> — Pflicht (sonst 400): sie ist die Kennung der Meldung, aus ihr folgen alle Ids;</item>
/// <item>über die <see cref="Hineingabe"/> der Instanz durabel hineingeben (derselbe Weg wie ein Läufer) — ERST DANN 202; steht die Verbindung nicht oder
///   scheitert es, 503 „später“: der Absender wiederholt mit derselben Kennung, und es entstehen dieselben Ids.</item>
/// </list>
/// </summary>
public sealed class Annahmen(ILogger? logger = null)
{
    private sealed class Stelle(EingangPlan plan)
    {
        public EingangPlan Plan { get; } = plan;
        public Hineingabe? Aktuell;
    }

    private readonly Dictionary<string, Stelle> _jePfad = new(StringComparer.Ordinal);

    /// <summary>Die Pfade mit ihrem Port.</summary>
    public IEnumerable<(string Pfad, int Port)> Pfade => _jePfad.Select(x => (x.Key, x.Value.Plan.Port));

    /// <summary>Die Ports, auf denen angenommen wird.</summary>
    public IReadOnlyCollection<int> Ports => _jePfad.Values.Select(s => s.Plan.Port).Distinct().ToList();

    /// <summary>Nimmt eine annehmende Quelle auf (vor dem Start).</summary>
    public void Nimm(EingangPlan plan) => _jePfad[Annahmevertrag.Normal(plan.Pfad)] = new Stelle(plan);

    /// <summary>Setzt die Hineingabe der Instanz (nach dem Verbinden) bzw. nimmt sie weg (null, die Verbindung ist fort).</summary>
    public void Setze(string instanz, Func<EingangPlan, Hineingabe>? hineingabe)
    {
        foreach (var s in _jePfad.Values.Where(s => s.Plan.Instanz == instanz)) s.Aktuell = hineingabe?.Invoke(s.Plan);
    }

    /// <summary>Nimmt einen Aufruf an <paramref name="pfad"/> auf <paramref name="port"/> an (siehe oben); die Antwort trägt den HTTP-Status.</summary>
    public async Task<AnnahmeAntwort> NimmAsync(string pfad, int port, string? schluessel, string? kennung, string inhalt, CancellationToken ct)
    {
        if (!_jePfad.TryGetValue(Annahmevertrag.Normal(pfad), out var s) || (port > 0 && s.Plan.Port != port))
            return new AnnahmeAntwort(404, Fehler: $"unter „{Annahmevertrag.Normal(pfad)}“ nimmt hier keine Quelle an");
        var plan = s.Plan;
        if (plan.Schluessel is { } soll && !Gleich(soll, schluessel))
            return new AnnahmeAntwort(401, Fehler: $"der Schlüssel fehlt oder stimmt nicht (Kopf „{Annahmevertrag.SchluesselKopf}“)");
        // Ohne Kennung gäbe es kein „genau einmal“: die Ids folgen aus ihr — eine Wiederholung mit neuer Kennung wäre ein neues Element.
        if (string.IsNullOrWhiteSpace(kennung))
            return new AnnahmeAntwort(400, Fehler: $"der Kopf „{Annahmevertrag.KennungKopf}“ fehlt — die Kennung des Aufrufs macht, dass eine Wiederholung einmal wirkt");
        if (s.Aktuell is not { Verbunden: true } h)
            return new AnnahmeAntwort(503, kennung, Fehler: $"{plan.Ort}: keine Verbindung zum Backend — später, mit derselben Kennung");
        try
        {
            var element = await h.GibHineinAsync(new Aufruf(kennung, inhalt), ct);
            return element is { } el
                ? new AnnahmeAntwort(202, kennung, el)
                : new AnnahmeAntwort(503, kennung, Fehler: $"{plan.Ort}: keine Übersetzung nimmt den Aufruf");
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger?.LogWarning("[Quellen] {Quelle}: Aufruf {Kennung} nicht hineingegeben ({Grund}) — später", plan.Ort, kennung, e.GetBaseException().Message);
            return new AnnahmeAntwort(503, kennung, Fehler: $"{plan.Ort}: nicht hineingegeben ({e.GetBaseException().Message}) — später, mit derselben Kennung");
        }
    }

    /// <summary>Vergleicht zwei Schlüssel in fester Zeit (über ihre Hashes — auch die Länge verrät nichts).</summary>
    public static bool Gleich(string soll, string? ist) =>
        ist is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(soll)), SHA256.HashData(Encoding.UTF8.GetBytes(ist)));
}
