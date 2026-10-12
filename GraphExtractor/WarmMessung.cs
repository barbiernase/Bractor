using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GraphExtractor;

/// <summary>
/// <c>--warm</c>: misst den warmen Workspace der Werkbank (P7.1, E7) und prüft, dass das warme Lesen dasselbe Board ergibt wie das kalte.
/// <list type="number">
/// <item>Kalt: ganze Lage (inkl. Laufzeit mit v1-Generatoren), Lesen.</item>
/// <item>Warm ohne Änderung: nur die Compilations unterhalb der Laufzeit, Routing aus den Decide-Signaturen — Board = kalt?</item>
/// <item>Text-Änderung in einer Domänen-Datei (Kommentar, dann ein neuer Typ): Übersetzen + Lesen je Runde, der neue Typ muss im Board stehen.</item>
/// </list>
/// Schreibt nichts auf die Platte (Änderungen nur im Speicher).
/// </summary>
public static class WarmMessung
{
    public static async Task<int> LaufAsync(Solution solution, string wurzel, TimeSpan laden)
    {
        Console.WriteLine($"\n── Warmer Workspace (E7) ──\n   Laden der Solution: {laden.TotalSeconds:0.0} s");
        var uhr = Stopwatch.StartNew();
        var kalt = await Projektlage.ErmittleAsync(solution, Console.WriteLine);
        var kaltUebersetzen = uhr.Elapsed;
        var kaltLesung = await Lesen.LiesAsync(solution, kalt, wurzel, uebersetzen: kaltUebersetzen);
        Console.WriteLine($"   kalt: Übersetzen {Ms(kaltLesung.Uebersetzen)} · Lesen {Ms(kaltLesung.Extrahieren)} · Routing {kaltLesung.Routing.Source}");

        uhr.Restart();
        var warm = await kalt.NurDomänenAsync(solution);
        var warmLesung = await Lesen.LiesAsync(solution, warm, wurzel, kaltLesung.CompositionRoot, uhr.Elapsed);
        Console.WriteLine($"   warm (unverändert): Übersetzen {Ms(warmLesung.Uebersetzen)} · Lesen {Ms(warmLesung.Extrahieren)} · Routing {warmLesung.Routing.Source}");
        Console.WriteLine($"     Phasen: {string.Join(" · ", warmLesung.Phasen.Select(p => $"{p.Phase} {Ms(p.Dauer)}"))}");
        var unterschiede = Unterschiede(kaltLesung.Board, warmLesung.Board, "").Take(25).ToList();
        foreach (var d in (kaltLesung.Board["diagnosen"] as JsonArray ?? []).Select(x => x!.ToJsonString())
                     .Except((warmLesung.Board["diagnosen"] as JsonArray ?? []).Select(x => x!.ToJsonString()))) Console.WriteLine($"     nur kalt: {d}");
        foreach (var d in (warmLesung.Board["diagnosen"] as JsonArray ?? []).Select(x => x!.ToJsonString())
                     .Except((kaltLesung.Board["diagnosen"] as JsonArray ?? []).Select(x => x!.ToJsonString()))) Console.WriteLine($"     nur warm: {d}");
        foreach (var u in unterschiede) Console.WriteLine($"     ≠ {u}");
        var fehler = unterschiede.Count;
        Console.WriteLine(fehler == 0 ? "   ✓ warmes Board = kaltes Board" : $"   ✗ warmes Board weicht ab ({fehler}+ Stellen)");

        // Die Datei des ersten Deciders — eine echte Domänen-Datei, an der eine Geste ansetzen würde.
        var datei = kaltLesung.Dom.Aggregates.Select(a => a.DeciderDatei).FirstOrDefault(d => d != null);
        var docId = datei == null ? null : solution.GetDocumentIdsWithFilePath(datei).FirstOrDefault();
        if (docId == null) { Console.WriteLine("   ✗ keine Domänen-Datei zum Ändern gefunden"); return 2; }
        var text = await solution.GetDocument(docId)!.GetTextAsync();
        Console.WriteLine($"   Änderungen an {Path.GetRelativePath(wurzel, datei!)} (nur im Speicher):");

        var zeiten = new List<(string Art, TimeSpan Uebersetzen, TimeSpan Lesen)>();
        var stand = solution;
        for (var i = 1; i <= 5; i++)
        {
            // Abwechselnd: nur ein Kommentar (Trivia) bzw. ein neuer Typ (der im Board erscheinen muss).
            var neuerTyp = i % 2 == 0;
            var zusatz = neuerTyp ? $"\nnamespace WarmProbe {{ public sealed record WarmProbeWert{i}(int Wert); }}\n" : $"\n// warm {i}\n";
            stand = stand.WithDocumentText(docId, SourceText.From(text + zusatz));
            uhr.Restart();
            var lage = await kalt.NurDomänenAsync(stand);
            var uebersetzen = uhr.Elapsed;
            var lesung = await Lesen.LiesAsync(stand, lage, wurzel, kaltLesung.CompositionRoot, uebersetzen);
            zeiten.Add((neuerTyp ? "neuer Typ" : "Kommentar", lesung.Uebersetzen, lesung.Extrahieren));
            Console.WriteLine($"     {i}. {(neuerTyp ? "neuer Typ" : "Kommentar"),-9} Übersetzen {Ms(lesung.Uebersetzen),7} · Lesen {Ms(lesung.Extrahieren),7} · gesamt {Ms(lesung.Uebersetzen + lesung.Extrahieren),7}");
            if (neuerTyp && !lesung.BoardJson.Contains($"\"WarmProbeWert{i}\"", StringComparison.Ordinal))
            {
                Console.WriteLine($"   ✗ der neue Typ WarmProbeWert{i} fehlt im warmen Board");
                fehler++;
            }
        }
        var gesamt = zeiten.Select(z => (z.Uebersetzen + z.Lesen).TotalMilliseconds).OrderBy(x => x).ToList();
        Console.WriteLine($"   p50 Übersetzen+Lesen nach Text-Änderung: {gesamt[gesamt.Count / 2]:0} ms (min {gesamt[0]:0}, max {gesamt[^1]:0})");
        return fehler == 0 ? 0 : 2;
    }

    private static string Ms(TimeSpan t) => $"{t.TotalMilliseconds:0} ms";

    /// <summary>Strukturelle Unterschiede zweier JSON-Bäume als Pfade (Reihenfolge in Arrays zählt).</summary>
    private static IEnumerable<string> Unterschiede(JsonNode? a, JsonNode? b, string pfad)
    {
        if (a is JsonObject oa && b is JsonObject ob)
        {
            foreach (var k in oa.Select(x => x.Key).Union(ob.Select(x => x.Key)))
                foreach (var u in Unterschiede(oa[k], ob[k], pfad + "." + k)) yield return u;
        }
        else if (a is JsonArray xa && b is JsonArray xb)
        {
            if (xa.Count != xb.Count) { yield return $"{pfad}: {xa.Count} ≠ {xb.Count} Einträge"; yield break; }
            for (var i = 0; i < xa.Count; i++)
                foreach (var u in Unterschiede(xa[i], xb[i], $"{pfad}[{i}]")) yield return u;
        }
        else if (a?.ToJsonString() != b?.ToJsonString())
            yield return $"{pfad}: {Kurz(a)} ≠ {Kurz(b)}";
    }

    private static string Kurz(JsonNode? n) { var s = n?.ToJsonString() ?? "∅"; return s.Length > 80 ? s[..80] + "…" : s; }
}
