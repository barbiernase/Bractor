using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Das Format der WIRKER-Id (<c>docs/konzept-werkbank.md</c> §7.1) an EINER Stelle: der Baustein, der einen Log-Eintrag geschrieben hat —
/// dieselbe Id wie seine Karte im Editor. Generatoren (netstandard2.0, per Link), Extractor und Laufzeit lesen sie von hier; so heißt ein
/// Fluss-Knoten oder eine Prozess-Regel im Header genauso wie auf dem Board.
/// <list type="bullet">
/// <item><c>decide:&lt;Aggregat&gt;/&lt;Command&gt;</c></item>
/// <item><c>fluss:&lt;Pipeline&gt;#&lt;Knoten&gt;</c> — der Knoten-Name ist die Variable im <c>Definiere</c>-Lambda (<see cref="KnotenNamen"/>)</item>
/// <item><c>prozess:&lt;Prozess&gt;#&lt;Regel&gt;</c> — der Regel-Name folgt aus den Typ-Argumenten (<see cref="Regel"/>, E4)</item>
/// <item><c>funktion:&lt;IFunktion&gt;</c>, <c>quelle:&lt;Typ&gt;</c>, <c>projektion:&lt;Typ&gt;</c>, <c>reaktion:&lt;Typ&gt;</c>, <c>akteur:&lt;Akteur&gt;</c></item>
/// </list>
/// Alle Namen sind einfache Typ- bzw. Variablennamen (ohne Namespace).
/// <para>In den Generatoren (netstandard2.0, per Link) ist die Klasse <c>internal</c> — sonst sähe ein Projekt, das Abstractions UND einen
/// Generator referenziert (Prüfstand), sie doppelt.</para>
/// </summary>
#if NETSTANDARD2_0
internal
#else
public
#endif
static class WirkerId
{
    public const string Decide = "decide";
    public const string Fluss = "fluss";
    public const string Prozess = "prozess";
    public const string Funktion = "funktion";
    public const string Quelle = "quelle";
    public const string Projektion = "projektion";
    public const string Reaktion = "reaktion";
    public const string Akteur = "akteur";

    /// <summary>Die acht Arten in fester Reihenfolge.</summary>
    public static readonly string[] Arten = { Decide, Fluss, Prozess, Funktion, Quelle, Projektion, Reaktion, Akteur };

    public static string FuerDecide(string aggregat, string command) => Decide + ":" + aggregat + "/" + command;
    public static string FuerFluss(string pipeline, string knoten) => Fluss + ":" + pipeline + "#" + knoten;
    public static string FuerProzess(string prozess, string regel) => Prozess + ":" + prozess + "#" + regel;
    public static string FuerFunktion(string funktion) => Funktion + ":" + funktion;
    public static string FuerQuelle(string typ) => Quelle + ":" + typ;
    public static string FuerProjektion(string typ) => Projektion + ":" + typ;
    public static string FuerReaktion(string typ) => Reaktion + ":" + typ;
    public static string FuerAkteur(string akteur) => Akteur + ":" + akteur;

    /// <summary>Art und Rest einer Wirker-Id (<c>fluss:Bildeingang#datei</c> → <c>(fluss, Bildeingang#datei)</c>); <c>null</c>, wenn sie keine ist.</summary>
    public static bool Zerlege(string? id, out string art, out string rest)
    {
        art = rest = "";
        if (string.IsNullOrEmpty(id)) return false;
        var i = id!.IndexOf(':');
        if (i <= 0 || i == id.Length - 1) return false;
        art = id.Substring(0, i);
        rest = id.Substring(i + 1);
        return Arten.Contains(art);
    }

    /// <summary>
    /// Der Name einer Prozess-Regel (E4): <c>&lt;Auslöser&gt;[+&lt;Und&gt;…][+&lt;Sammel&gt;*]→&lt;Ziel&gt;</c> aus den Typ-Argumenten —
    /// <c>Auf&lt;A&gt;().Und&lt;B&gt;().Sende&lt;C&gt;</c> heißt <c>A+B→C</c>, ein Count-Join <c>UndAlle&lt;S&gt;</c> trägt ein <c>*</c>
    /// (<c>A+S*→C</c>). Ziel ist der Command (<c>Sende</c>/<c>SendeJe</c>) bzw. die Funktion (<c>Rufe</c>). Zwei gleiche Namen in einem
    /// Prozess sind ein Build-Fehler (CQRS072).
    /// </summary>
    public static string Regel(IEnumerable<string> bedingung, string? sammel, string ziel)
    {
        var teile = bedingung.ToList();
        if (!string.IsNullOrEmpty(sammel)) teile.Add(sammel + "*");
        return string.Join("+", teile) + "→" + ziel;
    }

    /// <summary>
    /// Die Knoten-Namen eines Flusses in Deklarations-Reihenfolge: die Variable (<c>var vorschau = …</c>), sonst — eine Anweisung ohne
    /// Variable — der einfache Typname klein geschrieben (<c>MeldeFertig</c> → <c>meldeFertig</c>), bei Gleichstand mit einem früheren
    /// Namen durchnummeriert ab 2.
    /// </summary>
    /// <param name="variablen">je Knoten die Variable oder <c>null</c></param>
    /// <param name="typen">je Knoten der einfache Name seines Typs (Nachricht, Funktion, Command bzw. Element)</param>
    public static IReadOnlyList<string> KnotenNamen(IReadOnlyList<string?> variablen, IReadOnlyList<string> typen)
    {
        var namen = new List<string>();
        var vergeben = new HashSet<string>(System.StringComparer.Ordinal);
        for (var i = 0; i < variablen.Count; i++)
        {
            var name = variablen[i];
            if (name == null)
            {
                var basis = Klein(typen[i]);
                name = basis;
                for (var n = 2; vergeben.Contains(name); n++) name = basis + n;
            }
            namen.Add(name);
            vergeben.Add(name);
        }
        return namen;
    }

    private static string Klein(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
}
