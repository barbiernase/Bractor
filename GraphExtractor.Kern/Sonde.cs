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
/// Property-Records, zwei Aggregate in einem Namespace, Decider in eigener Datei, Fristen in der Entscheidung, Funktionen als
/// Klasse und als Schnittstelle (mit Dienst, Lese-Fähigkeit, Strom), frei benannte Stores, class-ReadModel, OneOf-Antwort,
/// Konstanten-Ids.
///
/// Dazu Bausteine, die im Editor GEZEICHNET werden (<c>Sonde/Gezeichnet.gesten.json</c>: neuer Store mit einer im Panel benannten
/// und einer abgeleiteten Fähigkeit, Projektion, Reader, Query/Response/ReadModel, Akteur, Client, Dienst): ein
/// Gesten-Skript — je Geste ein Karten-Delta wie aus dem Browser, geschrieben vom Gesten-Motor (<see cref="Gesten"/>) in den Fork, danach neu
/// gelesen (Werkbank P7.3). Das Soll beschreibt, was der Extractor daraus wieder lesen MUSS.
///
/// Ablauf: Sonde im SPEICHER in ein Aggregat-Projekt der Solution legen (Fork, nichts auf Platte), die ECHTE Pipeline
/// fahren (Projektlage → Extraktion → Board-Modell), das Inventar der Sonde mit dem VON HAND geschriebenen Soll
/// (<c>Sonde/soll.txt</c>) vergleichen — nicht mit einer Ausgabe des Extractors selbst — und auf dem Fork die volle
/// Paritäts-Prüfung (Inventar + Fixpunkt) laufen lassen.
/// </summary>
public static class Sonde
{
    /// <summary>
    /// Die Wurzel-Namespaces der Sonde — gelesen aus ihren eigenen Quelltexten (erstes Segment jeder Namespace-Deklaration), nie getippt:
    /// neben „Leihwesen“ liegen fremde Szenarien in eigenen Wurzeln (Plan Pub/Sub-Neustrukturierung §6, Phase 0), damit das Modell nicht
    /// auf eine Beispieldomäne passt.
    /// </summary>
    private static readonly IReadOnlySet<string> Wurzeln = Quellen()
        .SelectMany(q => CSharpSyntaxTree.ParseText(q.Text).GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        .Select(n => n.Name.ToString().Split('.')[0]).ToHashSet(StringComparer.Ordinal);

    private static bool InSonde(string? ns) => ns != null && Wurzeln.Contains(ns.Split('.')[0]);

    public static async Task<int> PruefeAsync(Solution solution, Projektlage lage, string solutionDir, bool zeigeIst, string? boardAusgabe = null)
    {
        // Ziel: ein Projekt, das Aggregate trägt (dort laufen die Domänen-Generatoren, der Vertrag ist referenziert).
        var ziel = lage.Analyse.Where(a => lage.AggregatAssemblies.Contains(a.Compilation.AssemblyName ?? ""))
            .OrderBy(a => a.Projekt.Name, StringComparer.Ordinal).First().Projekt;
        var fork = solution;
        foreach (var (name, text) in Quellen())
            fork = fork.AddDocument(DocumentId.CreateNewId(ziel.Id), name, text, folders: new[] { "__sonde" },
                filePath: Path.Combine(Path.GetDirectoryName(ziel.FilePath)!, "__sonde", name));
        Console.WriteLine($"   Sonde „{string.Join("“, „", Wurzeln.Order(StringComparer.Ordinal))}\" ({Quellen().Count()} Dateien) im Speicher in {ziel.Name} gelegt.");

        // ── GEZEICHNET = Gesten (Werkbank P7.3): je Geste ein Karten-Delta wie aus dem Browser, geschrieben vom Gesten-Motor in den Fork
        //    (Scaffolder für Neues, Abgleich für Geändertes), danach neu gelesen — die nächste Geste sieht den neuen Code-Stand wie in der Werkbank. ──
        CompositionRoot? cr1 = null;
        var gezeichnet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var geste in (JsonNode.Parse(Ressource("Gezeichnet.gesten.json"))!["gesten"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var lageG = await Projektlage.ErmittleAsync(fork, _ => { });
            var analyseG = ParitaetsPruefung.Analysiere(lageG.Compilations, lageG.DomänenAssemblies, lageG.Quelltexte);
            analyseG.Dom.Wurzel = solutionDir;
            foreach (var (ns, dir) in lageG.ProjektWurzeln()) analyseG.Dom.ProjektWurzeln[ns] = dir;
            cr1 ??= await new CompositionRootExtractor(fork, lageG, analyseG.Routing, analyseG.Dom).ExtractAsync();
            var texte = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var d in fork.Projects.SelectMany(x => x.Documents).Where(d => d.FilePath != null))
                texte[Path.GetFullPath(d.FilePath!)] = (await d.GetTextAsync()).ToString();
            var puffer = new Dateipuffer(solutionDir, p => texte.GetValueOrDefault(p) ?? (File.Exists(p) ? File.ReadAllText(p) : null));
            var bericht = Gesten.Schreibe(ModellMapper.ZuBoard(analyseG.Graph, analyseG.Dom, cr1), KartenDelta.AusJson(geste), puffer);
            foreach (var patch in bericht.Patch)
            {
                var voll = puffer.Voll(patch.Datei);
                gezeichnet.Add(voll);
                var doc = fork.Projects.SelectMany(x => x.Documents).FirstOrDefault(d => d.FilePath != null && Path.GetFullPath(d.FilePath) == voll);
                fork = doc != null
                    ? patch.Nachher == null ? fork.RemoveDocument(doc.Id) : fork.WithDocumentText(doc.Id, Microsoft.CodeAnalysis.Text.SourceText.From(patch.Nachher))
                    : fork.AddDocument(DocumentId.CreateNewId(ziel.Id), Path.GetFileName(voll), patch.Nachher ?? "", folders: new[] { "__sonde" }, filePath: voll);
                if (zeigeIst) Console.WriteLine($"\n── {patch.Datei} ({(patch.Vorher == null ? "neu" : "geändert")}) ──\n{patch.Nachher}");
            }
            Console.WriteLine($"   Geste „{(string?)geste["titel"]}“: {bericht.Patch.Count} Datei(en) geschrieben"
                              + (bericht.Schreiber.Uebersprungen.Count > 0 ? $", übersprungen: {string.Join("; ", bericht.Schreiber.Uebersprungen)}" : "") + ".");
        }

        var lage2 = await Projektlage.ErmittleAsync(fork, Console.WriteLine);
        var fehler = 0;

        // Die Sonde muss kompilieren — sonst prüft sie nichts.
        foreach (var c in lage2.DomänenCompilations)
            foreach (var d in c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error
                                                             && d.Location.SourceTree?.FilePath is { } pfad
                                                             && (pfad.Contains("__sonde") || gezeichnet.Contains(Path.GetFullPath(pfad)))).Take(20))
            {
                Console.WriteLine($"   ✗ Sonde kompiliert nicht: {Path.GetFileName(d.Location.SourceTree!.FilePath)} {d.Id}: {d.GetMessage()}");
                fehler++;
            }

        var analyse = ParitaetsPruefung.Analysiere(lage2.Compilations, lage2.DomänenAssemblies, lage2.Quelltexte);
        analyse.Dom.Wurzel = solutionDir;
        foreach (var (ns, dir) in lage2.ProjektWurzeln()) analyse.Dom.ProjektWurzeln[ns] = dir;
        var cr = await new CompositionRootExtractor(fork, lage2, analyse.Routing, analyse.Dom).ExtractAsync();
        var board = ModellMapper.ZuBoardJson(analyse.Graph, analyse.Dom, cr);
        if (boardAusgabe != null) { await File.WriteAllTextAsync(boardAusgabe, board); Console.WriteLine($"   Board-Modell (inkl. Sonde) → {boardAusgabe}"); }

        // ── Soll ⇄ Ist ──
        var ist = Inventar(JsonNode.Parse(board)!.AsObject()).Select(z => z.TrimEnd()).ToList();
        if (zeigeIst) { Console.WriteLine("\n── Ist-Inventar der Sonde ──"); foreach (var z in ist) Console.WriteLine(z); }
        var soll = Ressource("soll.txt").Replace("\r\n", "\n").Split('\n')
            .Select(z => z.TrimEnd()).Where(z => z.Length > 0 && !z.StartsWith('#')).ToList();
        foreach (var z in soll.Where(z => !z.StartsWith("lage ", StringComparison.Ordinal)).Except(ist)) { Console.WriteLine($"   ✗ fehlt/abweichend:  {z}"); fehler++; }
        foreach (var z in ist.Except(soll)) { Console.WriteLine($"   ✗ unerwartet:        {z}"); fehler++; }
        if (fehler == 0) Console.WriteLine($"   ✓ Inventar: {soll.Count(z => !z.StartsWith("lage ", StringComparison.Ordinal))} Soll-Fakten erkannt, nichts Unerwartetes.");

        // Graph-Diagnosen, die Sonden-Typen nennen (Namen aus dem Ist-Inventar).
        var sondenNamen = ist.Select(x => x.Split(' ')).Where(t => t.Length > 1).Select(t => Kurz(t[1])).ToHashSet(StringComparer.Ordinal);
        foreach (var d in analyse.Graph.Views.Diagnostics.Where(d => d.Severity != "info" && sondenNamen.Any(n => d.Message.Contains($"'{n}'"))))
            Console.WriteLine($"   [Graph-Diagnose {d.Severity}] {d.Code}: {d.Message}");

        // ── Volle Parität auf dem Fork (Inventar + Fixpunkt: zurückschreiben, neu kompilieren, neu extrahieren) ──
        var befunde = await ParitaetsPruefung.PruefeAsync(fork, lage2, analyse, board);
        foreach (var b in befunde.Where(b => b.Schweregrad == "error" && b.Bereich != "Lage"))
        {
            Console.WriteLine($"   ✗ [{b.Bereich}] {b.Meldung}");
            fehler++;
        }

        // ── Lage (docs/07): die Sonde liegt absichtlich falsch — die Prüfung muss genau ihre Typen in ihre Rollen-Projekte weisen
        //    (Soll-Zeilen „lage Typ -> Projekt“) und darf nichts außerhalb der Sonde melden. So beweist sie, dass sie anschlägt. ──
        var lageBefunde = befunde.Where(b => b.Bereich == "Lage" && b.Schweregrad == "error").Select(b => b.Meldung).ToList();
        var rolle = new System.Text.RegularExpressions.Regex(@"^'(?<typ>[^']+)' ist .+ und gehört nach (?<ziel>\S+), nicht nach");
        var lageIst = lageBefunde.Select(m => rolle.Match(m)).Where(m => m.Success).Select(m => $"lage {m.Groups["typ"].Value} -> {m.Groups["ziel"].Value}").ToList();
        var lageSoll = soll.Where(z => z.StartsWith("lage ", StringComparison.Ordinal)).ToList();
        foreach (var z in lageSoll.Except(lageIst)) { Console.WriteLine($"   ✗ Lage fehlt:      {z}"); fehler++; }
        foreach (var z in lageIst.Except(lageSoll)) { Console.WriteLine($"   ✗ Lage unerwartet: {z}"); fehler++; }
        // Ohne Sonde ist die Lage der Solution sauber — dann stammt jeder Befund auf dem Fork aus der Sonde.
        foreach (var m in Lagepruefung.Pruefe(solution, lage)) { Console.WriteLine($"   ✗ Lage außerhalb der Sonde: {m}"); fehler++; }
        if (lageSoll.Count > 0 && lageIst.Count == lageSoll.Count && fehler == 0)
            Console.WriteLine($"   ✓ Lage: {lageIst.Count} Sonden-Typen ins Projekt ihrer Rolle gewiesen ({lageBefunde.Count} Befunde, alle in der Sonde).");
        Console.WriteLine(fehler == 0
            ? "\n✅ Sonde: die unbekannte Domäne wird vollständig aus dem Code erkannt und verlustfrei zurückgeschrieben."
            : $"\n❌ Sonde: {fehler} Abweichung(en).");
        return fehler == 0 ? 0 : 3;
    }

