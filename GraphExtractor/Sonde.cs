using System.Reflection;
using System.Text.Json.Nodes;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>
/// Die AGNOSTIK-SONDE (<c>dotnet run --project GraphExtractor -- --sonde</c>): beweist, dass der Extractor eine ihm
/// UNBEKANNTE Domäne allein aus ihrem Code vollständig erkennt — ohne Vorwissen, Namenskonventionen oder Heuristiken.
///
/// Die Sonden-Domäne „Leihwesen" (eingebettete Ressourcen <c>Sonde/*.cs.txt</c>) nutzt absichtlich jede gültige
/// Schreibweise, die eine Konvention brechen würde: fremder Wurzel-Namespace, Commands in eigenem Sub-Namespace,
/// Property-Records, zwei Aggregate in einem Namespace, Decider in eigener Datei, Saga mit expliziter
/// Interface-Implementierung, frei benannte Stores, class-ReadModel, OneOf-Antwort, Konstanten-Ids, Reaktion, Pipeline
/// mit Konfig und einem nie ausgegebenen Objekt.
///
/// Dazu eine im Editor GEZEICHNETE Leseseite (<c>Sonde/Gezeichnet.board.json</c>: neuer Store mit einer im Panel benannten
/// und einer abgeleiteten Fähigkeit, Projektion, Reaktion, Reader, Query/Response/ReadModel): sie wird an das Board der Sonde
/// gehängt, über <see cref="BoardLeseseite"/> ins Modell gelesen, vom <see cref="Scaffolder"/> geschrieben und liegt dann als
/// Code im Fork — das Soll beschreibt, was der Extractor daraus wieder lesen MUSS.
///
/// Ablauf: Sonde im SPEICHER in ein Aggregat-Projekt der Solution legen (Fork, nichts auf Platte), die ECHTE Pipeline
/// fahren (Projektlage → Extraktion → Board-Modell), das Inventar der Sonde mit dem VON HAND geschriebenen Soll
/// (<c>Sonde/soll.txt</c>) vergleichen — nicht mit einer Ausgabe des Extractors selbst — und auf dem Fork die volle
/// Paritäts-Prüfung (Inventar + Fixpunkt) laufen lassen.
/// </summary>
public static class Sonde
{
    /// <summary>Der Wurzel-Namespace der Sonden-Domäne — nur die Sonde selbst kennt ihn.</summary>
    private const string Wurzel = "Leihwesen";

