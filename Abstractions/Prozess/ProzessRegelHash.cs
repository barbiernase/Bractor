using System;
using System.Security.Cryptography;
using System.Text;

namespace Abstractions;

/// <summary>
/// Deterministischer STRUKTUR-Hash eines <see cref="ProzessRegeln"/>-Satzes (P5b, Marking-Cursor-Invalidierung,
/// docs/prozess-marking-cursor-konzept.md §2/§5). Analog zur <see cref="Snapshot{TState}.SchemaVersion"/> der
/// Aggregat-Snapshots: ändert sich die Struktur der Regeln (Auslöser, Bedingungen, produzierte Commands,
/// Count-Join-Typ), passt der Hash nicht mehr → ein zuvor gecachtes <see cref="ProzessMarking"/> ist ungültig
/// und wird verworfen (Voll-Fold ab 0). Die Wahrheit bleibt der Log (Invariante 1); der Hash schützt nur davor,
/// ein Marking gegen einen geänderten Regelsatz weiterzufalten.
///
/// Rein aus der Typ-STRUKTUR gebildet (Typ-Namen, Reihenfolge, Count-Join-Marker) — KEINE Laufzeit-Reflection
/// über Werte, kein Dispatch (Invariante 4 unberührt): der Hash ist ein reiner Cache-Schlüssel, keine
/// Routing-Entscheidung. Stabil über Prozess-Neustarts und Knoten (nur <see cref="Type.FullName"/> + Aufbau).
/// </summary>
public static class ProzessRegelHash
{
    /// <summary>Bildet den Struktur-Hash: Auslöser-Typ + je Regel ihre Bedingungs-/Command-/Sammel-Struktur.</summary>
    public static string Berechne(ProzessRegeln regeln)
    {
        if (regeln is null) throw new ArgumentNullException(nameof(regeln));

        var sb = new StringBuilder();
        sb.Append("auslöser=").Append(regeln.AuslöserTyp.FullName).Append('\n');
        for (int i = 0; i < regeln.Regeln.Count; i++)
        {
            var r = regeln.Regeln[i];
            sb.Append('r').Append(i).Append(":bed=");
            foreach (var t in r.Bedingung) sb.Append(t.FullName).Append(',');
            sb.Append(";cmd=");
            foreach (var t in r.ProduziertCommands) sb.Append(t.FullName).Append(',');
            sb.Append(";sammel=").Append(r.Sammel?.Typ.FullName ?? "-");
            sb.Append(";rückgängig=").Append(r.RückgängigDurch is null ? "0" : "1");
            sb.Append('\n');
        }

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()), hash);
        return Convert.ToHexString(hash);
    }
}
