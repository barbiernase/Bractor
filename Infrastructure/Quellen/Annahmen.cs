using System.Security.Cryptography;
using System.Text;
using Abstractions;
using Infrastructure.Eingaenge;
using Infrastructure.Emit;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Quellen;

/// <summary>Die Antwort der Annahme auf einen Aufruf: der HTTP-Status, die Kennung des Absenders, das Element (seine Kennung im System) oder der Grund.</summary>
public sealed record AnnahmeAntwort(int Status, string? Kennung = null, Guid? Element = null, string? Fehler = null)
{
    public bool Ok => Status == 202;
}

/// <summary>
/// Die ANNAHME des Hosts (docs/04 §4.7): die Eingänge aus annehmenden Quellarten (<c>IAnnahme&lt;Aufruf&gt;</c>, der Webhook) — jeder Host nimmt sie an
/// (<c>POST /eingang/webhook/&lt;Pfad&gt;</c>, <see cref="AnnahmeEndpunkt"/>), nicht ein Läufer auf einem Rechner. Je Aufruf:
/// <list type="number">
/// <item>der Eingang über den Pfad (sonst 404);</item>
/// <item>nennt der Eingang einen Schlüssel, muss der Kopf <c>schluessel</c> ihn tragen (zeitkonstant verglichen; sonst 401);</item>
/// <item>die Kennung aus dem Kopf <c>kennung</c> — Pflicht (sonst 400): sie ist die Kennung der Meldung, aus ihr folgen alle Ids; ohne sie bekäme jede
///   Wiederholung neue Ids und wirkte doppelt;</item>
/// <item>Gegendruck: sind so viele Elemente des Eingangs offen, wie er erlaubt, heißt es „später“ (503) — gezählt je Host (die Kennungen, die dieser
///   Prozess hineingab);</item>
/// <item>über die <see cref="Hineingabe"/> durabel hineingeben (derselbe Weg wie ein Läufer) — ERST DANN 202 mit der Kennung; scheitert es (Aggregat
///   unbekannt, Datenbank, kein Kopf), 503 „später“: der Absender wiederholt mit derselben Kennung, und es entstehen dieselben Ids.</item>
/// </list>
/// Store-frei prüfbar: die Hineingabe bekommt Emitter, offene Eingänge und Wege als Nähte.
/// </summary>
public sealed class Annahmen
{
    private readonly Dictionary<string, Hineingabe> _jePfad;
    private readonly ILogger? _logger;

    public Annahmen(IEnumerable<EingangPlan> eingaenge, Func<EingangPlan, Hineingabe> hineingabe, ILogger? logger = null)
    {
        _jePfad = eingaenge.Where(e => e.Annahme).ToDictionary(e => Annahmevertrag.Normal(e.Pfad), hineingabe, StringComparer.Ordinal);
        _logger = logger;
    }

    public static readonly Annahmen Keine = new([], _ => throw new InvalidOperationException());

    /// <summary>Die Pfade, unter denen der Host annimmt.</summary>
    public IReadOnlyCollection<string> Pfade => _jePfad.Keys;

    /// <summary>
    /// Die Annahmen der Umgebung mit den Diensten des Hosts: Emitter über den Boten (ein Command an ein Aggregat), offene Eingänge und Wege (ein Command
    /// ohne Ziel am Kopf). Ein Command, dessen Ausgang unbekannt bleibt, wird hier seltener versucht als im Läufer — der Absender wartet auf die Antwort.
    /// </summary>
    public static Annahmen Aus(Eingaenge eingaenge, IServiceProvider sp)
    {
        var annehmende = eingaenge.Annehmende.ToList();
        if (annehmende.Count == 0) return Keine;
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Quellen");
        var system = sp.GetRequiredService<ActorSystem>();
        var emitter = new CommandEmitter(new Bote(system.Cluster(), logger), logger);
        var koepfe = sp.GetRequiredService<Verkettung>();
        return new Annahmen(annehmende, e => new Hineingabe(e, emitter.EmitMitAntwortAsync, koepfe, sp.GetService<IOffeneEingaenge>(),
            sp.GetService<IFunktionsWege>(), logger, Versuche), logger);
    }

    /// <summary>So oft versucht die Annahme einen Command mit unbekanntem Ausgang, bevor sie „später“ antwortet.</summary>
    public const int Versuche = 3;

    /// <summary>Nimmt einen Aufruf an <paramref name="pfad"/> an (siehe oben); die Antwort trägt den HTTP-Status.</summary>
    public async Task<AnnahmeAntwort> NimmAsync(string pfad, string? schluessel, string? kennung, string inhalt, CancellationToken ct)
    {
        if (!_jePfad.TryGetValue(Annahmevertrag.Normal(pfad), out var h))
            return new AnnahmeAntwort(404, Fehler: $"unter „{Annahmevertrag.Normal(pfad)}“ nimmt kein Eingang an");
        var plan = h.Plan;
        if (plan.Schluessel is { } soll && !Gleich(soll, schluessel))
            return new AnnahmeAntwort(401, Fehler: $"der Schlüssel fehlt oder stimmt nicht (Kopf „{Annahmevertrag.SchluesselKopf}“)");
        // Ohne Kennung gäbe es kein „genau einmal“: die Ids folgen aus ihr — eine Wiederholung mit neuer Kennung wäre ein neues Element.
        if (string.IsNullOrWhiteSpace(kennung))
            return new AnnahmeAntwort(400, Fehler: $"der Kopf „{Annahmevertrag.KennungKopf}“ fehlt — die Kennung des Aufrufs macht, dass eine Wiederholung einmal wirkt");
        try
        {
            if (!await h.HatPlatzAsync(ct))
                return new AnnahmeAntwort(503, kennung, Fehler: $"{plan.Ort}: {h.Offen} Elemente offen (Grenze {plan.Grenze}) — später");
            var element = await h.GibHineinAsync(new Aufruf(kennung, inhalt), ct);
            return element is { } el
                ? new AnnahmeAntwort(202, kennung, el)
                : new AnnahmeAntwort(503, kennung, Fehler: $"{plan.Ort}: keine Übersetzung nimmt den Aufruf");
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger?.LogWarning(e, "[Quellen] {Eingang}: Aufruf {Kennung} nicht hineingegeben — später", plan.Ort, kennung);
            return new AnnahmeAntwort(503, kennung, Fehler: $"{plan.Ort}: nicht hineingegeben ({e.GetBaseException().Message}) — später, mit derselben Kennung");
        }
    }

    /// <summary>Vergleicht zwei Schlüssel in fester Zeit (über ihre Hashes — auch die Länge verrät nichts).</summary>
    public static bool Gleich(string soll, string? ist) =>
        ist is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(soll)), SHA256.HashData(Encoding.UTF8.GetBytes(ist)));
}
