namespace Abstractions;

/// <summary>
/// Ein AKTEUR — wer von außen in das System hineingibt (Mensch in einer Rolle oder Fremdsystem; bewusst keine
/// Unterscheidung). Was er darf, steht in seiner Basisliste als <see cref="IDarf{T}"/> — ein Code-Fakt, den
/// Generator, Extractor und Editor aus der Signatur lesen:
/// <code>public sealed record Disponent : IAkteur, IDarf&lt;SetzeModellAktiv&gt;, IDarf&lt;HoleModelle&gt;;</code>
/// Alles Weitere (welche Events er hören darf, ob er eine externe Zuständigkeit übernehmen darf) wird aus dem
/// Graphen abgeleitet (<c>docs/konzept-akteure.md</c>).
/// <para>Auch ein DIENST kann Akteur sein (z. B. die KI): sein Vertrag trägt <c>IAkteur, IDarf&lt;…&gt;</c>, und ein
/// Pipeline-Handle, der ihn als Parameter nimmt, entscheidet in seinem Auftrag (CQRS060).</para>
/// </summary>
public interface IAkteur { }

/// <summary>
/// Der Akteur darf <typeparamref name="T"/> in das System hineingeben — EIN Wort für alles, was hineingeht:
/// Command (auslösen), Query (fragen bzw. als Zuständiger beantworten), Trigger (starten bzw. verarbeiten), Transient-Event.
/// Andere <typeparamref name="T"/> meldet der Analyzer CQRS058.
/// </summary>
public interface IDarf<T> { }

/// <summary>
/// „Im Auftrag von": der Akteur, für den gerade entschieden wird — gesetzt vom GENERIERTEN Pipeline-Dispatch um einen
/// Handle, der einen Akteur-Dienst als Parameter nimmt (<c>Handle(evt, ctx, IClassifierService ki)</c>), gelesen vom
/// Emit-Primitiv (stempelt ihn als <c>UserId</c>). Fluss-lokal (<see cref="System.Threading.AsyncLocal{T}"/>): er reist
/// durch die awaits des Handle-Aufrufs und endet mit ihm. Der Fachcode sieht ihn nie.
/// </summary>
public static class ImAuftrag
{
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
