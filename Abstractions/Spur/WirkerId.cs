using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Das Format der WIRKER-Id (<c>docs/konzept-werkbank.md</c> §7.1) an EINER Stelle: der Baustein, der einen Log-Eintrag geschrieben hat —
/// dieselbe Id wie seine Karte im Editor. Generatoren (netstandard2.0, per Link), Extractor und Laufzeit lesen sie von hier.
/// <list type="bullet">
/// <item><c>decide:&lt;Aggregat&gt;/&lt;Command&gt;</c></item>
/// <item><c>funktion:&lt;IFunktion&gt;</c>, <c>quelle:&lt;Typ&gt;</c>, <c>projektion:&lt;Typ&gt;</c>, <c>akteur:&lt;Akteur&gt;</c></item>
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
    public const string Funktion = "funktion";
    public const string Quelle = "quelle";
    public const string Projektion = "projektion";
    public const string Akteur = "akteur";

    /// <summary>Der Wirker der Uhr des Frameworks (sie sendet fällige Fristen; eine Karte hat sie nicht, Plan Pub/Sub-Neustrukturierung §5).</summary>
    public const string Uhr = "uhr";

    /// <summary>Die Arten in fester Reihenfolge.</summary>
    public static readonly string[] Arten = { Decide, Funktion, Quelle, Projektion, Akteur };

    public static string FuerDecide(string aggregat, string command) => Decide + ":" + aggregat + "/" + command;
    public static string FuerFunktion(string funktion) => Funktion + ":" + funktion;
    public static string FuerQuelle(string typ) => Quelle + ":" + typ;
    public static string FuerProjektion(string typ) => Projektion + ":" + typ;
    public static string FuerAkteur(string akteur) => Akteur + ":" + akteur;

    /// <summary>Art und Rest einer Wirker-Id (<c>funktion:Bildeingang</c> → <c>(funktion, Bildeingang)</c>); <c>null</c>, wenn sie keine ist.</summary>
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
}
