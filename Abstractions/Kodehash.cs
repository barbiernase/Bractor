using System.Text;

namespace Abstractions;

/// <summary>
/// Der Code-Hash (docs/plan-werkbank-umsetzung.md E9): die Generatoren hashen die normalisierte Syntax eines Bausteins — die
/// Token-Folge ohne Leerraum und Kommentare —, damit „passen die Daten noch zum Code?" ohne Laufzeit-Reflection beantwortbar ist
/// (Aggregat = State + Applier → Snapshot verwerfen; Projektion = Klasse + Store → neu aufbauen). Eine Stelle für Generatoren
/// (per Link, netstandard2.0, dort <c>internal</c>) und Laufzeit (Zusammensetzen mehrerer Teil-Hashes).
/// </summary>
#if NETSTANDARD2_0
internal
#else
public
#endif
static class Kodehash
{
    /// <summary>Die ersten 16 Hex-Zeichen von SHA-256 über <paramref name="kanon"/>.</summary>
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

    /// <summary>Ein Hash über mehrere Teil-Hashes (Reihenfolge zählt).</summary>
    public static string Kombiniere(params string[] teile) => Hash(string.Join("|", teile));
}
