using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abstractions;

/// <summary>
/// Die Regeln eines CLIENTS (<see cref="IClient"/>, <c>docs/konzept-akteure.md</c> §4) an EINER Stelle: der Handshake-Name, die kanonische Form, aus
/// der der Hash entsteht, und wie ein <c>IDarf</c> der verkörperten Akteure über die Leitung geht. Server-Generator (<c>AkteurRechteGenerator</c>, per
/// Link eingebunden, netstandard2.0), Client-Generator, Analyzer, Extractor und der Python-Prepass (<c>Cqrs.Codegen</c>) lesen sie von hier — so
/// ergibt derselbe Client überall denselben Hash, und ein Client, der gegen einen älteren Stand gebaut wurde, fällt am Handshake auf.
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
    /// <summary>Der Handshake-Name eines Clients: der Name seines Records — so heißen auch die Python-Basis (<c>KlassifikationsWorkerBasis</c>) und
    /// der Schlüssel der generierten Tabelle. Akteur- und Client-Namen dürfen sich nicht überschneiden (CQRS059).</summary>
    public static string ClientName(string recordName) => recordName;

    /// <summary>Hash: die ersten 16 Hex-Zeichen von SHA-256 über die kanonische Form.</summary>
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
    /// Kanonische Textform eines Clients: <c>Client|A:…|S:…|F:…|H:…</c> — <c>A</c> = die verkörperten Akteure, <c>S</c> = sendet, <c>F</c> = fragt,
    /// <c>H</c> = hört (jeweils sortierte einfache Namen). Alles folgt aus den Akteuren: bekommt einer eine Befugnis dazu, ändert sich der Hash jedes
    /// Clients, der ihn verkörpert.
    /// </summary>
    public static string ClientKanon(string client, IEnumerable<string> akteure, IEnumerable<string> sendet, IEnumerable<string> fragt,
        IEnumerable<string> hoert)
    {
        string Liste(IEnumerable<string> x) => string.Join(",", x.Distinct().OrderBy(n => n, System.StringComparer.Ordinal));
        var sb = new StringBuilder(client).Append("|A:").Append(Liste(akteure));
        sb.Append("|S:").Append(Liste(sendet)).Append("|F:").Append(Liste(fragt)).Append("|H:").Append(Liste(hoert));
        return sb.ToString();
    }

    /// <summary>Wie ein Client ein <c>IDarf&lt;T&gt;</c> seiner verkörperten Akteure über die Leitung gibt.</summary>
    public enum Befugnisart
    {
        /// <summary>Kein Weg über die Leitung (CQRS058 meldet so ein <c>IDarf</c>).</summary>
        Keine,
        /// <summary>Hinein: ein Command oder ein Hinweis.</summary>
        Sendet,
        /// <summary>Eine Query.</summary>
        Fragt,
    }

    /// <summary>Die Marker (volle Namen), an denen <see cref="Art"/> die Befugnisart abliest.</summary>
    public const string IQuery = "Abstractions.IQuery", ICommand = "Abstractions.ICommand", ITransientEvent = "Abstractions.ITransientEvent",
        IEvent = "Abstractions.IEvent";

    /// <summary>
    /// Die Befugnisart eines <c>IDarf&lt;T&gt;</c> — EINE Regel für Server-Generator, Client-Generator, Analyzer, Python-Prepass und Extractor:
    /// die Art steht am Marker von <c>T</c>, kein zweites Wort am Client. <paramref name="hat"/> sagt, ob <c>T</c> den Marker (voller Name)
    /// trägt; <paramref name="schnittstelle"/>, ob <c>T</c> ein Interface ist.
    /// </summary>
    public static Befugnisart Art(System.Func<string, bool> hat, bool schnittstelle) =>
        schnittstelle ? Befugnisart.Keine
        : hat(IQuery) ? Befugnisart.Fragt
        : hat(ICommand) || hat(ITransientEvent) ? Befugnisart.Sendet
        : Befugnisart.Keine;

    /// <summary>
    /// Kann ein Akteur <c>T</c> hören (<c>IHoert&lt;T&gt;</c>)? Nur ein Event oder einen Hinweis — was zu ihm kommt, ist ein Fakt (er reagiert darauf
    /// oder nimmt es zur Kenntnis); einen Command hört kein Akteur (CQRS058, Schritt 6: es gibt keine Arbeit aus einem Decide). <paramref name="hat"/>
    /// wie bei <see cref="Art"/>.
    /// </summary>
    public static bool Hoerbar(System.Func<string, bool> hat) => hat(IEvent) || hat(ITransientEvent);
}
