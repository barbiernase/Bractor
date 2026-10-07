using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Abstractions;

/// <summary>
/// Deterministischer STRUKTUR-Hash eines <see cref="ProzessRegeln"/>-Satzes (P5b, Marking-Cursor-Invalidierung,
/// docs/04-konsum-und-prozess-maschine.md §4.5). Analog zur <see cref="Snapshot{TState}.SchemaVersion"/> der
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
            // Funktions-Aufruf und Zeitlimit nur, wenn gesetzt — so bleibt der Hash bestehender Regelsätze unverändert
            // (kein unnötiger Voll-Fold nach dem Update), und jede neue Struktur bekommt trotzdem einen eigenen Schlüssel.
            if (r.GerufeneFunktionen.Count > 0)
            {
                sb.Append(";ruft=");
                foreach (var t in r.GerufeneFunktionen) sb.Append(t.FullName).Append(',');
            }
            if (r.Zeitlimit is { } z) sb.Append(";zeitlimit=").Append(z.Ticks);
            // Pipeline-Fluss (§14): Knoten, Herkunft je Bedingung, Je-Auffächern und Sammel-Drähte — nur wenn gesetzt.
            if (r.Knoten is int k)
            {
                sb.Append(";knoten=").Append(k).Append(";von=");
                foreach (var v in r.VonKnoten) sb.Append(v?.ToString() ?? "*").Append(',');
                if (r.JeKnoten is int je) sb.Append(";je=").Append(je);
                if (r.Sammel is { } sm)
                {
                    sb.Append(";sammelJe=").Append(sm.JeKnoten?.ToString() ?? "-").Append(";drähte=");
                    foreach (var (t, v) in sm.Drähte) sb.Append(t.FullName).Append('@').Append(v?.ToString() ?? "*").Append(',');
                }
            }
            sb.Append('\n');
        }
        if (regeln.IstFluss)
        {
            sb.Append("quelle=").Append(regeln.QuellKnoten).Append(";⏳=");
            foreach (var z in regeln.UmleitenZeitlimit.OrderBy(x => x)) sb.Append(z).Append(',');
            sb.Append(";✕=");
            foreach (var a in regeln.UmleitenAbgelehnt.OrderBy(x => x)) sb.Append(a).Append(',');
        }

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()), hash);
        return Convert.ToHexString(hash);
    }
}
