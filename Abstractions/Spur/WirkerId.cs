using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Das Format der WIRKER-Id (docs/04 §4.9) an EINER Stelle: der Baustein, der einen Log-Eintrag geschrieben hat —
/// dieselbe Id wie seine Karte im Editor. Generatoren (netstandard2.0, per Link), Extractor und Laufzeit lesen sie von hier.
/// <list type="bullet">
/// <item><c>decide:&lt;Aggregat&gt;/&lt;Command&gt;</c></item>
/// <item><c>funktion:&lt;Funktion&gt;</c> (die Rechnung — so leiten <c>Herkunft</c> und Ids ab), <c>knoten:&lt;Instanz&gt;/&lt;Knoten&gt;</c> (der Schritt eines
/// Laufs in der Spur: Instanz eines Graphen und Pfad des Knotens, <c>linie1/Aufbereitung.Vorschau</c>), <c>projektion:&lt;Typ&gt;</c>,
/// <c>akteur:&lt;Akteur&gt;</c>, <c>uebersetzung:&lt;Übersetzung&gt;</c> (eine Übersetzung der Domäne, docs/04 §4.4)</item>
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
    public const string Knoten = "knoten";
    public const string Projektion = "projektion";
    public const string Akteur = "akteur";
    public const string Uebersetzung = "uebersetzung";
    /// <summary>Die Quelle einer App-Instanz (<c>eingang:Instanz/Quelle</c>): sie gibt im Namen eines Akteurs ihres Clients hinein, was sie meldet.</summary>
    public const string Eingang = "eingang";

    /// <summary>Der Wirker der Uhr des Frameworks (sie sendet fällige Fristen; eine Karte hat sie nicht).</summary>
    public const string Uhr = "uhr";

    /// <summary>Die Arten in fester Reihenfolge.</summary>
    public static readonly string[] Arten = { Decide, Funktion, Knoten, Projektion, Akteur, Uebersetzung, Eingang };

    public static string FuerDecide(string aggregat, string command) => Decide + ":" + aggregat + "/" + command;
    public static string FuerFunktion(string funktion) => Funktion + ":" + funktion;
    /// <summary>Der Schritt eines Laufs: der Knoten <paramref name="knoten"/> (Pfad im Graphen) der Instanz <paramref name="instanz"/>.</summary>
    public static string FuerKnoten(string instanz, string knoten) => Knoten + ":" + instanz + "/" + knoten;
    public static string FuerProjektion(string typ) => Projektion + ":" + typ;
    public static string FuerAkteur(string akteur) => Akteur + ":" + akteur;
    public static string FuerUebersetzung(string uebersetzung) => Uebersetzung + ":" + uebersetzung;
    public static string FuerEingang(string instanz, string quelle) => Eingang + ":" + instanz + "/" + quelle;

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
