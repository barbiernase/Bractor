using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abstractions;

/// <summary>
/// Die Regeln eines CLIENT-VERTRAGS (<see cref="IClientVertrag"/>, <c>docs/konzept-akteure.md</c> §4) an EINER Stelle: der Handshake-Name, die
/// kanonische Form, aus der der Vertrags-Hash entsteht, und wie ein <c>IDarf</c> der verkörperten Akteure über die Leitung geht. Server-Generator (<c>AkteurRechteGenerator</c>,
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
    /// Kanonische Textform eines Client-Vertrags: <c>Client|A:…|T:&lt;Kanon der getragenen Funktion&gt;|…|S:…|F:…|H:…</c> — <c>A</c> = die
    /// verkörperten Akteure, getragene Funktionen nach Namen sortiert, <c>S</c> = sendet, <c>F</c> = fragt, <c>H</c> = hört (jeweils sortierte
    /// einfache Namen). Trägt, sendet und fragt sind die <c>IDarf</c> der Akteure (<see cref="Art"/>): bekommt ein Akteur eine Befugnis dazu,
    /// ändert sich der Hash jedes Clients, der ihn verkörpert.
    /// </summary>
    public static string ClientKanon(string client, IEnumerable<string> akteure, IEnumerable<KeyValuePair<string, IEnumerable<Eingang>>> traegt,
        IEnumerable<string> sendet, IEnumerable<string> fragt, IEnumerable<string> hoert)
    {
        string Liste(IEnumerable<string> x) => string.Join(",", x.Distinct().OrderBy(n => n, System.StringComparer.Ordinal));
        var sb = new StringBuilder(client).Append("|A:").Append(Liste(akteure));
        foreach (var t in traegt.OrderBy(t => t.Key, System.StringComparer.Ordinal))
            sb.Append("|T:").Append(Kanon(t.Key, t.Value));
        sb.Append("|S:").Append(Liste(sendet)).Append("|F:").Append(Liste(fragt)).Append("|H:").Append(Liste(hoert));
        return sb.ToString();
    }

    /// <summary>Wie ein Client ein <c>IDarf&lt;T&gt;</c> seiner verkörperten Akteure über die Leitung gibt.</summary>
    public enum Befugnisart
    {
        /// <summary>Kein Weg über die Leitung (CQRS058 meldet so ein <c>IDarf</c>).</summary>
        Keine,
        /// <summary>Von sich aus hinein: ein Command, eine Quell-Nachricht oder ein Hinweis.</summary>
        Sendet,
        /// <summary>Eine Query.</summary>
        Fragt,
        /// <summary>Eine Funktion als Schnittstelle: der Client führt ihren Code aus.</summary>
        Traegt,
    }

    /// <summary>Die Marker (volle Namen), an denen <see cref="Art"/> die Befugnisart abliest.</summary>
    public const string IFunktion = "Abstractions.IFunktion", IQuery = "Abstractions.IQuery", ICommand = "Abstractions.ICommand",
        IQuellNachricht = "Abstractions.IQuellNachricht", ITransientEvent = "Abstractions.ITransientEvent";

    /// <summary>
    /// Die Befugnisart eines <c>IDarf&lt;T&gt;</c> — EINE Regel für Server-Generator, Client-Generator, Analyzer, Python-Prepass und Extractor:
    /// die Art steht am Marker von <c>T</c>, kein zweites Wort am Client-Vertrag. <paramref name="hat"/> sagt, ob <c>T</c> den Marker (voller Name)
    /// trägt; <paramref name="schnittstelle"/>, ob <c>T</c> ein Interface ist.
    /// </summary>
    public static Befugnisart Art(System.Func<string, bool> hat, bool schnittstelle) =>
        schnittstelle ? (hat(IFunktion) ? Befugnisart.Traegt : Befugnisart.Keine)
        : hat(IQuery) ? Befugnisart.Fragt
        : hat(ICommand) || hat(IQuellNachricht) || hat(ITransientEvent) ? Befugnisart.Sendet
        : Befugnisart.Keine;

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
