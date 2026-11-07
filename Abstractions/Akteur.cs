namespace Abstractions;

/// <summary>
/// Ein AKTEUR — ein Domänen-Experte draußen (<c>docs/konzept-akteure.md</c> §2): was er hineingibt, steht als <see cref="IDarf{T}"/>, was zu ihm
/// hinkommt, als <see cref="IHoert{T}"/> — Code-Fakten, die Generator, Extractor und Editor aus der Basisliste lesen:
/// <code>public sealed record Klassifizierer : IAkteur, IDarf&lt;KlassifiziereBildPaarDurchKi&gt;, IHoert&lt;ImagePairKomplett&gt;, IHoert&lt;ModellAktiviert&gt;;</code>
/// Ein Command darf bei mehreren Akteuren stehen. Was eine Funktion oder eine Frist daraus erzeugt, trägt den Akteur der Kette — dafür gibt es kein
/// weiteres Wort. Ein Akteur ist immer ein Record, nie ein Dienst.
/// </summary>
public interface IAkteur { }

/// <summary>
/// Ein CLIENT — die Software an der Leitung (Python-Worker, Blazor-Arbeitsplatz …, <c>docs/konzept-akteure.md</c> §4): ein Record, der nur nennt, WEN
/// er verkörpert (<see cref="IVerkoerpert{TAkteur}"/>):
/// <code>public sealed record Arbeitsplatz : IClient, IVerkoerpert&lt;Inspekteur&gt;, IVerkoerpert&lt;KIOperator&gt;;</code>
/// Was über seine Leitung geht, sind die <c>IDarf</c> und <c>IHoert</c> seiner Akteure. Wirksam ist an jeder Sitzung „verkörpert ∩ Token“, für beide
/// Richtungen. Handshake-Name = Record-Name, der Hash folgt aus seinen Akteuren. Die client-INTERNEN Commands/Events (Intents, ClientEvents,
/// Python-State) stehen hier nie.
/// </summary>
public interface IClient { }

/// <summary>
/// Diese Client-Assembly IST der Client <typeparamref name="TClient"/> (<c>[assembly: IstClient&lt;Arbeitsplatz&gt;]</c>, docs/konzept-akteure.md §4):
/// der Client-Generator nimmt Handshake-Name, Hash und die gehörten Typen aus den Akteuren des Clients, leitet nur die Commands weiter und stellt nur
/// die Queries, die ein verkörperter Akteur darf. Der Analyzer hält den Client-Code dagegen (CQRS093–097).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class IstClientAttribute<TClient> : Attribute where TClient : IClient { }

/// <summary>Am Client (<see cref="IClient"/>): er spricht für den Akteur <typeparamref name="TAkteur"/>. Befugt ist der Akteur, nicht die Software.</summary>
public interface IVerkoerpert<TAkteur> where TAkteur : IAkteur { }

/// <summary>
/// Am Akteur (<see cref="IAkteur"/>): zu ihm kommt <typeparamref name="T"/>. Ein Command ohne Ziel ist ARBEIT — die Werkstatt gibt ihn genau einer
/// Sitzung, die den Akteur verkörpert, und stellt ihn erneut zu, bis eine Antwort kommt (die erste gewinnt; die Antwort sind Commands über sein
/// <c>IDarf</c>). Ein Event oder ein Hinweis ist KENNTNIS — gepusht, verlierbar, ohne Ausgabe (docs/konzept-akteure.md §5); verlässlich liest ein
/// Client nur über „lies das Log ab Position“.
/// </summary>
public interface IHoert<T> { }

/// <summary>
/// Der Akteur darf <typeparamref name="T"/> in das System hineingeben — EIN Wort für alles, was hineingeht: ein Command (auslösen; an ein Aggregat
/// oder den Kopf eines Graphen), eine Query (fragen), ein Hinweis. Ein Client, der den Akteur verkörpert (<see cref="IVerkoerpert{TAkteur}"/>), darf
/// genau das über seine Leitung — die Art folgt aus dem Marker von <typeparamref name="T"/> (<see cref="Clientvertragsregeln.Art"/>). Andere
/// <typeparamref name="T"/> meldet der Analyzer CQRS058.
/// </summary>
public interface IDarf<T> { }

/// <summary>
/// „Im Auftrag von": der Akteur der Kette — gesetzt um die Arbeit eines Konsumenten (Funktion, Uhr) aus dem Akteur seines Eingangs, gelesen vom
/// Emit-Primitiv (stempelt ihn als <c>UserId</c>). Fluss-lokal (<see cref="System.Threading.AsyncLocal{T}"/>): er reist durch die awaits und
/// endet mit dem Aufruf. Der Fachcode sieht ihn nie.
/// </summary>
public static class ImAuftrag
{
    /// <summary>Header im Event-Log, der den Akteur je Event trägt (Kausalkette, wie Correlation) — Betrieb, nie Domäne.</summary>
    public const string Header = "akteur";

    /// <summary>Kein Akteur (reine Automation ohne Herkunft) — wird nicht ins Log geschrieben.</summary>
    public const string Ohne = "system";

    /// <summary>Ist <paramref name="userId"/> ein echter Akteur (nicht leer, nicht <see cref="Ohne"/>/„anonymous")?</summary>
    public static bool IstAkteur(string? userId) => !string.IsNullOrEmpty(userId) && userId != Ohne && userId != "anonymous";

    private static readonly System.Threading.AsyncLocal<string?> _akteur = new();

    /// <summary>Der Akteur des laufenden Handle-Aufrufs (null = kein Akteur, z. B. reine Automation).</summary>
    public static string? Akteur => _akteur.Value;

    /// <summary>Setzt den Akteur bis zum Dispose (stellt den vorherigen wieder her).</summary>
    public static IDisposable Von(string akteur)
    {
        var vorher = _akteur.Value;
        _akteur.Value = akteur;
        return new Zurueck(vorher);
    }

    private sealed class Zurueck(string? vorher) : IDisposable
    {
        public void Dispose() => _akteur.Value = vorher;
    }
}