    public static async Task<int> PruefeAsync(Solution solution, Projektlage lage, string solutionDir, bool zeigeIst, string? boardAusgabe = null)
    {
        // Ziel: ein Projekt, das Aggregate trägt (dort laufen die Domänen-Generatoren, der Vertrag ist referenziert).
        var ziel = lage.Analyse.Where(a => lage.AggregatAssemblies.Contains(a.Compilation.AssemblyName ?? ""))
            .OrderBy(a => a.Projekt.Name, StringComparer.Ordinal).First().Projekt;
        var fork = solution;
        foreach (var (name, text) in Quellen())
            fork = fork.AddDocument(DocumentId.CreateNewId(ziel.Id), name, text, folders: new[] { "__sonde" },
                filePath: Path.Combine(Path.GetDirectoryName(ziel.FilePath)!, "__sonde", name));
        Console.WriteLine($"   Sonde „{Wurzel}\" ({Quellen().Count()} Dateien) im Speicher in {ziel.Name} gelegt.");

        // ── Die gezeichnete Leseseite: Board der Sonde + Zeichnung → Modell → Scaffolder → in den Fork ──
        var lage1 = await Projektlage.ErmittleAsync(fork, _ => { });
        var analyse1 = ParitaetsPruefung.Analysiere(lage1.Compilations, lage1.DomänenAssemblies);
        analyse1.Dom.Wurzel = solutionDir;
        foreach (var (ns, dir) in lage1.ProjektWurzeln()) analyse1.Dom.ProjektWurzeln[ns] = dir;
        var cr1 = await new CompositionRootExtractor(fork, lage1, analyse1.Routing, analyse1.Dom).ExtractAsync();
        var gezeichnet = Zeichne(ModellMapper.ZuBoardJson(analyse1.Graph, analyse1.Dom, cr1), Ressource("Gezeichnet.board.json"), out var namen);
        var modell = BoardLeseseite.AusBoard(gezeichnet);
        // Neue Selbst-Nachrichten legt das Modell selbst an (Self-Tick ohne Record) — sie gehören zur Zeichnung.
        namen.UnionWith(modell.Records.Where(r => r.Kind == RecordArt.Selbst && r.Datei == null).Select(r => r.Name));
        foreach (var st in (modell.Lesen?.Stores ?? []).Where(s => namen.Contains(s.Name)))
        {
            namen.UnionWith(st.Fns.Select(f => f.Name));
            if (st.Impl != null) namen.Add(st.Impl.Name);
        }
        var neu = Scaffolder.Generiere(modell).Where(d =>
        {
            var typen = CSharpSyntaxTree.ParseText(d.Inhalt).GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax).Select(t => t.Identifier.Text).ToList();
            return typen.Count > 0 && typen.All(namen.Contains);
        }).ToList();
        foreach (var (d, i) in neu.Select((d, i) => (d, i)))
        {
            var name = $"Gezeichnet.{i}.{Path.GetFileName(d.Pfad)}";   // eindeutig: zwei Namespaces können dieselbe Zieldatei haben
            fork = fork.AddDocument(DocumentId.CreateNewId(ziel.Id), name, d.Inhalt, folders: new[] { "__sonde" },
                filePath: Path.Combine(Path.GetDirectoryName(ziel.FilePath)!, "__sonde", name));
        }
        Console.WriteLine($"   Gezeichnet: {namen.Count} Typen über Board → Modell → Scaffolder in {neu.Count} Datei(en) geschrieben.");
        if (zeigeIst) foreach (var d in neu) Console.WriteLine($"\n── {d.Pfad} ({d.Art}) ──\n{d.Inhalt}");

        var lage2 = await Projektlage.ErmittleAsync(fork, Console.WriteLine);
        var fehler = 0;

