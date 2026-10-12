using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;

namespace GraphExtractor;

/// <summary>
/// Ein Lesen des Codes (Code → Board): Routing, Domänen-Extraktion, Graph, Composition Root, Board mit Karten-Orten, Code-Karten und
/// Lese-Befunden. Derselbe Weg für die CLI (kalt, ganze Solution) und die Werkbank (warm, nur Domänen-Compilations — E7).
/// </summary>
public sealed record Lesung(
    Projektlage Lage, RoutingTruth Routing, DomainModel Dom, KnowledgeGraph Graph, CompositionRoot CompositionRoot, JsonObject Board,
    TimeSpan Uebersetzen, TimeSpan Extrahieren, IReadOnlyList<(string Phase, TimeSpan Dauer)> Phasen)
{
    public string BoardJson => ModellMapper.Serialisiere(Board);

    /// <summary>Die Lese-Befunde, wie das Board sie trägt (Karte, Datei relativ, Zeile).</summary>
    public IEnumerable<JsonObject> Befunde => (Board["lesebefunde"] as JsonArray ?? []).OfType<JsonObject>();
}

public static class Lesen
{
    /// <param name="zusammensetzung">
    /// Die Composition Root aus einem früheren kalten Lesen — das warme Lesen übersetzt die Hosts nicht (E7); ohne Angabe wird sie aus den
    /// Hosts der Lage gelesen.
    /// </param>
    /// <param name="uebersetzen">Die Zeit, die das Übersetzen der Lage schon gekostet hat (nur für die Messung).</param>
    public static async Task<Lesung> LiesAsync(Solution solution, Projektlage lage, string wurzel, CompositionRoot? zusammensetzung = null,
        TimeSpan uebersetzen = default)
    {
        var uhr = Stopwatch.StartNew();
        var phasen = new List<(string, TimeSpan)>();
        var takt = Stopwatch.StartNew();
        void Phase(string name) { phasen.Add((name, takt.Elapsed)); takt.Restart(); }
        // Warm (ohne Laufzeit-Compilation, E7) gelten die Decide-Signaturen; kalt das Generat (fehlt es: dieselbe Ableitung, als Warnung).
        var routing = lage.LaufzeitCompilation == null
            ? RoutingTruth.AusDecideSignaturen(lage.Compilations)
            : RoutingTruth.FromCompilations(lage.Compilations, lage.Quelltexte);
        Phase("Routing");
        var extractor = new DomainExtractor(lage.Compilations, lage.DomänenAssemblies, lage.Quelltexte);
        Phase("Typen");
        var dom = extractor.Extract();
        Phase("Extraktion");
        dom.Wurzel = wurzel;
        foreach (var (ns, dir) in lage.ProjektWurzeln()) dom.ProjektWurzeln[ns] = dir;
        var graph = new GraphBuilder(routing, dom).Build();
        Phase("Graph");
        var cr = zusammensetzung ?? await new CompositionRootExtractor(solution, lage, routing, dom).ExtractAsync();
        Phase("Composition Root");
        var board = ModellMapper.ZuBoard(graph, dom, cr, Umgebungen.Lies(wurzel));
        Phase("Board");
        return new Lesung(lage, routing, dom, graph, cr, board, uebersetzen, uhr.Elapsed, phasen);
    }
}