    private static string Kurz(string full) => full[(full.LastIndexOf('.') + 1)..];

    /// <summary>
    /// Das Inventar der Sonde als kanonische Zeilen — aus dem Board und seiner Sicht (der Kartenliste des Servers: dem, was der Editor sieht),
    /// gefiltert auf den Sonden-Namespace. Ein Fakt je Zeile, deterministisch sortiert.
    /// </summary>
    private static List<string> Inventar(JsonObject b)
    {
        var z = new List<string>();
        bool Sonde(JsonNode? n) => InSonde((string?)n?["namespace"]);
        string S(JsonNode? n, string k) => (string?)n?[k] ?? "";
        IEnumerable<JsonNode> A(JsonNode? n, string k) => (n?[k] as JsonArray)?.Where(x => x != null).Select(x => x!) ?? [];
        string Felder(JsonNode? n, string k = "felder") => string.Join(", ", A(n, k).Select(f =>
            $"{S(f, "name")}:{S(f, "typ")}" + (f["standard"] is { } st ? $"={st}" : "")
            + (f["zugriff"] is { } zg ? $" {zg}" : "") + (f["pflicht"]?.GetValue<bool>() == true ? " required" : "")));

        var sondenAggregate = A(b, "aggregate").Where(Sonde).Select(a => S(a, "name")).ToHashSet();

        foreach (var r in A(b, "records").Where(Sonde))
            z.Add($"record {S(r, "namespace")}.{S(r, "name")} {S(r, "kind")}"
                  + (r["istErzeugung"]?.GetValue<bool>() == true ? " erzeugung" : "")
                  + (b["sicht"]?["karten"]?["rec:" + S(r, "name")]?["aggregat"] is { } ag ? $" aggregat={ag}" : "")
                  + (r["typart"] is { } ta ? $" typart={ta}" : "")
                  + (r["treiber"] is { } tr ? $" treiber={tr}" : "")
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
        string Konstruktor(JsonNode? n) => "(" + string.Join(", ", A(n, "konstruktor").Select(p => $"{S(p, "name")}:{S(p, "typ")}")) + ")";
        // Funktionen: Klasse oder Schnittstelle (mit Zustand und einer Adresse, Plan §2.9), je Eingang Herkunft/Hinweis, Ausgaben (Strom; keine =
        //   „lernt“), Lese-Fähigkeiten; Konstruktor, Umsetzungen.
        foreach (var f in A(b, "bausteinFunktionen").Where(Sonde))
            z.Add($"funktion-auf {S(f, "namespace")}.{S(f, "name")} {(f["klasse"]?.GetValue<bool>() == true ? "klasse" + Konstruktor(f) : "schnittstelle")}"
                + (f["zustand"] is { } zu ? $" zustand={zu}" : "") + (f["eineAdresse"]?.GetValue<bool>() == true ? " eineadresse" : "")
                + (f["lebenszyklus"]?.GetValue<bool>() == true ? " lebenszyklus" : "") + " | "
                + string.Join(" || ", A(f, "eingaenge").Select(e => S(e, "eingang")
                    + (e["hinweis"]?.GetValue<bool>() == true ? " hinweis" : "") + (e["herkunft"]?.GetValue<bool>() == true ? " herkunft" : "")
                    + " -> " + (e["strom"]?.GetValue<bool>() == true ? "strom " : "")
                    + (A(e, "ausgaben").Any() ? string.Join("; ", A(e, "ausgaben").Select(x => (string?)x)) : "lernt")
                    + (A(e, "faehigkeiten").Any() ? " liest " + string.Join(", ", A(e, "faehigkeiten").Select(p => $"{S(p, "typ")} {S(p, "name")}")) : "")))
                + (A(f, "umsetzungen").Any() ? " umsetzung " + string.Join(", ", A(f, "umsetzungen").Select(u => S(u, "name") + Konstruktor(u))) : ""));
        // Domänen-Dienste (konzept-werkbank §12.3 E11): Signatur (Methoden) und Implementierungen mit Konstruktor.
        foreach (var d in A(b, "dienste").Where(Sonde))
            z.Add($"dienst {S(d, "namespace")}.{S(d, "name")} | "
                  + string.Join("; ", A(d, "methoden").Select(m => $"{S(m, "name")}({string.Join(", ", A(m, "parameter").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}):{S(m, "rueckgabe")}"))
                  + " | " + string.Join("; ", A(d, "implementierungen").Select(i => $"impl {S(i, "name")}{Konstruktor(i)}")));

        // Leseseite: Store-Fn-Ids → „Store.Fn" auflösen.
        var fnName = new Dictionary<string, string>();
        foreach (var st in A(b, "stores"))
            foreach (var f in A(st, "writeFns").Concat(A(st, "readFns"))) fnName[S(f, "_id")] = $"{S(st, "name")}.{S(f, "name")}";
        string Fns(JsonNode h) => string.Join(", ", A(h, "fns").Select(x => fnName.GetValueOrDefault((string?)x ?? "", "?")));

        foreach (var rm in A(b, "readModels").Where(Sonde))
            z.Add($"readmodel {S(rm, "namespace")}.{S(rm, "name")} store={S(rm, "store")}"
                  + (rm["geteilt"]?.GetValue<bool>() == true ? " geteilt" : "") + $" | {Felder(rm)}");
        foreach (var st in A(b, "stores").Where(Sonde))
            z.Add($"store {S(st, "namespace")}.{S(st, "name")} | schreibt {string.Join(", ", A(st, "writeFns").Select(f => $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))})"))}"
                  + $" | liest {string.Join(", ", A(st, "readFns").Select(f => $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}):{S(f, "rueckgabe")}"))}");
        foreach (var st in A(b, "stores").Where(Sonde))
            foreach (var (f, art) in A(st, "writeFns").Select(f => (f, "schreibt")).Concat(A(st, "readFns").Select(f => (f, "liest"))))
                z.Add($"faehigkeit {(string?)f["sig"]?["namespace"] ?? S(st, "namespace")}.{S(f, "faehigkeit")} {art} {S(st, "name")}.{S(f, "name")}");
        foreach (var p in A(b, "projektionen").Where(Sonde))
            z.Add($"projektion {S(p, "namespace")}.{S(p, "name")} id={S(p, "subscriberId")}{(p["pull"]?.GetValue<bool>() == true ? " pull" : "")} | "
                  + string.Join("; ", A(p, "handles").Select(h => $"{S(h, "event")} -> {Fns(h)}")));
        foreach (var r in A(b, "reader").Where(Sonde))
            z.Add($"reader {S(r, "namespace")}.{S(r, "name")} projektion={S(r, "projektion")} trackDeps={r["trackDeps"]} | "
                  + string.Join("; ", A(r, "handles").Select(h => $"{S(h, "query")} -> {Fns(h)} => {string.Join("|", A(h, "responses").Select(x => (string?)x))}")));
        foreach (var ak in A(b, "akteure").Where(Sonde))
            z.Add($"akteur {S(ak, "namespace")}.{S(ak, "name")}{(ak["art"]?.GetValue<string>() is { Length: > 0 } art ? " " + art : "")} | darf {string.Join(", ", A(ak, "darf").Select(x => (string?)x))}");
        // Clients (docs/konzept-akteure.md §4): je Client sein Rand — trägt (Funktionen), sendet, fragt, hört.
        foreach (var c in A(b, "clients").Where(Sonde))
            z.Add($"client {S(c, "namespace")}.{S(c, "name")} | traegt {string.Join(", ", A(c, "traegt").Select(x => (string?)x))}; "
                  + $"sendet {string.Join(", ", A(c, "sendet").Select(x => (string?)x))}; fragt {string.Join(", ", A(c, "fragt").Select(x => (string?)x))}; "
                  + $"hoert {string.Join(", ", A(c, "kenntnis").Select(x => (string?)x))}");
        // Regel Z: Zustandsfelder der zustandslosen Übersetzer (Symbol-Fakt, im Build CQRS083 GR-ZUSTAND).
        foreach (var o in A(b, "projektionen").Where(Sonde).Where(o => A(o["code"], "zustand").Any()))
            z.Add($"zustand projektion {S(o, "namespace")}.{S(o, "name")} | {string.Join(", ", A(o["code"], "zustand").Select(x => (string?)x))}");
        // Handle-Verträge: was jeder Handler erzeugen KANN und welche Store-Fns er rufen DARF — nur aus der Signatur.
        string Ausgaenge(JsonNode h) => string.Join("; ", A(h, "ausgaenge").Select(a =>
            $"{S(a, "art")}:{(S(a, "art") == "storefn" ? S(a, "store") + "." : "")}{S(a, "typ")}"));
        foreach (var (liste, disc) in new[] { ("projektionen", "event"), ("reader", "query") })
            foreach (var o in A(b, liste).Where(Sonde))
                foreach (var h in A(o, "handles"))
                    z.Add($"ausgang {S(o, "name")}.{S(h, disc)} {S(h, "form")}{(h["signaturOffen"]?.GetValue<bool>() == true ? " offen" : "")} | {Ausgaenge(h)}");

        // Die Sicht (Werkbank P7.3): Domäne und Block je Karte, wie sie der Editor vom Server bekommt — aus dem Graphen, nie aus dem Namespace.
        var sicht = Sicht.Aus(b);
        foreach (var k in sicht.Karten.Where(k => k.Art is "valueobject" or "enum" or "query" or "queryresponse" or "readmodel" or "store" or "projektion"
                                                       or "reader" or "akteur" or "dienst" && Sonde(k.Ref)))
            z.Add($"sicht {S(k.Ref, "namespace")}.{k.Name} {k.Art} domaene={sicht.Domaene(k)} block={sicht.Block(k)}"
                  + (sicht.Inseln.Contains(k.Id) ? " insel" : ""));

        // Code-Karten (Werkbank P7.4): Hilfscode ohne Baustein-Rolle, roh — mit der Zahl seiner Deklarationen (partial: _ort + _teile).
        foreach (var k in sicht.Karten.Where(k => k.Art == "code" && Sonde(k.Ref)))
            z.Add($"code {S(k.Ref, "namespace")}.{k.Name} {S(k.Ref, "art")} teile={1 + A(k.Ref, "_teile").Count()} domaene={sicht.Domaene(k)} block={sicht.Block(k)}");

        return z.Select(x => x.TrimEnd()).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>Die Quelltexte der Sonden-Domäne (Dateiname, Text) — auch die Fixture der Gesten-Tests im Prüfstand.</summary>
    public static IEnumerable<(string Name, string Text)> Quelltexte() => Quellen();

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
