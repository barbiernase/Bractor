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
/// kann über mehrere Clients laufen. Der Client-Vertrag ist ein Interface, das zusammenstellt, was über SEINE Leitung geht
/// (docs/konzept-akteure.md §5):
/// <code>public interface IMlWorker : IClientVertrag, ITraegt&lt;IBildpaarBeurteilung&gt;, ISendet&lt;KlassifiziereEinzelBildDurchKi&gt;, IFragt&lt;HoleAktivesModell&gt;, IHoert&lt;ModellAktiviert&gt;;</code>
/// <list type="bullet">
/// <item><see cref="ITraegt{TFunktion}"/> → Funktionen, deren Code er ausführt (draußen gerechnet, die Garantie bleibt beim Funktions-Actor);</item>
/// <item><see cref="ISendet{T}"/> / <see cref="IFragt{T}"/> → was er von sich aus hineingibt bzw. fragt;</item>
/// <item><see cref="IHoert{T}"/> → Kenntnis (UI-Aktualisierung, verlierbar), nie eine Ausgabe.</item>
/// </list>
/// Was er trägt, sendet oder fragt, muss ein verkörperter Akteur dürfen (<c>IDarf</c>); welche Akteure er verkörpert, wird daraus abgeleitet
/// (<see cref="Clientvertragsregeln.Verkoerpert"/>). Wirksam ist am Handshake die Schnittmenge mit den Akteuren des Tokens. Die client-INTERNEN
/// Commands/Events (Intents, ClientEvents, Python-State) stehen hier nie — der Vertrag ist nur der Rand (CQRS063, CQRS064).
/// </summary>
public interface IClientVertrag { }

/// <summary>
/// Diese Client-Assembly IST der Client <typeparamref name="TVertrag"/> (<c>[assembly: IstClient&lt;IArbeitsplatz&gt;]</c>, docs/konzept-akteure.md §4):
/// der Client-Generator nimmt Handshake-Name, Vertrags-Hash und die abonnierten Events aus dem Vertrag (<see cref="IHoert{T}"/>), leitet nur die Commands seines <see cref="ISendet{T}"/> weiter und stellt nur die Queries seines <see cref="IFragt{T}"/>. Der
/// Analyzer hält den Client-Code dagegen: ein erzeugter Command/eine Query ohne Vertrag, ein Handle auf ein Server-Event ohne Kenntnis ist
/// ein Build-Fehler (CQRS093–097).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class IstClientAttribute<TVertrag> : Attribute where TVertrag : IClientVertrag { }

/// <summary>
/// Am Client-Vertrag (<see cref="IClientVertrag"/>): der Client gibt <typeparamref name="T"/> von sich aus hinein — ein Command, ein
/// eine Quell-Nachricht oder ein Hinweis. Ein verkörperter Akteur muss <c>IDarf&lt;T&gt;</c> haben (CQRS064): der Client-Vertrag schneidet aus
/// der Befugnis, er erweitert sie nie.
/// </summary>
public interface ISendet<T> { }

/// <summary>
/// Am Client-Vertrag (<see cref="IClientVertrag"/>): der Client stellt die Query <typeparamref name="T"/> (und bekommt ihre Response).
/// Ein verkörperter Akteur muss <c>IDarf&lt;T&gt;</c> haben (CQRS064).
/// </summary>
public interface IFragt<T> { }

/// <summary>
/// Am Client-Vertrag (<see cref="IClientVertrag"/>): der Client hört <typeparamref name="T"/> — Kenntnis, verlierbar (docs/konzept-akteure.md §5, docs/10 §10.5): ein Event
/// oder ein Hinweis, gepusht, ohne Garantie und ohne Ausgabe. Verlässlich liest ein Client nur über „lies das Log ab Position“.
/// </summary>
public interface IHoert<T> { }

/// <summary>
/// Am Client-Vertrag (<see cref="IClientVertrag"/>): der Client TRÄGT die Funktion <typeparamref name="TFunktion"/> — er führt ihren Code aus
/// (docs/konzept-akteure.md §5, docs/10 §10.3: eine Funktion, als Schnittstelle deklariert, läuft draußen). Er holt ihre Eingänge über die Vermittlung mit Lease, antwortet
/// mit den Ausgaben ihrer Signatur; festgehalten und gesendet wird beim Funktions-Actor, die erste Antwort gewinnt. Ein verkörperter Akteur muss
/// <c>IDarf&lt;TFunktion&gt;</c> haben; eine getragene Funktion ohne Bindung in der Umgebung läuft dort (Laufort Extern).
/// </summary>
public interface ITraegt<TFunktion> where TFunktion : IFunktion { }

/// <summary>
/// Der Akteur darf <typeparamref name="T"/> in das System hineingeben — EIN Wort für alles, was hineingeht:
/// Command (auslösen), Query (fragen), Quell-Nachricht, Hinweis, und die Ausgaben einer Funktion, deren Code er draußen rechnet (<c>IDarf&lt;IFunktion-Typ&gt;</c>).
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
