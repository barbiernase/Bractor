using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Nodes;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GraphExtractor;

/// <summary>
/// Die AGNOSTIK-SONDE (<c>dotnet run --project GraphExtractor -- --sonde</c>): beweist, dass der Extractor eine ihm
/// UNBEKANNTE Domäne allein aus ihrem Code vollständig erkennt — ohne Vorwissen, Namenskonventionen oder Heuristiken.
///
/// Die Sonden-Domänen (eingebettete Ressourcen <c>Sonde/*.cs.txt</c>: „Leihwesen“ und fremde Wurzeln) nutzen absichtlich jede gültige
/// Schreibweise, die eine Konvention brechen würde: fremde Wurzel-Namespaces, Commands in eigenem Sub-Namespace, Property-Records, zwei Aggregate
/// in einem Namespace, Decider in eigener Datei, Fristen in der Entscheidung, frei benannte Stores, class-ReadModel, OneOf-Antwort, Stores aller
/// drei Klassen — und ALLE allgemeinen Primitive des Funktionsmodells (Plan „Commands und Funktionsgraphen“): Funktionen als Klassen (Technik als
/// Schnittstelle und als Klasse, Konfig-Record, Zustand, Lese-Fähigkeit, Strom, Antwort auf den eigenen Command), Graphen mit jedem Muster aus
/// Entscheidung 5 (Folge, Fork, Wahl, Join, Je + Sammeln, Rennen, Warten mit Frist), Graph im Graphen, eine generische Funktion mit Typargument als
/// Knoten, Quellfunktionen (eigene und aus dem Katalog) am Kopf, Commands ohne Ziel von Akteuren an einen Graph-Kopf, Reaktionen (ein Akteur hört
/// ein Fakt und darf den Command, der folgt — ein Decide gibt nur Fakten aus), Clients als Records, Hinweise nur als Ausgaben.
///
/// Dazu Bausteine, die im Editor GEZEICHNET werden (<c>Sonde/Gezeichnet.gesten.json</c>: neuer Store mit einer im Panel benannten
/// und einer abgeleiteten Fähigkeit, Projektion, Reader, Query/Response/ReadModel, Akteur mit darf und hoert, Client als Record, eine neue
/// Funktion mit Technik, ein Knoten mehr an einem Graphen, ein neuer Graph mit generischem Knoten): ein Gesten-Skript — je Geste ein Karten-Delta
/// wie aus dem Browser, geschrieben vom Gesten-Motor (<see cref="Gesten"/>) in den Fork, danach neu gelesen (Werkbank P7.3). Das Soll beschreibt,
/// was der Extractor daraus wieder lesen MUSS. Die Sonde hält die Regeln des Builds ein (Analyzer, Lesarten der Generatoren) — sonst bewiese sie
/// an Code, den der Build ablehnt.
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
    /// neben „Leihwesen“ liegen fremde Szenarien in eigenen Wurzeln (docs/12 §12.6), damit das Modell nicht
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
        var (fork, katalog) = MitKatalog(solution, solutionDir, ziel.Id);
        foreach (var (name, text) in Quelltexte())
            fork = fork.AddDocument(DocumentId.CreateNewId(ziel.Id), name, text, folders: new[] { "__sonde" },
                filePath: Path.Combine(Path.GetDirectoryName(ziel.FilePath)!, "__sonde", name));
        // Was den fremden Katalog nennt (Graphen mit Übersetzungen), liegt bei den Abläufen — nur sie kennen einen Katalog (docs/07).
        var ablaeufe = fork.Projects.First(p => p.AssemblyName == Abstractions.Projektstruktur.Projekt(Abstractions.Projektstruktur.StandardWurzel,
            Abstractions.Projektstruktur.Rolle.Ablaeufe));
        foreach (var (name, text) in Ablaeufetexte())
            fork = fork.AddDocument(DocumentId.CreateNewId(ablaeufe.Id), name, text, folders: new[] { "__sonde" },
                filePath: Path.Combine(Path.GetDirectoryName(ablaeufe.FilePath)!, "__sonde", name));
        // Der fremde Katalog als eigenes Projekt: jede Datei im Bereichs-Ordner, wie die Projektstruktur ihn will (docs/07 §7.3).
        var gezeichnet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, text) in Katalogtexte())
        {
            var (bereich, datei) = KatalogDatei(name);
            var pfad = Path.Combine(solutionDir, SondenKatalog, bereich, datei);
            fork = fork.AddDocument(DocumentId.CreateNewId(katalog), datei, text, folders: new[] { bereich }, filePath: pfad);
            gezeichnet.Add(Path.GetFullPath(pfad));
        }
        Console.WriteLine($"   Sonde „{string.Join("“, „", Wurzeln.Order(StringComparer.Ordinal))}\" ({Quellen().Count()} Dateien) im Speicher in {ziel.Name} "
                          + $"und im Katalog {SondenKatalog} gelegt.");

        // ── GEZEICHNET = Gesten (Werkbank P7.3): je Geste ein Karten-Delta wie aus dem Browser, geschrieben vom Gesten-Motor in den Fork
        //    (Scaffolder für Neues, Abgleich für Geändertes), danach neu gelesen — die nächste Geste sieht den neuen Code-Stand wie in der Werkbank. ──
        CompositionRoot? cr1 = null;
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
                // Eine neue Datei im Katalog gehört zum Katalog-Projekt, jede andere neue Datei zur Sonde im Ziel.
                var imKatalog = voll.StartsWith(Path.GetFullPath(Path.Combine(solutionDir, SondenKatalog)) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
                fork = doc != null
                    ? patch.Nachher == null ? fork.RemoveDocument(doc.Id) : fork.WithDocumentText(doc.Id, Microsoft.CodeAnalysis.Text.SourceText.From(patch.Nachher))
                    : imKatalog
                        ? fork.AddDocument(DocumentId.CreateNewId(katalog), Path.GetFileName(voll), patch.Nachher ?? "", filePath: voll)
                        : fork.AddDocument(DocumentId.CreateNewId(ziel.Id), Path.GetFileName(voll), patch.Nachher ?? "", folders: new[] { "__sonde" }, filePath: voll);
                if (zeigeIst) Console.WriteLine($"\n── {patch.Datei} ({(patch.Vorher == null ? "neu" : "geändert")}) ──\n{patch.Nachher}");
            }
            Console.WriteLine($"   Geste „{(string?)geste["titel"]}“: {bericht.Patch.Count} Datei(en) geschrieben"
                              + (bericht.Schreiber.Uebersprungen.Count > 0 ? $", übersprungen: {string.Join("; ", bericht.Schreiber.Uebersprungen)}" : "") + ".");
        }

        // Der Stand nach den Gesten, frisch auf die Solution gelegt (jede Sonden-Datei als neues Dokument): ein Generator, der Symbole zwischenspeichert,
        //   verlöre über die geänderten Dokumente der Gesten-Schleife sonst seine Ausgaben (inkrementell) — der Build übersetzt ja auch frisch.
        var endstand = new List<(string Pfad, string Text, bool ImKatalog, string? Projekt)>();
        foreach (var d in fork.Projects.SelectMany(x => x.Documents).Where(d => d.FilePath != null && (d.Folders.Contains("__sonde") || gezeichnet.Contains(Path.GetFullPath(d.FilePath))
                                                                                                    || d.Project.Id == katalog)))
        {
            endstand.Add((d.FilePath!, (await d.GetTextAsync()).ToString(), d.Project.Id == katalog, d.Project.AssemblyName));
            gezeichnet.Add(Path.GetFullPath(d.FilePath!));
        }
        (fork, katalog) = MitKatalog(solution, solutionDir, ziel.Id);
        // Jede Datei wieder in ihr Projekt (die Sonde im Ziel, was den Katalog nennt bei den Abläufen, der Katalog für sich).
        foreach (var (pfad, text, imKatalog, projekt) in endstand)
            fork = imKatalog
                ? fork.AddDocument(DocumentId.CreateNewId(katalog), Path.GetFileName(pfad), text, filePath: pfad)
                : fork.AddDocument(DocumentId.CreateNewId((fork.Projects.FirstOrDefault(x => x.AssemblyName == projekt) ?? fork.GetProject(ziel.Id)!).Id),
                    Path.GetFileName(pfad), text, folders: new[] { "__sonde" }, filePath: pfad);
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

        // Ein Generator, der an der Sonde scheitert (CS8785), nimmt dem ganzen Projekt seine Generate — das zählt wie ein Kompilierfehler.
        foreach (var (p, c) in lage2.Analyse.Where(a => lage2.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")))
        {
            var generatoren = p.AnalyzerReferences.SelectMany(r => r.GetGenerators(LanguageNames.CSharp)).ToList();
            if (generatoren.Count == 0) continue;
            var roh = c.RemoveSyntaxTrees(c.SyntaxTrees.Where(lage2.Quelltexte.IstGeneriert));
            var lauf = CSharpGeneratorDriver.Create(generatoren, parseOptions: (CSharpParseOptions?)p.ParseOptions).RunGenerators(roh).GetRunResult();
            foreach (var r in lauf.Results.Where(r => r.Exception != null))
            {
                Console.WriteLine($"   ✗ Ein Generator scheitert an der Sonde: [{p.Name}] {r.Generator.GetGeneratorType().Name}: {r.Exception!.Message}");
                fehler++;
            }
        }
        fehler += await BuildRegelnAsync(lage2, gezeichnet);

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
        // Im Katalog: was dort nicht hingehört, weist die Prüfung hinaus („lage Typ -> nicht Katalog“).
        var hinaus = new System.Text.RegularExpressions.Regex(@"^'(?<typ>[^']+)' ist .+ und gehört nicht in einen Katalog \((?<katalog>[^)]+)\)");
        var lageIst = lageBefunde.Select(m => rolle.Match(m)).Where(m => m.Success).Select(m => $"lage {m.Groups["typ"].Value} -> {m.Groups["ziel"].Value}")
            .Concat(lageBefunde.Select(m => hinaus.Match(m)).Where(m => m.Success).Select(m => $"lage {m.Groups["typ"].Value} -> nicht {m.Groups["katalog"].Value}"))
            .ToList();
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
    /// Die Sonde hält auch die Regeln des BUILDS ein (W5: Prüfen = Bauen) — sonst bewiese sie an Code, den der Build ablehnt: die Analyzer der Projekte
    /// (im Domänen-Projekt, dazu das Gesamtbild in der Laufzeit) und die Lesarten der Generatoren der Komposition (Funktionsform: CQRS100, Graphform:
    /// CQRS115 — dieselbe Lesart, die der Generator meldet). Ausgenommen ist die Lage (CQRS098): die Sonde liegt absichtlich falsch, das prüft unten die
    /// Lage-Prüfung. Gezählt wird, was eine Sonden-Datei trifft oder (ohne Quell-Ort, im Gesamtbild) einen Sonden-Typ nennt.
    /// </summary>
    private static async Task<int> BuildRegelnAsync(Projektlage lage, ISet<string> gezeichnet)
    {
        bool InSondeDatei(SyntaxTree? t) => t?.FilePath is { } pfad && (pfad.Contains("__sonde") || gezeichnet.Contains(Path.GetFullPath(pfad)));
        var fehler = 0;
        var gelaufen = 0;
        var typen = new List<INamedTypeSymbol>();
        foreach (var c in lage.DomänenCompilations)
            foreach (var t in c.SyntaxTrees.Where(InSondeDatei))
            {
                var model = c.GetSemanticModel(t);
                typen.AddRange(t.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Select(d => model.GetDeclaredSymbol(d)).OfType<INamedTypeSymbol>());
            }
        var namen = typen.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var (projekt, comp) in lage.Analyse.Where(a => lage.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "") || a.Projekt.Id == lage.Laufzeit.Id))
        {
            // Die Analyzer des Frameworks (die *.SourceGeneration-Projekte), nicht die der Plattform (CA…, IDE…).
            var analyzer = projekt.AnalyzerReferences.Where(r => (r.FullPath ?? r.Display ?? "").Contains(".SourceGeneration", StringComparison.Ordinal))
                .SelectMany(r => r.GetAnalyzers(LanguageNames.CSharp)).ToImmutableArray();
            if (analyzer.IsEmpty) continue;
            gelaufen += analyzer.Length;
            var befunde = await comp.WithAnalyzers(analyzer).GetAnalyzerDiagnosticsAsync();
            foreach (var d in befunde.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CQRS098"))
            {
                var treffer = d.Location.SourceTree != null ? InSondeDatei(d.Location.SourceTree) : namen.Any(n => d.GetMessage().StartsWith($"'{n}'", StringComparison.Ordinal));
                if (!treffer) continue;
                Console.WriteLine($"   ✗ Sonde verletzt eine Build-Regel: [{projekt.Name}] {d.Id} {Path.GetFileName(d.Location.SourceTree?.FilePath)}: {d.GetMessage()}");
                fehler++;
            }
        }
        foreach (var t in typen.Where(t => t.ContainingType == null))
        {
            var form = Infrastructure.SourceGeneration.Funktionsform.IstFunktion(t) ? Infrastructure.SourceGeneration.Funktionsform.Lies(t).Fehler.Select(f => ("CQRS100", f.Text))
                : Infrastructure.SourceGeneration.Graphform.IstGraph(t) ? Infrastructure.SourceGeneration.Graphform.Lies(t).Fehler.Select(f => ("CQRS115", f.Text))
                : Infrastructure.SourceGeneration.Uebersetzungsform.IstUebersetzung(t) ? Infrastructure.SourceGeneration.Uebersetzungsform.Lies(t).Fehler.Select(f => (f.Id, f.Text))
                : [];
            foreach (var (id, text) in form)
            {
                Console.WriteLine($"   ✗ Sonde verletzt eine Build-Regel: {id} '{t.Name}': {text}");
                fehler++;
            }
        }
        if (fehler == 0) Console.WriteLine($"   ✓ Build-Regeln: die Sonde hält {gelaufen} Analyzer und die Lesarten der Generatoren ein (ohne die Lage, CQRS098).");
        return fehler;
    }

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
        // Direkte Abfrage (docs/03 §3.2): die Fragen im Decider — Query und Antworten aus der Signatur.
        foreach (var a in A(b, "aggregate").Where(Sonde))
            foreach (var f in A(a, "fragen"))
                z.Add($"frage {S(a, "name")}.{S(f, "query")} -> {string.Join("|", A(f, "antworten").Select(x => (string?)x))}");
        string Konstruktor(JsonNode? n) => "(" + string.Join(", ", A(n, "konstruktor").Select(p => $"{S(p, "name")}:{S(p, "typ")}")) + ")";
        // Funktionen (Entscheidung 4): eine Klasse mit typisierten Ports — Konstruktor (Technik, Konfig, Zustand), je Auf seine eine Nachricht
        //   (GR-EINGANG), Herkunft, Lese-Fähigkeiten und die Ausgaben (Strom); eine Quelle lauscht (Lausche).
        bool Gerufen(JsonNode f) => A(f, "methoden").Any() && !A(f, "eingaenge").Any() && !A(f, "lauscht").Any();
        string Ausgaben(JsonNode e, string feld = "ausgaben") => A(e, feld).Any() ? string.Join("; ", A(e, feld).Select(x => (string?)x)) : "nichts";
        foreach (var f in A(b, "bausteinFunktionen").Where(Sonde).Where(f => !Gerufen(f)))
            z.Add($"funktion {S(f, "namespace")}.{S(f, "name")}{(f["klasse"]?.GetValue<bool>() == true ? Konstruktor(f) : " schnittstelle")}"
                + (f["zustand"] is { } zu ? $" zustand={zu}" : "")
                + " | "
                + string.Join(" || ", A(f, "eingaenge").Select(e => $"Auf({S(e, "eingang")})"
                    + (e["herkunft"]?.GetValue<bool>() == true ? " herkunft" : "")
                    + (A(e, "faehigkeiten").Any() ? " liest " + string.Join(", ", A(e, "faehigkeiten").Select(p => $"{S(p, "typ")} {S(p, "name")}")) : "")
                    + " -> " + (e["strom"]?.GetValue<bool>() == true ? "strom " : "") + Ausgaben(e))
                    .Concat(A(f, "lauscht").Any() ? [$"Lausche -> strom {Ausgaben(f, "lauscht")}"] : Array.Empty<string>())));
        // Technik (docs/06 §6.5, ohne Marker: was der Konstruktor einer Funktion nimmt): Klasse oder Schnittstelle, Signatur (Methoden) und Umsetzungen.
        foreach (var f in A(b, "bausteinFunktionen").Where(Sonde).Where(Gerufen))
            z.Add($"technik {S(f, "namespace")}.{S(f, "name")} {(f["klasse"]?.GetValue<bool>() == true ? "klasse" + Konstruktor(f) : "schnittstelle")} | "
                  + string.Join("; ", A(f, "methoden").Select(m => $"{S(m, "name")}({string.Join(", ", A(m, "parameter").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}):{S(m, "rueckgabe")}"))
                  + (A(f, "umsetzungen").Any() ? " | " + string.Join("; ", A(f, "umsetzungen").Select(u => $"umsetzung {S(u, "name")}{Konstruktor(u)}")) : ""));

        // Leseseite: Store-Fn-Ids → „Store.Fn" auflösen.
        var fnName = new Dictionary<string, string>();
        foreach (var st in A(b, "stores"))
            foreach (var f in A(st, "writeFns").Concat(A(st, "readFns"))) fnName[S(f, "_id")] = $"{S(st, "name")}.{S(f, "name")}";
        string Fns(JsonNode h) => string.Join(", ", A(h, "fns").Select(x => fnName.GetValueOrDefault((string?)x ?? "", "?")));

        foreach (var rm in A(b, "readModels").Where(Sonde))
            z.Add($"readmodel {S(rm, "namespace")}.{S(rm, "name")} store={S(rm, "store")}"
                  + (rm["geteilt"]?.GetValue<bool>() == true ? " geteilt" : "") + $" | {Felder(rm)}");
        // Store: seine Klasse (1/2/3, R14) aus der Basisliste der Implementierung — „-“: keine Implementierung bzw. keine Klasse.
        string Klassen(JsonNode st) => A(st, "klassen").Any() ? string.Join(",", A(st, "klassen").Select(k => k.ToString())) : "-";
        foreach (var st in A(b, "stores").Where(Sonde))
            z.Add($"store {S(st, "namespace")}.{S(st, "name")} klasse={Klassen(st)} | schreibt {string.Join(", ", A(st, "writeFns").Select(f => $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))})"))}"
                  + $" | liest {string.Join(", ", A(st, "readFns").Select(f => $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}):{S(f, "rueckgabe")}"))}");
        foreach (var st in A(b, "stores").Where(Sonde))
            foreach (var (f, art) in A(st, "writeFns").Select(f => (f, "schreibt")).Concat(A(st, "readFns").Select(f => (f, "liest"))))
                z.Add($"faehigkeit {(string?)f["sig"]?["namespace"] ?? S(st, "namespace")}.{S(f, "faehigkeit")} {art} {S(st, "name")}.{S(f, "name")}");
        // Projektion: Kennung = Typname (kein Feld); [MindestensEinmal]; je Auf der Eingang, ob er die Herkunft nimmt, und die Fähigkeiten.
        foreach (var p in A(b, "projektionen").Where(Sonde))
            z.Add($"projektion {S(p, "namespace")}.{S(p, "name")}{(p["mindestensEinmal"]?.GetValue<bool>() == true ? " mindestenseinmal" : "")} | "
                  + string.Join("; ", A(p, "handles").Select(h => $"{S(h, "event")}{(h["sig"]?["herkunftParameter"] != null ? " herkunft" : "")} -> {Fns(h)}")));
        // Bündel (docs/04 §4.3.1): der Schlüssel aus den Events — je Event, woher er kommt (Stream, Feld, global), und die Wahl am Parameter.
        foreach (var p in A(b, "projektionen").Where(Sonde))
            foreach (var h in A(p, "handles").Where(h => h["buendel"] is JsonObject))
            {
                var bu = h["buendel"]!;
                z.Add($"buendel {S(p, "name")}.{S(h, "event")} schluessel={(S(bu, "schluessel") is { Length: > 0 } s ? s : "global")}"
                      + (S(bu, "ueber") is { Length: > 0 } u ? $" ueber={u}" : "")
                      + (S(bu, "fehler") is { Length: > 0 } f ? $" fehler={f}" : "")
                      + $" | {string.Join(", ", A(bu, "glieder").Select(g => $"{S(g, "event")}:{S(g, "art")}{(S(g, "feld") is { Length: > 0 } fe ? "=" + fe : "")}"))}");
            }
        // Reader: die Projektionen dahinter folgen aus den Lese-Fähigkeiten (wer in denselben Store schreibt).
        foreach (var r in A(b, "reader").Where(Sonde))
            z.Add($"reader {S(r, "namespace")}.{S(r, "name")} hinter={string.Join(",", A(r, "projektionen").Select(x => (string?)x))} | "
                  + string.Join("; ", A(r, "handles").Select(h => $"{S(h, "query")} -> {Fns(h)} => {string.Join("|", A(h, "responses").Select(x => (string?)x))}")));
        string Namen(JsonNode n, string k) => A(n, k).Any() ? string.Join(", ", A(n, k).Select(x => (string?)x)) : "-";
        // Akteure (Entscheidung 13): was einer hineingibt (IDarf) und was zu ihm kommt (IHoert: Events und Hinweise) — die Basisliste in Reihenfolge.
        foreach (var ak in A(b, "akteure").Where(Sonde))
            z.Add($"akteur {S(ak, "namespace")}.{S(ak, "name")} | darf {Namen(ak, "darf")} | hoert {Namen(ak, "hoert")}");
        // Clients (docs/konzept-akteure.md §4): ein Record, der nur nennt, wen er verkörpert (was über seine Leitung geht, sagen die Akteur-Zeilen).
        foreach (var c in A(b, "clients").Where(Sonde))
            z.Add($"client {S(c, "namespace")}.{S(c, "name")} | verkoerpert {Namen(c, "verkoerpert")}");
        // Graphen (docs/04 §4.4): die Knoten (Parameter des Records, Typ wie geschrieben, [Kopf], [Von …]); abgeleitet daraus: Köpfe, Ausgänge, Quelle und
        //   die Kanten (aufgefaltete Pfade bei einem Graphen im Graphen).
        string Knoten(JsonNode k) => $"{S(k, "name")}:{S(k, "typ")}{(k["kopf"]?.GetValue<bool>() == true ? " [Kopf]" : "")}"
            + (A(k, "von").Any() ? $" [Von {string.Join("+", A(k, "von").Select(v => (string?)v))}]" : "")
            + (A(k, "verlierbar").Any() ? $" [Verlierbar {string.Join("+", A(k, "verlierbar").Select(v => (string?)v))}]" : "");
        foreach (var g in A(b, "graphen").Where(Sonde))
        {
            z.Add($"graph {S(g, "namespace")}.{S(g, "name")} | knoten {string.Join(", ", A(g, "knoten").Select(Knoten))}"
                  + $" | koepfe {Namen(g, "koepfe")} | aus {Namen(g, "ausgaenge")}"
                  + (g["quelle"] is { } q ? $" | quelle {q}" : ""));
            foreach (var k in A(g, "kanten"))
                z.Add($"kante {S(g, "name")} | {S(k, "von")} -{S(k, "typ")}-> {S(k, "nach")}");
        }
        // Übersetzungen der Domäne (docs/plan-ohne-pipeline.md): je Record seine [Feld] und je Parameter von TAus seine Quelle und Regel — was die ↦-Karte
        //   zeigt.
        foreach (var u in A(b, "uebersetzungen").Where(Sonde))
            z.Add($"uebersetzung {S(u, "namespace")}.{S(u, "name")} | {S(u, "ein")} -> {S(u, "aus")}"
                  + (A(u, "felder").Any() ? $" [Feld {string.Join("+", A(u, "felder").Select(f => S(f, "ziel") + "=" + S(f, "quelle")))}]" : "") + " | "
                  + string.Join(", ", A(u, "ziele").Select(x => $"{S(x, "name")}={(S(x, "quelle") is { Length: > 0 } q ? q : "")}({(S(x, "regel") is { Length: > 0 } r ? r : "offen")})")));
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
                                                       or "reader" or "akteur" && Sonde(k.Ref)))
            z.Add($"sicht {S(k.Ref, "namespace")}.{k.Name} {k.Art} domaene={sicht.Domaene(k)} block={sicht.Block(k)}"
                  + (sicht.Inseln.Contains(k.Id) ? " insel" : ""));

        // Der Besitz (Hierarchie Anwendung → Blöcke → Glieder): je Typ, der kein Block ist, sein Besitzer — aus den Signaturen.
        string Eltern(string id) => id.StartsWith('§') ? id : sicht.KarteVon.TryGetValue(id, out var e) ? $"{e.Art}:{e.Name}" : id;
        foreach (var k in sicht.Karten.Where(k => k.Art is "command" or "event" or "rejection" or "valueobject" or "query" or "queryresponse" or "konfig"
                                                       or "enum" or "readmodel" or "projektion" or "reader" or "code" && Sonde(k.Ref)))
            z.Add($"besitz {S(k.Ref, "namespace")}.{k.Name} {k.Art} -> {Eltern(sicht.Besitzer(k))}{(sicht.Mehrdeutig(k) ? " mehrdeutig" : "")}");

        // Code-Karten (Werkbank P7.4): Hilfscode ohne Baustein-Rolle, roh — mit der Zahl seiner Deklarationen (partial: _ort + _teile).
        foreach (var k in sicht.Karten.Where(k => k.Art == "code" && Sonde(k.Ref)))
            z.Add($"code {S(k.Ref, "namespace")}.{k.Name} {S(k.Ref, "art")} teile={1 + A(k.Ref, "_teile").Count()} domaene={sicht.Domaene(k)} block={sicht.Block(k)}");

        return z.Select(x => x.TrimEnd()).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>Die Quelltexte der Sonden-Domäne (Dateiname, Text) — auch die Fixture der Gesten-Tests im Prüfstand; ohne den Katalog und ohne das,
    /// was ihn nennt.</summary>
    public static IEnumerable<(string Name, string Text)> Quelltexte() => Quellen()
        .Where(q => !q.Name.StartsWith(KatalogPraefix, StringComparison.Ordinal) && !q.Name.StartsWith(AblaeufePraefix, StringComparison.Ordinal));

    /// <summary>Die Quelltexte der Sonde, die den fremden Katalog nennen (Dateien „Ablaeufe.&lt;Name&gt;.cs.txt“) — sie liegen bei den Abläufen.</summary>
    public static IEnumerable<(string Name, string Text)> Ablaeufetexte() => Quellen().Where(q => q.Name.StartsWith(AblaeufePraefix, StringComparison.Ordinal));

    private const string AblaeufePraefix = "Ablaeufe.";

    /// <summary>Die Quelltexte des fremden Katalogs der Sonde (Dateien „Katalog.&lt;Bereich&gt;.&lt;Datei&gt;.cs.txt“).</summary>
    public static IEnumerable<(string Name, string Text)> Katalogtexte() => Quellen().Where(q => q.Name.StartsWith(KatalogPraefix, StringComparison.Ordinal));

    private const string KatalogPraefix = "Katalog.";

    /// <summary>Das Projekt des fremden Katalogs der Sonde (docs/plan-katalog-und-graphen.md): nur Abstractions, genutzt von den Abläufen der Anwendung.</summary>
    public const string SondenKatalog = "Sondenkatalog.Katalog";

    /// <summary>„Katalog.Peilung.Commands.cs“ → Bereich „Peilung“, Datei „Commands.cs“.</summary>
    private static (string Bereich, string Datei) KatalogDatei(string name)
    {
        var rest = name[KatalogPraefix.Length..];
        var punkt = rest.IndexOf('.');
        return (rest[..punkt], rest[(punkt + 1)..]);
    }

    /// <summary>
    /// Der Fork mit dem Katalog-Projekt der Sonde: Optionen, Referenzen und Analyzer wie der Vertrag bzw. die Abläufe, eine Projekt-Referenz nur auf
    /// Abstractions — und die Abläufe der Anwendung nutzen ihn (wie nach der Geste „neuer Katalog“), so liegt er in der Hülle der Laufzeit.
    /// </summary>
    private static (Solution, ProjectId) MitKatalog(Solution solution, string solutionDir, ProjectId domaene)
    {
        var vertrag = solution.Projects.First(p => p.AssemblyName == "Abstractions");
        var ablaeufe = solution.Projects.First(p => p.AssemblyName == Abstractions.Projektstruktur.Projekt(Abstractions.Projektstruktur.StandardWurzel,
            Abstractions.Projektstruktur.Rolle.Ablaeufe));
        var id = ProjectId.CreateNewId(SondenKatalog);
        var info = ProjectInfo.Create(id, VersionStamp.Create(), SondenKatalog, SondenKatalog, LanguageNames.CSharp,
                filePath: Path.Combine(solutionDir, SondenKatalog, SondenKatalog + ".csproj"),
                compilationOptions: ablaeufe.CompilationOptions, parseOptions: ablaeufe.ParseOptions,
                projectReferences: new[] { new ProjectReference(vertrag.Id) }, metadataReferences: ablaeufe.MetadataReferences,
                analyzerReferences: ablaeufe.AnalyzerReferences)
            .WithDefaultNamespace(SondenKatalog);
        var fork = solution.AddProject(info).AddProjectReference(ablaeufe.Id, new ProjectReference(id));
        // Die Domäne der Sonde nutzt ihn auch: ihre Übersetzungen nehmen seine Nachrichten (docs/plan-ohne-pipeline.md).
        if (!fork.GetProject(domaene)!.ProjectReferences.Any(r => r.ProjectId == id)) fork = fork.AddProjectReference(domaene, new ProjectReference(id));
        // MSBuild reicht die Referenz transitiv weiter (Komposition, Akteure, Leitung sehen den Katalog über die Abläufe bzw. die Domäne), ein
        //   Roslyn-Fork nicht.
        foreach (var p in solution.Projects.Where(p => p.ProjectReferences.Any(r => r.ProjectId == ablaeufe.Id || r.ProjectId == domaene)))
            if (!fork.GetProject(p.Id)!.ProjectReferences.Any(r => r.ProjectId == id)) fork = fork.AddProjectReference(p.Id, new ProjectReference(id));
        // Globale usings (ImplicitUsings) wie in den Abläufen — Task & Co. ohne using.
        foreach (var d in ablaeufe.Documents.Where(d => d.FilePath?.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal) == true))
            fork = fork.AddDocument(DocumentId.CreateNewId(id), Path.GetFileName(d.FilePath!), d.GetTextAsync().Result,
                filePath: Path.Combine(solutionDir, SondenKatalog, "obj", Path.GetFileName(d.FilePath!)));
        return (fork, id);
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
