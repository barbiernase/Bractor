using System;
using System.Linq;
using System.Text;

namespace Abstractions;

/// <summary>
/// Deterministischer STRUKTUR-Hash eines Prozess-Regelsatzes (P5(b), Konzept §2/§5) — die Cache-
/// Invalidierung des <see cref="ProzessMarking"/>. Ändern sich die Regeln (Bedingungen, produzierte
/// Commands, Count-Join, Kompensation, Reihenfolge, Auslöser), ändert sich der Hash → ein alter
/// Marking-Cache passt nicht mehr und der <c>ProzessManager</c> faltet sauber ab 0 (zero-touch,
/// analog zum State-Struktur-Hash der Aggregat-Snapshots, <c>GeneratedSnapshotSchema</c>).
///
/// Rein strukturell aus den bereits gebauten <see cref="ProzessRegeln"/> abgeleitet (Typ-Namen der
/// Bedingungen/Commands, Vorhandensein von Sammel/Gegenzug) — KEIN Laufzeit-Dispatch, kein Invoke der
/// Regel-Lambdas; einmal am Boot je Prozess berechnet. FNV-1a wie beim Snapshot-Struktur-Hash.
/// </summary>
public static class ProzessRegelHash
{
    public static string Berechne(ProzessRegeln regeln)
    {
        var sb = new StringBuilder();
        sb.Append("auslöser=").Append(regeln.AuslöserTyp.FullName).Append('|');
        for (int i = 0; i < regeln.Regeln.Count; i++)
        {
            var r = regeln.Regeln[i];
            sb.Append(i).Append(':');
            sb.Append("bed=").Append(string.Join(",", r.Bedingung.Select(t => t.FullName)));
            sb.Append(";cmd=").Append(string.Join(",", r.ProduziertCommands.Select(t => t.FullName)));
            sb.Append(";sammel=").Append(r.Sammel is null ? "-" : r.Sammel.Typ.FullName);
            sb.Append(";gegen=").Append(r.RückgängigDurch is null ? "0" : "1");
            sb.Append('|');
        }
        return FnvHex(sb.ToString());
    }

    private static string FnvHex(string s)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (var b in Encoding.UTF8.GetBytes(s))
            {
                hash ^= b;
                hash *= prime;
            }
            return hash.ToString("x8");
        }
    }
}
