using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abstractions;

/// <summary>
/// Die Regeln eines AKTEUR-VERTRAGS (<see cref="IAkteurVertrag{TAkteur}"/>, <c>docs/konzept-akteure.md</c> §9) an EINER Stelle:
/// der Methodenname <see cref="Auf"/> und die kanonische Form, aus der der Vertrags-Hash entsteht. Server-Generator
/// (<c>AkteurRechteGenerator</c>, per Link eingebunden, netstandard2.0), Analyzer, Extractor und der Python-Prepass
/// (<c>Cqrs.Codegen</c>) lesen sie von hier — so ergibt derselbe Vertrag überall denselben Hash, und ein Client, der gegen
/// einen älteren Stand gebaut wurde, fällt am Handshake auf.
/// <para>In den Generatoren (netstandard2.0, per Link) ist die Klasse <c>internal</c> — sonst sähe ein Projekt, das Abstractions UND
/// einen Generator referenziert (Prüfstand), sie doppelt.</para>
/// </summary>
#if NETSTANDARD2_0
internal
#else
public
#endif
static class Akteurvertrag
{
    /// <summary>Reaktion auf ein Event: <c>Auf(TEvent e)</c>; der Rückgabetyp ist der Ausgabe-Vertrag.</summary>
    public const string Auf = "Auf";

    /// <summary>
    /// Kanonische Textform eines Vertrags: <c>Akteur:Event&gt;AusgabeA,AusgabeB[*];…</c> — Events und Ausgaben nach einfachem
    /// Namen sortiert (die Namen sind auch die Proto-Namen), <c>*</c> = Strom (mehrere Ausgaben je Reaktion).
    /// </summary>
    public static string Kanon(string akteur, IEnumerable<Reaktion> reaktionen)
    {
        var sb = new StringBuilder(akteur).Append(':');
        var erste = true;
        foreach (var r in reaktionen.OrderBy(x => x.Eingang, System.StringComparer.Ordinal))
        {
            if (!erste) sb.Append(';');
            erste = false;
            sb.Append(r.Eingang).Append('>')
              .Append(string.Join(",", r.Ausgaenge.Distinct().OrderBy(x => x, System.StringComparer.Ordinal)));
            if (r.Strom) sb.Append('*');
        }
        return sb.ToString();
    }

    /// <summary>Vertrags-Hash: die ersten 16 Hex-Zeichen von SHA-256 über <see cref="Kanon"/>.</summary>
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

    /// <summary>Eine Reaktion: Eingang (Event-Name), erlaubte Ausgaben (Command-Namen), Strom ja/nein.</summary>
    public sealed class Reaktion
    {
        public Reaktion(string eingang, IReadOnlyList<string> ausgaenge, bool strom)
        {
            Eingang = eingang;
            Ausgaenge = ausgaenge;
            Strom = strom;
        }

        public string Eingang { get; }
        public IReadOnlyList<string> Ausgaenge { get; }
        public bool Strom { get; }
    }
}