        // Die Sonde muss kompilieren — sonst prüft sie nichts.
        foreach (var c in lage2.DomänenCompilations)
            foreach (var d in c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error
                                                             && d.Location.SourceTree?.FilePath.Contains("__sonde") == true).Take(20))
            {
                Console.WriteLine($"   ✗ Sonde kompiliert nicht: {Path.GetFileName(d.Location.SourceTree!.FilePath)} {d.Id}: {d.GetMessage()}");
                fehler++;
            }

        var analyse = ParitaetsPruefung.Analysiere(lage2.Compilations, lage2.DomänenAssemblies);
        analyse.Dom.Wurzel = solutionDir;
        foreach (var (ns, dir) in lage2.ProjektWurzeln()) analyse.Dom.ProjektWurzeln[ns] = dir;
        var cr = await new CompositionRootExtractor(fork, lage2, analyse.Routing, analyse.Dom).ExtractAsync();
        var board = ModellMapper.ZuBoardJson(analyse.Graph, analyse.Dom, cr);
        if (boardAusgabe != null) { await File.WriteAllTextAsync(boardAusgabe, board); Console.WriteLine($"   Board-Modell (inkl. Sonde) → {boardAusgabe}"); }

        // ── Soll ⇄ Ist ──
        var ist = Inventar(JsonNode.Parse(board)!.AsObject());
        if (zeigeIst) { Console.WriteLine("\n── Ist-Inventar der Sonde ──"); foreach (var z in ist) Console.WriteLine(z); }
        var soll = Ressource("soll.txt").Replace("\r\n", "\n").Split('\n')
            .Select(z => z.TrimEnd()).Where(z => z.Length > 0 && !z.StartsWith('#')).ToList();
        foreach (var z in soll.Except(ist)) { Console.WriteLine($"   ✗ fehlt/abweichend:  {z}"); fehler++; }
        foreach (var z in ist.Except(soll)) { Console.WriteLine($"   ✗ unerwartet:        {z}"); fehler++; }
        if (fehler == 0) Console.WriteLine($"   ✓ Inventar: {soll.Count} Soll-Fakten erkannt, nichts Unerwartetes.");

        // Graph-Diagnosen, die Sonden-Typen nennen (Namen aus dem Ist-Inventar).
        var sondenNamen = ist.Select(x => x.Split(' ')).Where(t => t.Length > 1).Select(t => Kurz(t[1])).ToHashSet(StringComparer.Ordinal);
        foreach (var d in analyse.Graph.Views.Diagnostics.Where(d => d.Severity != "info" && sondenNamen.Any(n => d.Message.Contains($"'{n}'"))))
            Console.WriteLine($"   [Graph-Diagnose {d.Severity}] {d.Code}: {d.Message}");

        // ── Volle Parität auf dem Fork (Inventar + Fixpunkt: zurückschreiben, neu kompilieren, neu extrahieren) ──
        var befunde = await ParitaetsPruefung.PruefeAsync(fork, lage2, analyse, board, ModellMapper.Ingress(cr));
        foreach (var b in befunde.Where(b => b.Schweregrad == "error"))
        {
            Console.WriteLine($"   ✗ [{b.Bereich}] {b.Meldung}");
            fehler++;
        }
        Console.WriteLine(fehler == 0
            ? "\n✅ Sonde: die unbekannte Domäne wird vollständig aus dem Code erkannt und verlustfrei zurückgeschrieben."
            : $"\n❌ Sonde: {fehler} Abweichung(en).");
        return fehler == 0 ? 0 : 3;
    }

    private static string Kurz(string full) => full[(full.LastIndexOf('.') + 1)..];

    /// <summary>
    /// Die Zeichnung an das Board hängen (Sammlungen anfügen — so, wie der Browser neue Karten anlegt). <paramref name="namen"/>
    /// = die gezeichneten Typnamen (Records, ReadModels, Stores, Projektionen/Reaktionen, Reader).
    /// </summary>
    private static string Zeichne(string board, string zeichnung, out HashSet<string> namen)
    {
        var b = JsonNode.Parse(board)!.AsObject();
        var z = JsonNode.Parse(zeichnung)!.AsObject();
        namen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (k, v) in z)
        {
            if (v is not JsonArray neu) continue;
            if (b[k] is not JsonArray liste) b[k] = liste = new JsonArray();
            foreach (var x in neu.ToList())
            {
                if ((string?)x?["name"] is { } n) namen.Add(n);
                liste.Add(x!.DeepClone());
            }
        }
        return b.ToJsonString();
    }

    /// <summary>
    /// Das Inventar der Sonde als kanonische Zeilen — aus dem BOARD-Modell (dem, was der Editor sieht), gefiltert auf
    /// den Sonden-Namespace. Ein Fakt je Zeile, deterministisch sortiert.
    /// </summary>
    private static List<string> Inventar(JsonObject b)
    {
        var z = new List<string>();
        bool Sonde(JsonNode? n) => ((string?)n?["namespace"])?.StartsWith(Wurzel, StringComparison.Ordinal) == true;
        string S(JsonNode? n, string k) => (string?)n?[k] ?? "";
        IEnumerable<JsonNode> A(JsonNode? n, string k) => (n?[k] as JsonArray)?.Where(x => x != null).Select(x => x!) ?? [];
        string Felder(JsonNode? n, string k = "felder") => string.Join(", ", A(n, k).Select(f =>
            $"{S(f, "name")}:{S(f, "typ")}" + (f["standard"] is { } st ? $"={st}" : "")
            + (f["zugriff"] is { } zg ? $" {zg}" : "") + (f["pflicht"]?.GetValue<bool>() == true ? " required" : "")));

        var sondenAggregate = A(b, "aggregate").Where(Sonde).Select(a => S(a, "name")).ToHashSet();

        foreach (var r in A(b, "records").Where(Sonde))
            z.Add($"record {S(r, "namespace")}.{S(r, "name")} {S(r, "kind")}"
                  + (r["istErzeugung"]?.GetValue<bool>() == true ? " erzeugung" : "")
                  + (r["aggregat"] is { } ag ? $" aggregat={ag}" : "")
                  + (r["typart"] is { } ta ? $" typart={ta}" : "")
                  + $" | {Felder(r)}");
        foreach (var e in A(b, "enums").Where(Sonde))
            z.Add($"enum {S(e, "namespace")}.{S(e, "name")} | {string.Join(", ", A(e, "werte").Select(w => (string?)w))}");
        foreach (var a in A(b, "aggregate").Where(Sonde))
            z.Add($"aggregat {S(a, "namespace")}.{S(a, "name")} | {Felder(a, "state")}");
        foreach (var d in A(b, "decider").Where(d => sondenAggregate.Contains(S(d, "aggregat"))))
            z.Add($"decide {S(d, "aggregat")}.{S(d, "command")} -> " + string.Join("; ", A(d, "ergibt").Select(o =>
                S(o, "event"))));
        foreach (var a in A(b, "applier").Where(a => sondenAggregate.Contains(S(a, "aggregat"))))
            z.Add($"apply {S(a, "aggregat")}.{S(a, "event")}");
        foreach (var s in A(b, "sagas").Where(Sonde))
            z.Add($"saga {S(s, "namespace")}.{S(s, "name")} auslöser={S(s, "triggerEvent")} | " + string.Join("; ", A(s, "schritte").Select(t =>
                $"wenn {string.Join("+", A(t, "wenn").Select(x => (string?)x))} -> {(t["sendeJe"]?.GetValue<bool>() == true ? "sendeJe" : "sende")} {S(t, "sende")}"
                + (t["kompensation"] is { } k ? $" kompensiert {k}" : ""))));

        // Leseseite: Store-Fn-Ids → „Store.Fn" auflösen.
        var fnName = new Dictionary<string, string>();
        foreach (var st in A(b, "stores"))
            foreach (var f in A(st, "writeFns").Concat(A(st, "readFns"))) fnName[S(f, "_id")] = $"{S(st, "name")}.{S(f, "name")}";
        string Fns(JsonNode h) => string.Join(", ", A(h, "fns").Select(x => fnName.GetValueOrDefault((string?)x ?? "", "?")));

        foreach (var rm in A(b, "readModels").Where(Sonde))
            z.Add($"readmodel {S(rm, "namespace")}.{S(rm, "name")} store={S(rm, "store")} | {Felder(rm)}");
        foreach (var st in A(b, "stores").Where(Sonde))
            z.Add($"store {S(st, "namespace")}.{S(st, "name")} | schreibt {string.Join(", ", A(st, "writeFns").Select(f => $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))})"))}"
                  + $" | liest {string.Join(", ", A(st, "readFns").Select(f => $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}):{S(f, "rueckgabe")}"))}");
        foreach (var st in A(b, "stores").Where(Sonde))
            foreach (var (f, art) in A(st, "writeFns").Select(f => (f, "schreibt")).Concat(A(st, "readFns").Select(f => (f, "liest"))))
                z.Add($"faehigkeit {(string?)f["sig"]?["namespace"] ?? S(st, "namespace")}.{S(f, "faehigkeit")} {art} {S(st, "name")}.{S(f, "name")}");
        foreach (var p in A(b, "projektionen").Where(Sonde))
            z.Add($"projektion {S(p, "namespace")}.{S(p, "name")} id={S(p, "subscriberId")}{(p["pull"]?.GetValue<bool>() == true ? " pull" : "")} | "
                  + string.Join("; ", A(p, "handles").Select(h => $"{S(h, "event")} -> {Fns(h)}")));
        foreach (var p in A(b, "reaktionen").Where(Sonde))
            z.Add($"reaktion {S(p, "namespace")}.{S(p, "name")} id={S(p, "subscriberId")} | "
                  + string.Join("; ", A(p, "handles").Select(h => $"{S(h, "event")} -> sendet {string.Join(", ", A(h, "sends").Select(x => (string?)x))}")));
        foreach (var r in A(b, "reader").Where(Sonde))
            z.Add($"reader {S(r, "namespace")}.{S(r, "name")} projektion={S(r, "projektion")} trackDeps={r["trackDeps"]} | "
                  + string.Join("; ", A(r, "handles").Select(h => $"{S(h, "query")} -> {Fns(h)} => {string.Join("|", A(h, "responses").Select(x => (string?)x))}")));
        foreach (var ak in A(b, "akteure").Where(Sonde))
            z.Add($"akteur {S(ak, "namespace")}.{S(ak, "name")}{(ak["dienst"]?.GetValue<bool>() == true ? " dienst" : "")} | darf {string.Join(", ", A(ak, "darf").Select(x => (string?)x))}");
        var sondenTrigger = new HashSet<string>();
        foreach (var p in A(b, "pipelines").Where(Sonde))
        {
            z.Add($"pipeline {S(p, "namespace")}.{S(p, "name")} id={S(p, "pipelineId")} konfigs={string.Join(",", A(p, "konfigs").Select(x => (string?)x))} | "
                  + string.Join("; ", A(p, "handles").Select(h => $"{S(h, "input")}({S(h, "inputKind")}) -> sendet {string.Join(", ", A(h, "sends").Select(x => (string?)x))}"
                                                                 + (A(h, "fns").Any() ? $" -> {Fns(h)}" : "")
                                                                 + (h["akteur"] is { } ak ? $" im Auftrag {ak}" : ""))));
            foreach (var h in A(p, "handles").Where(h => S(h, "inputKind") == "trigger")) sondenTrigger.Add(S(h, "input"));
        }
        // Regel Z: Zustandsfelder der zustandslosen Übersetzer (Symbol-Fakt, Validator-Hinweis GR-ZUSTAND).
        foreach (var (liste, art) in new[] { ("pipelines", "pipeline"), ("projektionen", "projektion"), ("reaktionen", "reaktion") })
            foreach (var o in A(b, liste).Where(Sonde).Where(o => A(o["code"], "zustand").Any()))
                z.Add($"zustand {art} {S(o, "namespace")}.{S(o, "name")} | {string.Join(", ", A(o["code"], "zustand").Select(x => (string?)x))}");
        // Handle-Verträge: was jeder Handler erzeugen KANN und welche Store-Fns er rufen DARF — nur aus der Signatur.
        string Ausgaenge(JsonNode h) => string.Join("; ", A(h, "ausgaenge").Select(a =>
            $"{S(a, "art")}:{(S(a, "art") == "storefn" ? S(a, "store") + "." : "")}{S(a, "typ")}"));
        foreach (var (liste, disc) in new[] { ("projektionen", "event"), ("reaktionen", "event"), ("reader", "query"), ("pipelines", "input") })
            foreach (var o in A(b, liste).Where(Sonde))
                foreach (var h in A(o, "handles"))
                    z.Add($"ausgang {S(o, "name")}.{S(h, disc)} {S(h, "form")}{(h["signaturOffen"]?.GetValue<bool>() == true ? " offen" : "")} | {Ausgaenge(h)}");
        foreach (var t in A(b, "triggers").Where(t => sondenTrigger.Contains(S(t, "msgName"))))
            z.Add($"trigger {S(t, "msgName")} | {Felder(t)}");

        return z.Select(x => x.TrimEnd()).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<(string Name, string Text)> Quellen() =>
        typeof(Sonde).Assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".cs.txt", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => (Dateiname(n), Ressource(n, voll: true)));

    private static string Dateiname(string ressource)
    {
        // „GraphExtractor.Sonde.Mahnvorgang.Decider.cs.txt" → „Mahnvorgang.Decider.cs"
        var ohne = ressource[(ressource.IndexOf(".Sonde.", StringComparison.Ordinal) + ".Sonde.".Length)..];
        return ohne[..^".txt".Length];
    }

    private static string Ressource(string name, bool voll = false)
    {
        var asm = Assembly.GetExecutingAssembly();
        var res = voll ? name : asm.GetManifestResourceNames().Single(n => n.EndsWith(".Sonde." + name, StringComparison.Ordinal));
        using var s = asm.GetManifestResourceStream(res)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
