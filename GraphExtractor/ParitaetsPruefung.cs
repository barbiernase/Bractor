using System.Text.Json;
using System.Text.Json.Nodes;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>Ein Befund der Paritäts-Prüfung: Bereich (Inventar/Kompilat/Fixpunkt), Schweregrad, Text.</summary>
public sealed record ParitaetsBefund(string Bereich, string Schweregrad, string Meldung);

/// <summary>
/// Die Paritäts-Prüfung (<c>dotnet run --project GraphExtractor -- --check</c>): beweist, dass Code, Extraktion
/// und Editor-Modell dasselbe sagen — statt es zu hoffen.
///
/// <b>A · Inventar:</b> eine UNABHÄNGIGE, rein syntaktische Zählung der Domänen-Konstrukte (Marker-Interfaces,
/// Decide-/Apply-Methoden, Sende-Verben) gegen das, was im Board-Modell landet. Fängt blinde Flecken des
/// Extractors (Konstrukt im Code, im Editor unsichtbar).
///
/// <b>B · Fixpunkt:</b> M₁ = extract(Code). Die von M₁ abgedeckten Typen im Domain-Projekt werden durch
/// <c>Scaffolder.Generiere(M₁)</c> ERSETZT (Solution-Fork im Speicher, nichts auf Platte), alles neu
/// kompiliert (echte Generatoren), M₂ = extract(Fork). Gefordert: 0 Compile-Fehler UND M₁ == M₂. Damit ist
/// „der Editor kann den Code verlustfrei zurückschreiben" eine geprüfte Eigenschaft.
/// </summary>
public static class ParitaetsPruefung
{
    public sealed record Analyse(RoutingTruth Routing, DomainModel Dom, KnowledgeGraph Graph, List<Compilation> Compilations);

    public static async Task<List<ParitaetsBefund>> PruefeAsync(
        Solution solution, Projektlage lage, Analyse ist, string boardJson, IReadOnlyList<IngressBindung>? ingress = null)
    {
        var befunde = new List<ParitaetsBefund>();
        Inventar(ist, lage, boardJson, befunde);
        BoardModell(ist, boardJson, befunde);
        GrammatikUndModule(solution, ist, boardJson, ingress, befunde);
        await Fixpunkt(solution, lage, ist, befunde);
        return befunde;
    }

