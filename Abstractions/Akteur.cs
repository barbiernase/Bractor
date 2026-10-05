namespace Abstractions;

/// <summary>
/// Ein AKTEUR — ein Domänen-Experte, von dem alles kommt, was hineingeht (<c>docs/konzept-akteure.md</c> §8). Seine Art
/// steht in der Basisliste (<see cref="IMensch"/>, <see cref="IMaschine"/>, <see cref="IKi"/>), was er selbst hineingeben
/// darf als <see cref="IDarf{T}"/> — Code-Fakten, die Generator, Extractor und Editor aus der Signatur lesen:
/// <code>public sealed record Inspekteur : IMensch, IDarf&lt;LabelBildPaar&gt;, IDarf&lt;GetImagePair&gt;;</code>
/// Ein Command darf bei mehreren Akteuren stehen. Was eine Pipeline, ein Prozess oder eine Frist daraus erzeugt, trägt
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
/// Ein DIENST, mit dem <typeparamref name="TAkteur"/> drinnen entscheidet — der Dienst ist kein Akteur, er gehört einem:
/// <code>public interface IGutachten : IAkteurDienst&lt;Gutachter&gt; { … }</code>
/// Ein Pipeline-Handle, der ihn als Parameter nimmt, entscheidet im Auftrag von <typeparamref name="TAkteur"/> (der
/// einzige Akteur-Wechsel mitten in einer Kette); seine Ausgaben muss <typeparamref name="TAkteur"/> dürfen (CQRS060).
/// Damit steht fest, wer den Dienst benutzt: der Akteur — und genau die Handles mit diesem Parameter.
/// </summary>
public interface IAkteurDienst<TAkteur> where TAkteur : IAkteur { }

/// <summary>
/// Der Akteur darf <typeparamref name="T"/> in das System hineingeben — EIN Wort für alles, was hineingeht:
/// Command (auslösen), Query (fragen bzw. als Zuständiger beantworten), Trigger (starten bzw. verarbeiten), Transient-Event.
/// Andere <typeparamref name="T"/> meldet der Analyzer CQRS058.
/// </summary>
public interface IDarf<T> { }

/// <summary>
/// „Im Auftrag von": der Akteur, für den gerade entschieden wird — gesetzt vom GENERIERTEN Pipeline-Dispatch um einen
/// Handle, der einen Akteur-Dienst als Parameter nimmt (<c>Handle(evt, ctx, IGutachten g)</c> → „Gutachter“), gelesen vom
/// Emit-Primitiv (stempelt ihn als <c>UserId</c>). Fluss-lokal (<see cref="System.Threading.AsyncLocal{T}"/>): er reist
/// durch die awaits des Handle-Aufrufs und endet mit ihm. Der Fachcode sieht ihn nie.
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
