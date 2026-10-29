namespace Abstractions;

/// <summary>
/// Die SPUR eines Log-Eintrags (docs/04 §4.9): vier Header, vom Framework gesetzt und unsichtbar für den Fachcode
/// (Inv. 5) — <see cref="UrsacheHeader"/> (der Log-Eintrag, der ihn ausgelöst hat: <c>Stream:Version</c>), <see cref="WirkerHeader"/> (der
/// Baustein, der geschrieben hat: <see cref="WirkerId"/> = Karte im Editor), <see cref="UeberHeader"/> (wer den Command bzw. Auftrag
/// abgeschickt hat, den der Wirker verarbeitet: Fluss-Knoten, Regel, Reaktion, Akteur — der Weg über die Karten dazwischen) und
/// <see cref="StandHeader"/> (Hash des Builds).
/// <para>Ursache und Wirker reisen wie der Akteur (<see cref="ImAuftrag"/>) fluss-lokal (<see cref="System.Threading.AsyncLocal{T}"/>): wer
/// schreibt oder einen Command/Auftrag abschickt, setzt sie um seinen Schritt (<see cref="Von"/>); der Event-Store stempelt sie beim Append,
/// der Emit legt sie in den Command-Envelope, und das Aggregat setzt sie aus dem Envelope wieder — so ist die Ursache-Kette auch über
/// Commands und Funktionen hinweg lückenlos. <c>CausationId</c> bleibt unberührt (sie trägt den Prozess-Fold).</para>
/// </summary>
public static class Spur
{
    public const string UrsacheHeader = "ursache";
    public const string WirkerHeader = "wirker";
    public const string StandHeader = "stand";
    public const string UeberHeader = "ueber";

    /// <summary>Die Ursache als Text: <c>&lt;StreamId&gt;:&lt;Version&gt;</c>.</summary>
    public static string Ursache(Guid stream, long version) => stream.ToString("D") + ":" + version;

    /// <summary>Zerlegt eine Ursache (<c>Stream:Version</c>); false, wenn keine.</summary>
    public static bool ZerlegeUrsache(string? ursache, out Guid stream, out long version)
    {
        stream = Guid.Empty;
        version = 0;
        if (string.IsNullOrEmpty(ursache)) return false;
        var i = ursache!.LastIndexOf(':');
        return i > 0 && Guid.TryParse(ursache.Substring(0, i), out stream) && long.TryParse(ursache.Substring(i + 1), out version);
    }

    /// <summary>
    /// Der Stand des laufenden Builds — vom Host beim Laden gesetzt (generiert aus der MSBuild-Property <c>CqrsStand</c>, die die Werkbank
    /// beim Bauen übergibt; dieselbe Hash-Funktion wie ihr Kopfzeilen-Stand). Null: ohne Stand gebaut, kein Header.
    /// </summary>
    public static string? Stand { get; set; }

    private static readonly System.Threading.AsyncLocal<Kopf?> _kopf = new();

    /// <summary>Die Spur des laufenden Schritts (Ursache, Wirker) — null außerhalb eines Schritts.</summary>
    public static Kopf? Aktuell => _kopf.Value;

    /// <summary>
    /// Setzt Ursache, Wirker und Absender bis zum Dispose (stellt die vorherigen wieder her). Ein null-Wert erbt den äußeren — der Absender
    /// gehört zum Wirker: ein neuer Wirker ohne Absender hat keinen (sonst trüge er den Absender eines fremden Schritts).
    /// </summary>
    public static IDisposable Von(string? ursache, string? wirker, string? ueber = null)
    {
        var vorher = _kopf.Value;
        _kopf.Value = new Kopf(ursache ?? vorher?.Ursache, wirker ?? vorher?.Wirker, ueber ?? (wirker == null ? vorher?.Ueber : null));
        return new Zurueck(vorher);
    }

    /// <summary>Ursache, Wirker und Absender (<see cref="UeberHeader"/>) eines Schritts.</summary>
    public sealed record Kopf(string? Ursache, string? Wirker, string? Ueber = null);

    private sealed class Zurueck(Kopf? vorher) : IDisposable
    {
        public void Dispose() => _kopf.Value = vorher;
    }
}