    // ══ Grammatik + Module ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// (1) Jede Regel der <see cref="DomainEditor.Grammatik"/>, die ein Build-Gegenstück mit Kennung nennt (Analyzer-/Generator-ID),
    /// muss diese ID als Diagnose-Literal im Code haben — sonst verspricht der Editor eine Prüfung, die es nicht gibt.
    /// (2) Die Modul-Schnittstellen aus dem Board = die aus dem Code (dieselbe Ableitung, zwei Quellen).
    /// (3) Die Grammatik auf dem Code-Modell: was der Code heute verletzt (Bericht; Fehler nur, wo die Regel ein Build-Gegenstück hat).
    /// </summary>
    private static void GrammatikUndModule(Solution solution, Analyse ist, string boardJson, IReadOnlyList<IngressBindung>? ingress, List<ParitaetsBefund> befunde)
    {
        var literale = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in solution.Projects.SelectMany(p => p.Documents).Where(d => d.FilePath != null && !d.FilePath.Contains("/obj/") && File.Exists(d.FilePath)))   // Sonde: Dokumente nur im Speicher
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(d.FilePath!), "\"(CQRS\\d{3})\""))
                literale.Add(m.Groups[1].Value);
        var genannt = Grammatik.Regeln.SelectMany(r => r.Build.Where(g => g.Kennung != null).Select(g => (r.Id, g.Kennung!))).ToList();
        foreach (var (regel, id) in genannt.Where(x => !literale.Contains(x.Item2)))
            befunde.Add(new("Grammatik", "error", $"{regel}: Build-Gegenstück {id} gibt es im Code nicht."));
        var offen = Grammatik.Regeln.Where(r => r.Build.All(g => g.Art == "offen")).Select(r => r.Id).ToList();
        befunde.Add(new("Grammatik", "info",
            $"{Grammatik.Regeln.Count} Regeln ({Grammatik.Konsume.Count} Konsum, {Grammatik.Erzeugungen.Count} Erzeugung); " +
            $"{genannt.Select(x => x.Item2).Distinct().Count()} Build-IDs im Code gefunden ({string.Join(", ", genannt.Select(x => x.Item2).Distinct().OrderBy(x => x))}); " +
            $"ohne Build-Gegenstück (offen): {string.Join(", ", offen)}."));

        // Die Ingress-Bindungen stehen in der Composition Root (Code-Fakt, eigene Extraktion) — nicht im Domänen-Modell.
        var ausCode = ModellMapper.ZuEditorModell(ist.Graph, ist.Dom) with { Ingress = ingress };
        var ausBoard = BoardLeseseite.AusBoard(boardJson);
        var mc = Module.Ableiten(ausCode);
        var zc = Module.AlsZeilen(mc);
        var zb = Module.AlsZeilen(Module.Ableiten(ausBoard));
        foreach (var z in zc.Except(zb)) befunde.Add(new("Module", "error", $"Port nur aus dem Code: {z}"));
        foreach (var z in zb.Except(zc)) befunde.Add(new("Module", "error", $"Port nur aus dem Board: {z}"));
        befunde.Add(new("Module", "info",
            $"{mc.Count} Module (Namespaces), {zc.Count} Ports ({zc.Count(z => z.EndsWith("|offen"))} offen) — Board = Code: {(zc.SequenceEqual(zb) ? "ja" : "NEIN")}."));

        var g = Validator.PruefeGrammatik(ausCode);
        foreach (var b in g.Where(b => b.IstFehler))
            befunde.Add(new("Grammatik", Grammatik.RegelVon(b.Code).Build.Any(x => x.Art != "offen") ? "error" : "warning", b.Meldung));
        befunde.Add(new("Grammatik", "info",
            $"Code-Modell gegen die Grammatik: {g.Count(b => b.IstFehler)} Fehler, {g.Count(b => b.Schweregrad == "warning")} Warnungen, " +
            $"{g.Count(b => b.Schweregrad == "info")} Hinweise ({string.Join(", ", g.GroupBy(b => b.Code).OrderBy(x => x.Key).Select(x => $"{x.Key}×{x.Count()}"))})."));
    }

    /// <summary>Extraktions-Pipeline auf beliebigen Compilations (Original oder Fork).</summary>
    public static Analyse Analysiere(List<Compilation> comps, ISet<string> domänen)
    {
        var routing = RoutingTruth.FromCompilations(comps);
        var dom = new DomainExtractor(comps, domänen).Extract();
        var graph = new GraphBuilder(routing, dom).Build();
        return new Analyse(routing, dom, graph, comps);
    }

    // ══ A · Inventar ═══════════════════════════════════════════════════════════════════════════

    private static void Inventar(Analyse ist, Projektlage lage, string boardJson, List<ParitaetsBefund> befunde)
    {
        var board = JsonNode.Parse(boardJson)!.AsObject();
        var soll = SyntaxInventar.Aus(ist.Compilations, lage.DomänenAssemblies);
        befunde.Add(new("Inventar", "info",
            $"Soll aus dem Code: {soll.Aggregate.Count} Aggregate ({soll.Aggregate.Values.Sum(a => a.Decide.Count)} Decide, " +
            $"{soll.Aggregate.Values.Sum(a => a.Apply.Count)} Apply), {soll.Commands.Count} Commands, {soll.Events.Count} Events, " +
            $"{soll.Ablehnungen.Count} Ablehnungen, {soll.ValueObjects.Count} VOs, {soll.Konfigs.Count} Konfigs, {soll.Stores.Count} Stores, {soll.Enums.Count} Enums, {soll.Sagas.Count} Sagas " +
            $"({soll.Sagas.Values.Sum()} Regeln), {soll.Queries.Count} Queries, {soll.Responses.Count} Responses, {soll.ReadModels.Count} ReadModels, " +
            $"{soll.Subscriber.Count} Projektionen/Reaktionen, {soll.Readers.Count} Reader, {soll.Pipelines.Count} Pipelines, " +
            $"{soll.Akteure.Count} Akteure ({soll.AkteurRechte.Values.Sum(x => x.Count)} Befugnisse)."));

        HashSet<string> BoardRecords(string kind) => (board["records"]?.AsArray() ?? new JsonArray())
            .Where(r => (string?)r?["kind"] == kind)
            .Select(r => $"{r!["namespace"]}.{r["name"]}").ToHashSet(StringComparer.Ordinal);
        HashSet<string> BoardListe(string key) => (board[key]?.AsArray() ?? new JsonArray())
            .Select(r => $"{r!["namespace"]}.{r["name"]}").ToHashSet(StringComparer.Ordinal);

        Vergleiche("Command", soll.Commands, BoardRecords(RecordArt.Command), befunde);
        Vergleiche("Event", soll.Events, BoardRecords(RecordArt.Event), befunde);
        Vergleiche("Ablehnung", soll.Ablehnungen, BoardRecords(RecordArt.Rejection), befunde);
        Vergleiche("Value Object", soll.ValueObjects, BoardRecords(RecordArt.ValueObject), befunde);
        Vergleiche("Konfiguration", soll.Konfigs, BoardRecords("konfig"), befunde);
        Vergleiche("Store", soll.Stores, BoardListe("stores"), befunde);
        Vergleiche("Query", soll.Queries, BoardRecords("query"), befunde);
        Vergleiche("Response", soll.Responses, BoardRecords("queryresponse"), befunde);
        Vergleiche("Enum", soll.Enums, BoardListe("enums"), befunde);
        Vergleiche("Aggregat", soll.Aggregate.Keys.ToHashSet(StringComparer.Ordinal), BoardListe("aggregate"), befunde);
        Vergleiche("Saga", soll.Sagas.Keys.ToHashSet(StringComparer.Ordinal), BoardListe("sagas"), befunde);
        Vergleiche("ReadModel", soll.ReadModels, BoardListe("readModels"), befunde);
        Vergleiche("Reader", soll.Readers, BoardListe("reader"), befunde);
        Vergleiche("Pipeline", soll.Pipelines, BoardListe("pipelines"), befunde);
        Vergleiche("Akteur", soll.Akteure, BoardListe("akteure"), befunde);
        // Je Akteur seine Befugnisse: Soll = IDarf<T> der Basisliste, Ist = darf[] der Board-Karte.
        foreach (var a in board["akteure"]?.AsArray() ?? new JsonArray())
        {
            var full = $"{a!["namespace"]}.{a["name"]}";
            if (!soll.AkteurRechte.TryGetValue(full, out var sollDarf)) continue;
            var istDarf = (a["darf"]?.AsArray() ?? new JsonArray()).Select(x => $"{full}|{x}").ToHashSet(StringComparer.Ordinal);
            Vergleiche($"Befugnis ({a["name"]})", sollDarf.Select(x => $"{full}|{x}").ToHashSet(StringComparer.Ordinal), istDarf, befunde);
        }
        Vergleiche("Projektion/Reaktion", soll.Subscriber,
            BoardListe("projektionen").Concat(BoardListe("reaktionen")).ToHashSet(StringComparer.Ordinal), befunde);

        // Decide-/Apply-Methoden je Aggregat: Soll = Methoden im Quelltext, Ist = Decider-/Applier-Knoten im Board.
        var decider = (board["decider"]?.AsArray() ?? new JsonArray()).Select(d => $"{d!["aggregat"]}|{d["command"]}").ToHashSet(StringComparer.Ordinal);
        var applier = (board["applier"]?.AsArray() ?? new JsonArray()).Select(a => $"{a!["aggregat"]}|{a["event"]}").ToHashSet(StringComparer.Ordinal);
        foreach (var (agg, info) in soll.Aggregate)
        {
            var name = agg[(agg.LastIndexOf('.') + 1)..];
            Vergleiche($"Decide ({name})", info.Decide.Select(c => $"{name}|{c}").ToHashSet(StringComparer.Ordinal),
                decider.Where(x => x.StartsWith(name + "|", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal), befunde);
            Vergleiche($"Apply ({name})", info.Apply.Select(e => $"{name}|{e}").ToHashSet(StringComparer.Ordinal),
                applier.Where(x => x.StartsWith(name + "|", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal), befunde);
        }

        // Saga-Regeln: Soll = Anzahl Sende/SendeJe-Verben im Regeln-Ausdruck, Ist = Schritte im Board.
        foreach (var (saga, anzahl) in soll.Sagas)
        {
            var node = (board["sagas"]?.AsArray() ?? new JsonArray())
                .FirstOrDefault(s => $"{s!["namespace"]}.{s["name"]}" == saga);
            var ist2 = node?["schritte"]?.AsArray().Count ?? 0;
            if (ist2 != anzahl)
                befunde.Add(new("Inventar", "error", $"Saga {saga}: {anzahl} Regel(n) im Code, {ist2} im Editor-Modell."));
        }

        // Namenskollisionen: das Board referenziert Records per EINFACHEM Namen.
        foreach (var dup in soll.AlleRecordNamen.GroupBy(x => x[(x.LastIndexOf('.') + 1)..]).Where(g => g.Count() > 1))
            befunde.Add(new("Inventar", "warning",
                $"Namenskollision '{dup.Key}' in {string.Join(", ", dup)} — das Editor-Modell referenziert per einfachem Namen (mehrdeutig)."));
    }

    private static void Vergleiche(string art, HashSet<string> soll, HashSet<string> ist, List<ParitaetsBefund> befunde)
    {
        foreach (var fehlt in soll.Except(ist).OrderBy(x => x, StringComparer.Ordinal))
            befunde.Add(new("Inventar", "error", $"{art} '{fehlt}' steht im Code, fehlt im Editor-Modell."));
        foreach (var extra in ist.Except(soll).OrderBy(x => x, StringComparer.Ordinal))
            befunde.Add(new("Inventar", "error", $"{art} '{extra}' steht im Editor-Modell, aber nicht (so) im Code."));
    }

    /// <summary>Die unabhängige Soll-Zählung: rein über Syntax + Marker, ohne den DomainExtractor.</summary>
    private sealed class SyntaxInventar
    {
        public HashSet<string> Commands = new(), Events = new(), Ablehnungen = new(), ValueObjects = new(),
            Queries = new(), Responses = new(), Enums = new(), ReadModels = new(), Readers = new(),
            Pipelines = new(), Subscriber = new(), Konfigs = new(), Stores = new(), Akteure = new();
        /// <summary>Akteur → seine IDarf-Ziele (einfache Namen) — aus der Basisliste, unabhängig vom DomainExtractor.</summary>
        public Dictionary<string, HashSet<string>> AkteurRechte = new(StringComparer.Ordinal);
        public List<string> AlleRecordNamen = new();
        public Dictionary<string, (HashSet<string> Decide, HashSet<string> Apply)> Aggregate = new();
        public Dictionary<string, int> Sagas = new();

        public static SyntaxInventar Aus(List<Compilation> comps, ISet<string> domänen)
        {
            var inv = new SyntaxInventar();
            INamedTypeSymbol? Get(string n) => comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
            var iCmd = Get(Vertrag.ICommand); var iEvt = Get(Vertrag.IEvent); var iTr = Get(Vertrag.ITransientEvent);
            var iQ = Get(Vertrag.IQuery); var iQr = Get(Vertrag.IQueryResponse); var iRm = Get(Vertrag.IReadModel);
            var iState = Get(Vertrag.IState); var iProz = Get(Vertrag.IProzessDefinition);
            var iSub = Get(Vertrag.ISubscriber); var iPipe = Get(Vertrag.IPipelineHandler);
            var iPayload = Get(Vertrag.IMessagePayload); var iOut = Get(Vertrag.IPipelineOutput);
            var iSelf = Get(Vertrag.IPipelineSelfMessage); var iTrig = Get(Vertrag.IPipelineTrigger);
            var iReader = Get(Vertrag.IReader); var iDecider = Get(Vertrag.IDecider); var iApplier = Get(Vertrag.IApplier);
            var iWStore = Get(Vertrag.IWriteStore); var iRStore = Get(Vertrag.IReadStore); var iStore = Get(Vertrag.IStore);
            var iWert = Get(Vertrag.IWertobjekt); var iEnv = Get(Vertrag.IAggregateEnvelope);
            var iAkteur = Get(Vertrag.IAkteur); var iDarf = Get(Vertrag.IDarf);
            bool Innen(INamedTypeSymbol t, INamedTypeSymbol? g) => g != null && t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == g.ToDisplayString());
            bool Domäne(IAssemblySymbol? a) => a != null && domänen.Contains(a.Name);

            // Fähigkeiten, die ein Bündel (IStore) erbt — die sind KEIN eigener Store.
            var imBuendel = new HashSet<string>(StringComparer.Ordinal);
            foreach (var comp in comps.Where(c => domänen.Contains(c.AssemblyName ?? "")))
                foreach (var tree in comp.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)))
                    foreach (var decl in tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                        if (comp.GetSemanticModel(tree).GetDeclaredSymbol(decl) is INamedTypeSymbol b && Sym.Implements(b, iStore))
                            foreach (var i in b.AllInterfaces) imBuendel.Add(i.Fq());

            foreach (var comp in comps.Where(c => domänen.Contains(c.AssemblyName ?? "")))
                foreach (var tree in comp.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)))
                {
                    var model = comp.GetSemanticModel(tree);
                    foreach (var decl in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                    {
                        if (model.GetDeclaredSymbol(decl) is not INamedTypeSymbol t) continue;
                        var full = t.Fq();
                        if (decl is EnumDeclarationSyntax) { inv.Enums.Add(full); continue; }
                        if (decl is InterfaceDeclarationSyntax)
                        {
                            // Akteur-Dienst: ein Vertrag mit IAkteur (z. B. die KI).
                            if (iAkteur != null && full != iAkteur.Fq() && Sym.Implements(t, iAkteur))
                            {
                                inv.Akteure.Add(full);
                                inv.AkteurRechte[full] = t.AllInterfaces.Where(i => iDarf != null && i.OriginalDefinition.Fq() == iDarf.Fq())
                                    .Select(i => i.TypeArguments[0].Name).ToHashSet(StringComparer.Ordinal);
                                continue;
                            }
                            // Store = jedes Bündel (IStore), plus jede Fähigkeit OHNE Bündel (dann ihr eigener Store).
                            if (Sym.Implements(t, iStore)
                                || (Sym.Implements(t, iWStore) || Sym.Implements(t, iRStore))
                                   && !imBuendel.Contains(full))
                                inv.Stores.Add(full);
                            continue;
                        }
                        var istRecord = decl is RecordDeclarationSyntax;
                        if (istRecord && t.ContainingType == null) inv.AlleRecordNamen.Add(full);

                        if (iAkteur != null && Sym.Implements(t, iAkteur) && !t.AllInterfaces.Any(i => i.Fq() != iAkteur.Fq() && Sym.Implements(i, iAkteur)))
                        {
                            inv.Akteure.Add(full);
                            inv.AkteurRechte[full] = t.AllInterfaces.Where(i => iDarf != null && i.OriginalDefinition.Fq() == iDarf.Fq())
                                .Select(i => i.TypeArguments[0].Name).ToHashSet(StringComparer.Ordinal);
                        }
                        else if (Sym.Implements(t, iCmd)) inv.Commands.Add(full);
                        else if (Sym.Implements(t, iTr)) inv.Ablehnungen.Add(full);
                        else if (Sym.Implements(t, iEvt)) inv.Events.Add(full);
                        else if (Sym.Implements(t, iQ)) inv.Queries.Add(full);
                        else if (Sym.Implements(t, iQr)) inv.Responses.Add(full);
                        else if (Sym.Implements(t, iRm)) inv.ReadModels.Add(full);
                        else if ((istRecord || Sym.Implements(t, iWert)) && t.ContainingType == null && !Sym.Implements(t, iPayload) && !Sym.Implements(t, iOut)
                                 && !Sym.Implements(t, iSelf) && !Sym.Implements(t, iTrig) && !Sym.Implements(t, iState)
                                 && !Sym.Implements(t, iWStore) && !Sym.Implements(t, iRStore))
                            inv.ValueObjects.Add(full);

                        if (t.AllInterfaces.Any(i => iReader != null && i.OriginalDefinition.Fq() == iReader.Fq())) inv.Readers.Add(full);
                        if (Sym.Implements(t, iPipe)) inv.Pipelines.Add(full);
                        // Konsument (Pipeline/Subscriber/Reader/Store-Impl): Records im öffentlichen Ctor = Konfiguration.
                        var istStoreImpl = t.TypeKind != TypeKind.Interface && (Sym.Implements(t, iWStore) || Sym.Implements(t, iRStore));
                        if (Sym.Implements(t, iPipe) || Sym.Implements(t, iSub) || istStoreImpl
                            || t.AllInterfaces.Any(i => iReader != null && i.OriginalDefinition.Fq() == iReader.Fq()))
                            foreach (var c in t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
                                foreach (var prm in c.Parameters)
                                    if (prm.Type is INamedTypeSymbol { IsRecord: true } pr && Domäne(pr.ContainingAssembly)
                                        && !Sym.Implements(pr, iPayload) && !Sym.Implements(pr, iOut) && !Sym.Implements(pr, iRm))
                                        inv.Konfigs.Add(pr.Fq());
                        // Konsument = Subscriber mit mindestens einem Handler (Event + Aggregat-Umschlag) — Methodenname egal.
                        if (Sym.Implements(t, iSub) && t.GetMembers().OfType<IMethodSymbol>()
                                .Any(m => m.Parameters.Length > 1 && Sym.Implements(m.Parameters[0].Type, iEvt)
                                          && m.Parameters.Skip(1).Any(p => p.Type.Fq() == iEnv?.Fq())))
                            inv.Subscriber.Add(full);

                        // Aggregat-Komposition über den TYP: Decider/Applier = wer IDecider<S>/IApplier<S> implementiert (wo auch immer).
                        foreach (var i in t.AllInterfaces.Where(i => i.TypeArguments.Length == 1
                                     && (i.OriginalDefinition.Fq() == iDecider?.Fq() || i.OriginalDefinition.Fq() == iApplier?.Fq())))
                        {
                            var stateFull = i.TypeArguments[0].Fq();
                            if (!inv.Aggregate.TryGetValue(stateFull, out var info))
                                inv.Aggregate[stateFull] = info = (new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
                            var istDecider = i.OriginalDefinition.Fq() == iDecider?.Fq();
                            foreach (var md in decl.ChildNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword)))
                            {
                                if (model.GetDeclaredSymbol(md) is not IMethodSymbol ms || ms.Parameters.Length == 0) continue;
                                var p0 = ms.Parameters[0].Type;
                                if (istDecider && Sym.Implements(p0, iCmd)) info.Decide.Add(p0.Name);
                                if (!istDecider && ms.ReturnsVoid && Sym.Implements(p0, iEvt)) info.Apply.Add(p0.Name);
                            }
                        }

                        // Saga: jede Regel endet in genau einem Sende/SendeJe.
                        if (Sym.Implements(t, iProz))
                        {
                            var regeln = decl.DescendantNodes().OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => p.Identifier.Text == Vertrag.Regeln);
                            inv.Sagas[full] = regeln?.DescendantNodes().OfType<GenericNameSyntax>()
                                .Count(g => g.Identifier.Text == Vertrag.Sende || g.Identifier.Text == Vertrag.SendeJe) ?? 0;
                        }
                    }
                }
            inv.ValueObjects.ExceptWith(inv.Konfigs);
            return inv;
        }
    }

    // ══ B · Fixpunkt ═══════════════════════════════════════════════════════════════════════════

    // ══ Board ⇄ Modell ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Board zeigt die Leseseite in eigenen Sammlungen; <see cref="BoardLeseseite.AusBoard"/> liest sie zurück. Beides muss
    /// dasselbe Modell ergeben wie die Extraktion — sonst schriebe „C# schreiben" etwas anderes, als der Code sagt.
    /// </summary>
    private static void BoardModell(Analyse ist, string boardJson, List<ParitaetsBefund> befunde)
    {
        var ausCode = ModellMapper.ZuEditorModell(ist.Graph, ist.Dom);
        var ausBoard = BoardLeseseite.AusBoard(boardJson);
        var vorher = befunde.Count;
        VergleicheLeseseite("Board ⇄ Modell", ausCode, ausBoard, befunde, mitDateien: true);
        // Alle Records (auch ReadModel/Trigger/Selbst/Konfig, die das Board in eigenen Sammlungen zeigt).
        Liste("Board ⇄ Modell", "Record", ausCode.Records, ausBoard.Records, r => $"{r.Kind}|{r.Namespace}.{r.Name}", r => r, befunde);
        // Idempotenz: ein unverändertes Board meldet KEIN Element als „im Editor geändert“ — sonst fasste „C# schreiben“ Code an.
        foreach (var g in Herkunft.Geaenderte(ausBoard))
            befunde.Add(new("Board ⇄ Modell", "error", $"Stempel: '{g}' gilt ohne Änderung als geändert — „C# schreiben“ würde es anfassen."));
        befunde.Add(new("Board ⇄ Modell", "info", befunde.Count == vorher
            ? $"Leseseite aus dem Board = Leseseite aus dem Code ({ausCode.Lesen?.Stores.Count} Stores, {ausCode.Lesen?.Konsumenten.Count} Projektionen/Reaktionen, {ausCode.Lesen?.Reader.Count} Reader, {ausCode.Lesen?.Pipelines.Count} Pipelines)."
            : "Leseseite aus dem Board weicht vom Code ab."));
        PipelineAusgaengeImBoard(ist, boardJson, befunde);
    }

    /// <summary>
    /// Jede Variante einer Pipeline-Signatur (OneOf) steht am Board-Handle als Ausgang — in der Liste ihrer Art (sends, emits,
    /// publishes, schedules, fristen). Sonst zeigt die GUI eine Kante nicht, die die Laufzeit nach Typ routet.
    /// </summary>
    private static void PipelineAusgaengeImBoard(Analyse ist, string boardJson, List<ParitaetsBefund> befunde)
    {
        var board = JsonNode.Parse(boardJson);
        var vorher = befunde.Count;
        var n = 0;
        foreach (var p in ist.Dom.Pipelines)
        {
            var bp = (board?["pipelines"] as JsonArray)?.FirstOrDefault(x => (string?)x?["name"] == p.Name);
            foreach (var (input, v) in p.HandleVertraege)
            {
                var ein = input[(input.LastIndexOf('.') + 1)..];
                var bh = (bp?["handles"] as JsonArray)?.FirstOrDefault(h => ((string?)h?["input"] ?? (string?)h?["event"]) == ein);
                string[] L(string feld) => (bh?[feld] as JsonArray)?.Select(x => x is JsonObject o ? (string?)(o["name"] ?? o["command"]) ?? "" : (string?)x ?? "").ToArray() ?? [];
                bool Frist(string art, string typ) => (bh?["fristen"] as JsonArray)?.Any(f => (string?)f?["command"] == typ && (string?)f?["art"] == art) == true;
                foreach (var a in v.Ausgaenge.Where(a => a.Art != "storefn"))
                {
                    n++;
                    var da = a.Art switch
                    {
                        "command" => L("sends").Contains(a.Typ),
                        "trigger" => L("emits").Contains(a.Typ),
                        "transient" or "event" => L("publishes").Contains(a.Typ),
                        "self" => L("schedules").Contains(a.Typ),
                        "frist" => Frist("frist", a.Typ),
                        "fristStorno" => Frist("storno", a.Typ),
                        _ => true,
                    };
                    if (!da) befunde.Add(new("Board ⇄ Modell", "error",
                        $"Pipeline {p.Name}.Handle({ein}): Ausgang {a.Art}:{a.Typ} steht in der Signatur, aber nicht am Board-Handle."));
                }
            }
        }
        if (befunde.Count == vorher)
            befunde.Add(new("Board ⇄ Modell", "info", $"Pipeline-Ausgänge: alle {n} Signatur-Varianten stehen am Board-Handle."));
    }

    private static async Task Fixpunkt(Solution solution, Projektlage lage, Analyse ist, List<ParitaetsBefund> befunde)
    {
        var m1 = ModellMapper.ZuEditorModell(ist.Graph, ist.Dom);
        // Ersetzt wird, was in den DOMÄNEN-Projekten deklariert ist (Schreibseite in den Aggregat-Projekten, Leseseite dort,
        //   wo sie liegt). Store-IMPLEMENTIERUNGEN bleiben stehen: sie müssen gegen die geschriebenen Fähigkeiten kompilieren.
        var ziele = lage.Analyse.Where(a => lage.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? "")).ToList();
        var lesen = m1.Lesen ?? new Leseseite();
        var teil = new EditorModell
        {
            Records = m1.Records, Enums = m1.Enums,
            Aggregate = m1.Aggregate, Decider = m1.Decider, Applier = m1.Applier, Sagas = m1.Sagas, Rahmen = m1.Rahmen,
            Lesen = lesen with { Stores = lesen.Stores.Select(s => s with { Impl = null }).ToList() },
        };
        var abgedeckt = teil.Records.Select(r => $"{r.Namespace}.{r.Name}")
            .Concat(teil.Enums.Select(e => $"{e.Namespace}.{e.Name}"))
            .Concat(teil.Aggregate.Select(a => $"{a.Namespace}.{a.Name}"))
            .Concat(teil.Sagas.Select(s => $"{s.Namespace}.{s.Name}"))
            .Concat(lesen.Stores.SelectMany(s => s.Fns.Select(f => $"{f.Namespace ?? s.Namespace}.{f.Name}")
                .Concat(s.IstBuendel ? [$"{s.Namespace}.{s.Name}"] : [])))
            .Concat(lesen.Konsumenten.Select(k => $"{k.Namespace}.{k.Name}"))
            .Concat(lesen.Reader.Select(r => $"{r.Namespace}.{r.Name}"))
            .Concat(lesen.Pipelines.Select(p => $"{p.Namespace}.{p.Name}"))
            .ToHashSet(StringComparer.Ordinal);

        // Namespace → Projekt: das Projekt, in dem die ersetzten Typen dieses Namespace deklariert sind (sonst das erste mit dem Namespace).
        var nsProjekt = new Dictionary<string, (Project Projekt, Compilation Comp)>(StringComparer.Ordinal);
        foreach (var (p, c) in ziele)
            foreach (var tree in c.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)))
            {
                var model = c.GetSemanticModel(tree);
                foreach (var d in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                    if (model.GetDeclaredSymbol(d) is INamedTypeSymbol sym && abgedeckt.Contains(sym.Fq()))
                        nsProjekt.TryAdd(sym.ContainingNamespace.Fq(), (p, c));
            }
        foreach (var (p, c) in ziele)
            foreach (var ns in c.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t))
                         .SelectMany(t => t.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()).Select(n => n.Name.ToString()))
                nsProjekt.TryAdd(ns, (p, c));
        teil = teil with
        {
            Records = teil.Records.Where(r => nsProjekt.ContainsKey(r.Namespace)).ToList(),
            Enums = teil.Enums.Where(e => nsProjekt.ContainsKey(e.Namespace)).ToList(),
        };

        // Fork: abgedeckte Typ-Deklarationen aus den Aggregat-Projekten entfernen …
        var fork = solution;
        foreach (var (projekt, comp) in ziele)
            foreach (var docId in projekt.DocumentIds)
            {
                var doc = fork.GetDocument(docId)!;
                var tree = comp.SyntaxTrees.FirstOrDefault(t => t.FilePath == doc.FilePath);
                if (tree == null || Projektlage.IstGeneriert(tree)) continue;
                var root = (CompilationUnitSyntax)(await doc.GetSyntaxRootAsync())!;
                var model = comp.GetSemanticModel(tree);
                var weg = tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                    .Where(d => d.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                    .Where(d => model.GetDeclaredSymbol(d) is INamedTypeSymbol s && abgedeckt.Contains(s.Fq()))
                    .Select(d => d.Span).ToHashSet();
                if (weg.Count == 0) continue;
                var neu = root.RemoveNodes(root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(d => weg.Contains(d.Span)),
                    SyntaxRemoveOptions.KeepNoTrivia)!;
                fork = neu.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Any()
                    ? fork.WithDocumentSyntaxRoot(docId, neu)
                    : fork.RemoveDocument(docId);
            }
        // … und durch den Scaffolder ersetzen (jede Datei in das Projekt ihres Namespace).
        var dateien = Scaffolder.Generiere(teil);
        befunde.Add(new("Fixpunkt", "info",
            $"{abgedeckt.Count} Typen in {ziele.Count} Domänen-Projekt(en) durch {dateien.Count} Scaffolder-Dateien ersetzt " +
            $"(davon Leseseite: {lesen.Stores.Sum(s => s.Fns.Count)} Fähigkeiten, {lesen.Stores.Count(s => s.IstBuendel)} Bündel, " +
            $"{lesen.Konsumenten.Count} Projektionen/Reaktionen, {lesen.Reader.Count} Reader, {lesen.Pipelines.Count} Pipelines), neu kompiliert, neu extrahiert."));
        foreach (var d in dateien.Where(d => !d.Platzierbar))
            befunde.Add(new("Fixpunkt", "error", $"{d.Art} {d.Pfad}: nicht platzierbar (Verzeichnis bzw. Schreiber-Typ aus dem Code unbekannt)."));
        foreach (var datei in dateien.Where(d => d.Platzierbar))
        {
            var ns = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(datei.Inhalt).GetRoot()
                .DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
            var ziel = nsProjekt.TryGetValue(ns, out var z) ? z.Projekt : ziele[0].Projekt;
            fork = fork.AddDocument(DocumentId.CreateNewId(ziel.Id), Path.GetFileName(datei.Pfad), datei.Inhalt,
                folders: new[] { "__scaffold" }, filePath: Path.Combine(Path.GetDirectoryName(ziel.FilePath)!, "__scaffold", datei.Pfad));
        }

        Projektlage.RegistriereDokumente(fork);
        var comps = new List<Compilation>();
        foreach (var (projekt, _) in lage.Analyse)
        {
            var c = await fork.GetProject(projekt.Id)!.GetCompilationAsync();
            if (c == null) continue;
            comps.Add(c);
            // Kompilat: Fehler in den Domänen-Projekten ⇒ das Rückschreiben bricht.
            if (lage.DomänenAssemblies.Contains(c.AssemblyName ?? ""))
                foreach (var d in c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(25))
                    befunde.Add(new("Kompilat", "error", $"[{projekt.Name}] {d.Id} {Path.GetFileName(d.Location.SourceTree?.FilePath)}: {d.GetMessage()}"));
        }

        var zweit = Analysiere(comps, lage.DomänenAssemblies);
        zweit.Dom.Wurzel = ist.Dom.Wurzel;
        foreach (var kv in ist.Dom.ProjektWurzeln) zweit.Dom.ProjektWurzeln[kv.Key] = kv.Value;
        var m2 = ModellMapper.ZuEditorModell(zweit.Graph, zweit.Dom);
        VergleicheModelle(m1, m2, befunde);
    }

    /// <summary>Element-weiser Vergleich M₁ vs. M₂ (usings bewusst ausgenommen — die prüft das Kompilat).</summary>
    private static void VergleicheModelle(EditorModell m1, EditorModell m2, List<ParitaetsBefund> befunde)
    {
        // Dateipfade und usings bewusst ausgenommen (der Fork legt neue Dateien an; usings prüft das Kompilat).
        Liste("Fixpunkt", "Record", m1.Records, m2.Records, r => $"{r.Namespace}.{r.Name}", r => r with { Usings = [], Datei = null }, befunde);
        Liste("Fixpunkt", "Enum", m1.Enums, m2.Enums, e => $"{e.Namespace}.{e.Name}", e => e with { Datei = null }, befunde);
        Liste("Fixpunkt", "Aggregat", m1.Aggregate, m2.Aggregate, a => $"{a.Namespace}.{a.Name}", a => a with { Usings = [], Datei = null, DeciderDatei = null, ApplierDatei = null }, befunde);
        Liste("Fixpunkt", "Decider", m1.Decider, m2.Decider, d => $"{d.Aggregat}|{d.Command}", d => d with { Datei = null }, befunde);
        Liste("Fixpunkt", "Applier", m1.Applier, m2.Applier, a => $"{a.Aggregat}|{a.Event}", a => a with { Datei = null }, befunde);
        Liste("Fixpunkt", "Saga", m1.Sagas, m2.Sagas, s => $"{s.Namespace}.{s.Name}", s => s with { ExtraUsings = [], Datei = null }, befunde);
        VergleicheLeseseite("Fixpunkt", m1, m2, befunde, mitDateien: false);
    }

    /// <summary>Die Leseseite zweier Modelle element-weise (Stores/Fähigkeiten, Projektionen/Reaktionen, Reader, Pipeline-Fähigkeiten).</summary>
    private static void VergleicheLeseseite(string bereich, EditorModell m1, EditorModell m2, List<ParitaetsBefund> befunde, bool mitDateien)
    {
        var a = m1.Lesen ?? new Leseseite();
        var b = m2.Lesen ?? new Leseseite();
        Handle H(Handle h) => mitDateien ? h : h with { Datei = null };
        Liste(bereich, "Store", a.Stores, b.Stores, s => $"{s.Namespace}.{s.Name}",
            s => mitDateien ? s : s with { Datei = null, Fns = s.Fns.Select(f => f with { Datei = null }).ToList() }, befunde);
        Liste(bereich, "Projektion/Reaktion", a.Konsumenten, b.Konsumenten, k => $"{k.Namespace}.{k.Name}",
            k => (mitDateien ? k : k with { Datei = null, Usings = [] }) with { Handles = k.Handles.Select(H).ToList() }, befunde);
        Liste(bereich, "Reader", a.Reader, b.Reader, r => $"{r.Namespace}.{r.Name}",
            r => (mitDateien ? r : r with { Datei = null, Usings = [] }) with { Handles = r.Handles.Select(H).ToList() }, befunde);
        Liste(bereich, "Pipeline", a.Pipelines, b.Pipelines, p => $"{p.Namespace}.{p.Name}",
            p => (mitDateien ? p : p with { Datei = null, Usings = [] }) with { Handles = p.Handles.Select(H).ToList() }, befunde);
    }

    private static void Liste<T>(string bereich, string art, IEnumerable<T> a, IEnumerable<T> b, Func<T, string> key, Func<T, T> normal, List<ParitaetsBefund> befunde)
    {
        var da = a.GroupBy(key).ToDictionary(g => g.Key, g => Json(normal(g.First())), StringComparer.Ordinal);
        var db = b.GroupBy(key).ToDictionary(g => g.Key, g => Json(normal(g.First())), StringComparer.Ordinal);
        foreach (var k in da.Keys.Union(db.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!db.ContainsKey(k)) befunde.Add(new(bereich, "error", $"{art} '{k}': auf der zweiten Seite verschwunden."));
            else if (!da.ContainsKey(k)) befunde.Add(new(bereich, "error", $"{art} '{k}': auf der zweiten Seite neu aufgetaucht."));
            else if (da[k] != db[k]) befunde.Add(new(bereich, "error", $"{art} '{k}' weicht ab:\n{Diff(da[k], db[k])}"));
        }
    }

    private static string Json<T>(T x) => JsonSerializer.Serialize(x, EditorModell.JsonOptionen);

    /// <summary>Kompakter Zeilen-Diff (erste abweichende Zeilen) für lesbare Befunde.</summary>
    private static string Diff(string a, string b)
    {
        var la = a.Split('\n'); var lb = b.Split('\n');
        var aus = new List<string>();
        for (var i = 0; i < Math.Max(la.Length, lb.Length) && aus.Count < 6; i++)
        {
            var x = i < la.Length ? la[i].TrimEnd() : "∅"; var y = i < lb.Length ? lb[i].TrimEnd() : "∅";
            if (x != y) aus.Add($"      Code:   {x.Trim()}\n      Zurück: {y.Trim()}");
        }
        return string.Join("\n", aus);
    }
}
