namespace Abstractions;

/// <summary>
/// Ein AKTEUR — ein Domänen-Experte, von dem alles kommt, was hineingeht (<c>docs/konzept-akteure.md</c> §2). Seine Art
/// steht in der Basisliste (<see cref="IMensch"/>, <see cref="IMaschine"/>, <see cref="IKi"/>), was er selbst hineingeben
/// darf als <see cref="IDarf{T}"/> — Code-Fakten, die Generator, Extractor und Editor aus der Signatur lesen:
/// <code>public sealed record Inspekteur : IMensch, IDarf&lt;LabelBildPaar&gt;, IDarf&lt;GetImagePair&gt;;</code>
/// Ein Command darf bei mehreren Akteuren stehen. Was eine Funktion oder eine Frist daraus erzeugt, trägt
/// den Akteur der Kette — dafür gibt es kein weiteres Wort. Ein Akteur ist immer ein Record, nie ein Dienst.
/// </summary>
public interface IAkteur { }

/// <summary>Akteur-Art: ein Mensch in einer fachlichen Rolle (Inspekteur, KIOperator …).</summary>
public interface IMensch : IAkteur { }

/// <summary>Akteur-Art: eine Maschine bzw. ein Fremdsystem (KameraSystem, TrainingsSystem …).</summary>
public interface IMaschine : IAkteur { }

/// <summary>Akteur-Art: eine KI, die fachlich urteilt (Klassifizierer …).</summary>
public interface IKi : IAkteur { }

/// <summary>
/// Der VERTRAG eines CLIENTS — der Software, die an der Leitung hängt (Python-Worker, Blazor-Arbeitsplatz …),
/// <c>docs/konzept-akteure.md</c> §4. Ein Client ist nicht ein Akteur: er kann mehrere Akteure verkörpern, und ein Akteur
/// kann über mehrere Clients laufen. Der Client-Vertrag ist ein Interface, das nur nennt, WEN der Client verkörpert und WAS er hört
/// (docs/konzept-akteure.md §5):
/// <code>public interface IMlWorker : IClientVertrag, IVerkoerpert&lt;Klassifizierer&gt;, IHoert&lt;ModellAktiviert&gt;;</code>
/// <list type="bullet">
/// <item><see cref="IVerkoerpert{TAkteur}"/> → die Akteure, für die er spricht: was über seine Leitung hineingehen darf, sind ihre <c>IDarf</c>
/// (Commands, Quell-Nachrichten und Hinweise sendet er, Queries fragt er, Funktionen trägt er — die Art steht am Typ, nicht an einem Wort);</item>
/// <item><see cref="IHoert{T}"/> → Kenntnis (UI-Aktualisierung, verlierbar), nie eine Ausgabe.</item>
/// </list>
/// Wirksam ist am Handshake die Schnittmenge der verkörperten Akteure mit den Akteuren des Tokens. Die client-INTERNEN Commands/Events
/// (Intents, ClientEvents, Python-State) stehen hier nie — der Vertrag ist nur der Rand (CQRS063, CQRS064).
/// </summary>
public interface IClientVertrag { }

/// <summary>
/// Diese Client-Assembly IST der Client <typeparamref name="TVertrag"/> (<c>[assembly: IstClient&lt;IArbeitsplatz&gt;]</c>, docs/konzept-akteure.md §4):
/// der Client-Generator nimmt Handshake-Name, Vertrags-Hash und die abonnierten Events aus dem Vertrag (<see cref="IHoert{T}"/>), leitet nur die
/// Commands weiter und stellt nur die Queries, die ein verkörperter Akteur darf. Der Analyzer hält den Client-Code dagegen: ein erzeugter
/// Command/eine Query, die kein verkörperter Akteur darf, ein Handle auf ein Server-Event ohne Kenntnis ist ein Build-Fehler (CQRS093–097).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class IstClientAttribute<TVertrag> : Attribute where TVertrag : IClientVertrag { }

/// <summary>
/// Am Client-Vertrag (<see cref="IClientVertrag"/>): der Client spricht für den Akteur <typeparamref name="TAkteur"/>. Über seine Leitung darf
/// hinein, was der Akteur darf (<c>IDarf&lt;T&gt;</c>) — ein Command oder eine Quell-Nachricht oder ein Hinweis wird gesendet, eine Query
/// gefragt, eine Funktion getragen (ihr Code läuft dann draußen, die Garantie bleibt beim Funktions-Actor; eine Funktion ohne Bindung in der
/// Umgebung, die ein verkörperter Akteur rechnen darf, läuft dort: Laufort Extern). Befugt ist der Akteur, nicht die Software.
/// </summary>
public interface IVerkoerpert<TAkteur> where TAkteur : IAkteur { }

/// <summary>
/// Am Client-Vertrag (<see cref="IClientVertrag"/>): der Client hört <typeparamref name="T"/> — Kenntnis, verlierbar (docs/konzept-akteure.md §5, docs/10 §10.5): ein Event
/// oder ein Hinweis, gepusht, ohne Garantie und ohne Ausgabe. Verlässlich liest ein Client nur über „lies das Log ab Position“.
/// </summary>
public interface IHoert<T> { }

/// <summary>
/// Der Akteur darf <typeparamref name="T"/> in das System hineingeben — EIN Wort für alles, was hineingeht:
/// Command (auslösen), Query (fragen), Quell-Nachricht, Hinweis, und die Ausgaben einer Funktion, deren Code er draußen rechnet (<c>IDarf&lt;IFunktion-Typ&gt;</c>).
/// Ein Client, der den Akteur verkörpert (<see cref="IVerkoerpert{TAkteur}"/>), darf genau das über seine Leitung — die Art (senden, fragen,
/// tragen) folgt aus dem Marker von <typeparamref name="T"/> (<see cref="Clientvertragsregeln.Art"/>).
/// Andere <typeparamref name="T"/> meldet der Analyzer CQRS058.
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
