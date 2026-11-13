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
/// Richtungen. Umgesetzt wird ein Client von APPS (<see cref="IApp{TClient}"/>) — sie tragen den Vertrag an der Leitung (Handshake-Name, Hash). Die
/// client-INTERNEN Commands/Events (Intents, ClientEvents, Python-State) stehen hier nie.
/// </summary>
public interface IClient { }

/// <summary>
/// Eine APP — eine konkrete Software, die einen Client umsetzt (docs/konzept-akteure.md §4, docs/10 E18): sie ist der Vertrag, gegen den
/// verbindlich entwickelt wird. Je Nachricht des Clients (die <c>IDarf</c> seiner Akteure) setzt sie entweder die App selbst um (der Standard) oder eine
/// ihrer QUELLEN: jeder Parameter des Records ist eine Quellart (Name = Schlüssel ihrer Werte in der App-Instanz), <see cref="UebersetztAttribute{TUebersetzung}"/>
/// nennt die Übersetzung der Akteur-Seite, die aus ihrer Meldung die Nachricht macht.
/// <code>
/// public sealed record Kameraeingang([Uebersetzt&lt;DateiGefunden_LiesBild_1&gt;] Dateiwaechter Kamera) : IApp&lt;Linienrechner&gt;;   // nur Quellen
/// public sealed record ArbeitsplatzApp : IApp&lt;Arbeitsplatz&gt;;                                                              // alles über die App
/// </code>
/// Ihr Vertrag: was die Akteure des Clients hineingeben, ohne die Nachrichten ihrer Quellen, und was sie hören. Eine Nachricht mit Quelle steht nicht im
/// Vertrag der App (je Nachricht entweder die App oder Quellen). Wo sie läuft, sagt die Umgebung.
/// </summary>
/// <typeparam name="TClient">Der Client, den die App umsetzt.</typeparam>
public interface IApp<TClient> where TClient : IClient { }

/// <summary>
/// Am Parameter einer App (eine Quelle, <see cref="IApp{TClient}"/>): die Übersetzung der Akteur-Seite (<c>ITranslate&lt;TMeldung, TZiel&gt;</c>), die aus der
/// Meldung der Quellart die Nachricht macht — ihr Ziel ist ein Command, den ein Akteur des Clients darf. Mehrere sind eine Gabel.
/// </summary>
/// <typeparam name="TUebersetzung">Die Übersetzung (Klasse <c>{Meldung}_{Ziel}_{n}</c> in <c>&lt;Wurzel&gt;.Akteure/Uebersetzungen.cs</c>).</typeparam>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class UebersetztAttribute<TUebersetzung> : Attribute { }

/// <summary>
/// Diese Assembly IST die App <typeparamref name="TApp"/> (<c>[assembly: IstApp&lt;ArbeitsplatzApp&gt;]</c>, docs/konzept-akteure.md §4): der
/// Client-Generator nimmt Handshake-Name, Hash und die gehörten Typen aus dem Vertrag der App (die Akteure ihres Clients ohne die Nachrichten ihrer
/// Quellen), leitet nur deren Commands weiter und stellt nur deren Queries. Der Analyzer hält den Code dagegen (CQRS093–097).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class IstAppAttribute<TApp> : Attribute { }

/// <summary>Am Client (<see cref="IClient"/>): er spricht für den Akteur <typeparamref name="TAkteur"/>. Befugt ist der Akteur, nicht die Software.</summary>
public interface IVerkoerpert<TAkteur> where TAkteur : IAkteur { }

/// <summary>
/// Am Akteur (<see cref="IAkteur"/>): zu ihm kommt <typeparamref name="T"/> — ein Fakt: ein Event oder ein Hinweis, nie ein Command (CQRS058). Ein
/// Client, der ihn verkörpert, reagiert darauf mit Commands über sein <c>IDarf</c> oder nimmt es zur Kenntnis (docs/konzept-akteure.md §5); gepusht
/// wird es verlierbar, verlässlich liest er über „lies das Log ab Position“ (genau einmal mit dem Stand seiner Reaktionen).
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
