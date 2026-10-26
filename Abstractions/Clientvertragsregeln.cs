using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abstractions;

/// <summary>
/// Die Regeln eines CLIENT-VERTRAGS (<see cref="IClientVertrag"/>, <c>docs/konzept-akteure.md</c> §4) an EINER Stelle: der Handshake-Name, die
/// kanonische Form, aus der der Vertrags-Hash entsteht, und welche Akteure ein Client verkörpert. Server-Generator (<c>AkteurRechteGenerator</c>,
/// per Link eingebunden, netstandard2.0), Client-Generator, Analyzer, Extractor und der Python-Prepass (<c>Cqrs.Codegen</c>) lesen sie von hier —
/// so ergibt derselbe Vertrag überall denselben Hash, und ein Client, der gegen einen älteren Stand gebaut wurde, fällt am Handshake auf.
/// <para>In den Generatoren (netstandard2.0, per Link) ist die Klasse <c>internal</c> — sonst sähe ein Projekt, das Abstractions UND
/// einen Generator referenziert (Prüfstand), sie doppelt.</para>
/// </summary>
#if NETSTANDARD2_0
internal
#else
public
#endif
static class Clientvertragsregeln
{
    /// <summary>
    /// Der Handshake-Name eines Client-Vertrags: der Interface-Name ohne das führende <c>I</c> (<c>IMlWorker</c> → <c>MlWorker</c>) — so heißen
    /// auch die Python-Basis (<c>MlWorkerBasis</c>) und der Schlüssel der generierten Tabelle. Akteur- und Client-Namen dürfen sich nicht
    /// überschneiden (CQRS059).
    /// </summary>
    public static string ClientName(string interfaceName) =>
        interfaceName.Length > 1 && interfaceName[0] == 'I' && char.IsUpper(interfaceName[1]) ? interfaceName.Substring(1) : interfaceName;

    /// <summary>
    /// Kanonische Textform einer getragenen Funktion: <c>Funktion:Eingang&gt;AusgabeA,AusgabeB[*];…</c> — Eingänge und Ausgaben nach einfachem
    /// Namen sortiert (die Namen sind auch die Proto-Namen), <c>*</c> = Strom. Ändert sich eine <c>Auf</c>-Signatur, ändert sich der Hash jedes
    /// Clients, der die Funktion trägt — seine Python-Basis ist dann veraltet.
    /// </summary>
    public static string Kanon(string funktion, IEnumerable<Eingang> eingaenge)
    {
        var sb = new StringBuilder(funktion).Append(':');
        var erste = true;
        foreach (var r in eingaenge.OrderBy(x => x.Typ, System.StringComparer.Ordinal))
        {
            if (!erste) sb.Append(';');
            erste = false;
            sb.Append(r.Typ).Append('>')
              .Append(string.Join(",", r.Ausgaenge.Distinct().OrderBy(x => x, System.StringComparer.Ordinal)));
            if (r.Strom) sb.Append('*');
        }
        return sb.ToString();
    }

    /// <summary>Vertrags-Hash: die ersten 16 Hex-Zeichen von SHA-256 über die kanonische Form.</summary>
    public static string Hash(string kanon)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var b = sha.ComputeHash(Encoding.UTF8.GetBytes(kanon));
            var sb = new StringBuilder();
            for (var i = 0; i < 8; i++) sb.Append(b[i].ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Kanonische Textform eines Client-Vertrags: <c>Client|T:&lt;Kanon der getragenen Funktion&gt;|…|S:…|F:…|H:…</c> — getragene Funktionen nach
    /// Namen sortiert, <c>S</c> = sendet, <c>F</c> = fragt, <c>H</c> = hört (jeweils sortierte einfache Namen).
    /// </summary>
    public static string ClientKanon(string client, IEnumerable<KeyValuePair<string, IEnumerable<Eingang>>> traegt,
        IEnumerable<string> sendet, IEnumerable<string> fragt, IEnumerable<string> hoert)
    {
        string Liste(IEnumerable<string> x) => string.Join(",", x.Distinct().OrderBy(n => n, System.StringComparer.Ordinal));
        var sb = new StringBuilder(client);
        foreach (var t in traegt.OrderBy(t => t.Key, System.StringComparer.Ordinal))
            sb.Append("|T:").Append(Kanon(t.Key, t.Value));
        sb.Append("|S:").Append(Liste(sendet)).Append("|F:").Append(Liste(fragt)).Append("|H:").Append(Liste(hoert));
        return sb.ToString();
    }

    /// <summary>
    /// Welche Akteure ein Client VERKÖRPERT (docs/konzept-akteure.md §4.2) — EINE Regel für Server-Generator, Python-Prepass, Wissensgraph und
    /// Editor: je getragener Funktion, gesendetem und gefragtem Typ sein einziger <c>IDarf</c>-Halter (Pflicht); darf ein Typ mehrere, kommt nur
    /// dann jemand hinzu, wenn noch keiner seiner Halter verkörpert ist (dann alle — der Vertrag sagt nicht, welcher). So wird der Arbeitsplatz
    /// über <c>HoleDatensatzSamples</c> nicht auch das TrainingsSystem. Mehrdeutige Typen in fester Reihenfolge (sortierte Halter-Liste), damit
    /// überall dasselbe herauskommt. Was ein Client nur hört, verkörpert keinen Akteur.
    /// </summary>
    public static List<string> Verkoerpert(IEnumerable<IEnumerable<string>> halterJeTyp)
    {
        var verk = new SortedSet<string>(System.StringComparer.Ordinal);
        var listen = halterJeTyp.Select(h => h.Distinct().OrderBy(x => x, System.StringComparer.Ordinal).ToList()).Where(h => h.Count > 0).ToList();
        foreach (var h in listen.Where(h => h.Count == 1)) verk.Add(h[0]);
        foreach (var h in listen.Where(h => h.Count > 1).OrderBy(h => string.Join(",", h), System.StringComparer.Ordinal))
            if (!h.Any(verk.Contains)) verk.UnionWith(h);
        return verk.ToList();
    }

    /// <summary>Ein Eingang einer getragenen Funktion (ein <c>Auf</c>): sein Typ (einfacher Name), die erlaubten Ausgaben, Strom ja/nein.</summary>
    public sealed class Eingang
    {
        public Eingang(string typ, IReadOnlyList<string> ausgaenge, bool strom)
        {
            Typ = typ;
            Ausgaenge = ausgaenge;
            Strom = strom;
        }

        public string Typ { get; }
        public IReadOnlyList<string> Ausgaenge { get; }
        public bool Strom { get; }
    }
}
