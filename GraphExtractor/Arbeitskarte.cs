using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Arbeitskarte (docs/konzept-llm-minimalkontext.md)
//
//  Je H-Slot (heute: Decide-/Apply-Rumpf) eine Projektion des GRAPHEN: was der Rumpf-Schreiber wissen muss, wird aus
//  Knoten/Kanten des Wissensgraphen (Routing, OneOf-Ausgänge, Guards, Saga-/Pipeline-/Projektions-Kanten) und aus
//  Roslyn-Symbolen abgeleitet — nach festen, nummerierten Regeln. Kein LLM, keine Heuristik, keine Namens-Ähnlichkeit,
//  keine erfundene Prosa: Beschreibungen erscheinen NUR, wenn der Code einen Kommentar dazu trägt.
//  Der EIGENE Rumpf fließt nie ein (er ist die Antwort) — ausgenommen seine „// 🤖 Prompt:“-Zeile.
//
//  Die Regeln (Abschnitt BEZÜGE):
//    B1  Guard-Wiederverwendung: derselbe Ausgangstyp wird in anderen Slots desselben Aggregats unter Bedingung G erzeugt.
//    B2  Typgleiche Quellen: je Parameter eines Ausgangs (Decide) bzw. je Event-Feld (Apply) die erreichbaren Werte
//        exakt gleichen Typs (cmd.*, this.State.*), bei Apply die setzbaren Ziele gleichen Typs bzw. Sammlungen dieses Typs.
//    B3  Konstruktor-Treffer: ein Zustands-Typ, dessen Konstruktor exakt die Typfolge der Event-Felder nimmt.
//    B4  Zustands-Nutzung: welche Nachbar-Slots welches State-Member lesen / schreiben / aufrufen (Datenfluss).
//    B5  Beispiel: Nachbar derselben Art mit den meisten gemeinsamen Ausgängen, dann der kürzeste.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Vokabular-Eintrag: eine Deklaration, ihr Doku-Kommentar (verbatim, falls vorhanden) und ihre Member.</summary>
public sealed record KartenEintrag(string Rolle, string Deklaration, string? Doku, List<string> Member);

/// <summary>Ein Szenario aus einem bestehenden Test (Gegeben/Wenn/Dann-Kette der Test-DSL).</summary>
public sealed record KartenSzenario(string Name, string Quelle, List<string> Schritte);

public sealed class Arbeitskarte
{
    public required string Art { get; init; }              // "decide" | "apply"
    public required string Aggregat { get; init; }
    public required string Disc { get; init; }             // Command- bzw. Event-Name (identifiziert die Methode)
    public required string Datei { get; init; }
    public required int Zeile { get; init; }
    public required string Rumpf { get; init; }            // "geschrieben" | "leer" (bewusst, Marker) | "fehlt" (throw-Stub)
    public required string Signatur { get; init; }
    public required string Erreichbar { get; init; }
    public List<string> Vertrag { get; } = new();
    public List<string> Kommentare { get; } = new();
    public KartenEintrag? Eingang { get; set; }
    public List<KartenEintrag> Ausgaenge { get; } = new();
    public KartenEintrag? Zustand { get; set; }
    public List<string> Helfer { get; } = new();
    public List<KartenEintrag> Typen { get; } = new();
    public List<string> Umfeld { get; } = new();
    public List<string> Bezuege { get; } = new();
    public (string Disc, string Rumpf)? Beispiel { get; set; }
    public List<KartenSzenario> Szenarien { get; } = new();

    /// <summary>Symbol-Ids (Fq) aller Typen und Member, die die Karte deklariert — die Grundlage der Gegenprobe.</summary>
    public HashSet<string> Symbole { get; } = new(StringComparer.Ordinal);
    /// <summary>Gegenprobe (nicht Teil der Karte): Domänen-Symbole des ECHTEN Rumpfs, die die Karte nicht deklariert. null = kein Rumpf.</summary>
    public List<string>? NichtAufKarte { get; set; }

    public string Id => $"{Aggregat}.{Disc}";

    /// <summary>Token-Schätzung ohne Tokenizer: Zeichen / 3,3 (gegen den Qwen-BPE auf den Bestands-Karten kalibriert).</summary>
    public static int Token(string text) => (int)Math.Ceiling(text.Length / 3.3);

    public string AlsText()
    {
        var b = new StringBuilder();
        void Kopf(string t) { if (b.Length > 0) b.AppendLine(); b.AppendLine("## " + t); }
        static string Doku(string? d) => d == null ? "" : "   /// " + d;
        void Eintrag(string label, KartenEintrag e)
        {
            b.AppendLine($"{label,-10}{e.Deklaration}{(e.Rolle.Length > 0 ? "   [" + e.Rolle + "]" : "")}{Doku(e.Doku)}");
            foreach (var m in e.Member) b.AppendLine($"{"",-12}{m}");
        }

        Kopf("AUFGABE");
        b.AppendLine("Rumpf von: " + Signatur);
        b.AppendLine("Erreichbar: " + Erreichbar);

        if (Vertrag.Count > 0)
        {
            Kopf("VERTRAG");
            foreach (var v in Vertrag) b.AppendLine(v);
        }

        Kopf("KOMMENTARE (verbatim aus dem Code)");
        if (Kommentare.Count == 0) b.AppendLine("keine");
        foreach (var k in Kommentare) b.AppendLine(k);

        Kopf("TYPEN (abschließend; /// = Doku-Kommentar aus dem Code)");
        if (Eingang != null) Eintrag("Eingang", Eingang);
        for (var i = 0; i < Ausgaenge.Count; i++) Eintrag(i == 0 ? "Ausgang" : "", Ausgaenge[i]);
        if (Zustand != null) Eintrag("Zustand", Zustand);
        for (var i = 0; i < Helfer.Count; i++) b.AppendLine($"{(i == 0 ? "Helfer" : ""),-10}{Helfer[i]}");
        for (var i = 0; i < Typen.Count; i++) Eintrag(i == 0 ? "Typen" : "", Typen[i]);

        Kopf("GRAPH-UMFELD");
        foreach (var u in Umfeld) b.AppendLine(u);

        Kopf("BEZÜGE (regelbasiert abgeleitet)");
        if (Bezuege.Count == 0) b.AppendLine("keine");
        foreach (var x in Bezuege) b.AppendLine(x);

        if (Beispiel is { } bsp)
        {
            Kopf("BEISPIEL (B5)");
            b.AppendLine($"// {(Art == "decide" ? "Decide" : "Apply")}({bsp.Disc})");
            b.AppendLine(bsp.Rumpf);
        }

        Kopf("SPEZIFIKATION (Szenarien aus Tests)");
        if (Szenarien.Count == 0) b.AppendLine("keine — für diesen Slot liegt keine Spezifikation als Code vor");
        for (var i = 0; i < Szenarien.Count; i++)
        {
            b.AppendLine($"S{i + 1} {Szenarien[i].Name}   ({Szenarien[i].Quelle})");
            foreach (var s in Szenarien[i].Schritte) b.AppendLine("   " + s);
        }
        return b.ToString();
    }
}

/// <summary>
/// Baut Arbeitskarten aus Wissensgraph + Domänenmodell (Extractor-Ergebnis) und Roslyn-Symbolen. Slots über den Vertrag
/// (<c>IDecider&lt;T&gt;</c>/<c>IApplier&lt;T&gt;</c>, 1. Parameter <c>ICommand</c>/<c>IEvent</c>), Szenarien über die FORM der Test-DSL
/// (generischer Typ über einen State mit einer Methode, die genau ein <c>ICommand</c> nimmt) — nie über Namen.
/// </summary>
public sealed class KartenBauer
{
    /// <summary>Spiegel von <c>SimHost.CodeSync.PromptMarke</c> (SimHost referenziert den Extractor nicht).</summary>
    public const string PromptMarke = "// 🤖 Prompt:";

    private static readonly SymbolDisplayFormat Kurz = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                              | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private sealed record Slot(string Art, INamedTypeSymbol State, INamedTypeSymbol Klasse, IMethodSymbol Methode,
        INamedTypeSymbol Disc, MethodDeclarationSyntax Syntax)
    {
        public string Titel => $"{(Art == "decide" ? "Decide" : "Apply")}({Disc.Name})";
    }

    /// <summary>Ein Zugriff eines Slot-Rumpfs auf ein State-Member: liest | schreibt | ruft .Methode.</summary>
    private sealed record Zugriff(string Member, string Art);

    private readonly List<Compilation> _comps;
    private readonly HashSet<string> _domänen;
    private readonly List<Compilation> _tests;
    private readonly DomainModel _dom;
    private readonly KnowledgeGraph _graph;
    private readonly INamedTypeSymbol? _iCommand, _iEvent, _iTransient, _iState, _iDecider, _iApplier;
    private readonly List<Slot> _slots = new();
    private readonly Dictionary<Slot, List<Zugriff>> _zugriffe = new();

    private KartenBauer(List<Compilation> comps, HashSet<string> domänen, List<Compilation> tests, DomainModel dom, KnowledgeGraph graph)
    {
        _comps = comps; _domänen = domänen; _tests = tests; _dom = dom; _graph = graph;
        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iCommand = Get(Vertrag.ICommand);
        _iEvent = Get(Vertrag.IEvent);
        _iTransient = Get(Vertrag.ITransientEvent);
        _iState = Get(Vertrag.IState);
        _iDecider = Get(Vertrag.IDecider);
        _iApplier = Get(Vertrag.IApplier);
        SammleSlots();
        foreach (var s in _slots) _zugriffe[s] = Zugriffe(s).ToList();
    }

    public static async Task<KartenBauer> ErstelleAsync(Solution solution, Projektlage lage, DomainModel dom, KnowledgeGraph graph)
    {
        // Test-Projekte = Nicht-Analyse-Projekte, die eine Domänen-Assembly referenzieren (dort liegen die Szenarien).
        var analyse = lage.Analyse.Select(a => a.Projekt.Id).ToHashSet();
        var domänenIds = lage.Analyse.Where(a => lage.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? ""))
            .Select(a => a.Projekt.Id).ToHashSet();
        var abh = solution.GetProjectDependencyGraph();
        var tests = new List<Compilation>();
        foreach (var p in solution.Projects.Where(p => !analyse.Contains(p.Id)))
            if (abh.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Any(domänenIds.Contains)
                && await p.GetCompilationAsync() is { } c)
                tests.Add(c);
        return new KartenBauer(lage.Compilations, lage.DomänenAssemblies, tests, dom, graph);
    }

    /// <summary>Alle Karten, deren Disc (oder Aggregat.Disc) passt; ohne Filter alle.</summary>
    public List<Arbeitskarte> Baue(string? filter = null) => _slots
        .Where(s => filter == null || s.Disc.Name == filter || $"{s.State.Name}.{s.Disc.Name}" == filter)
        .Select(Baue).ToList();

    // ── Slots ──

    private void SammleSlots()
    {
        foreach (var comp in _comps.Where(c => _domänen.Contains(c.AssemblyName ?? "")))
            foreach (var t in AlleTypen(comp.Assembly.GlobalNamespace))
                foreach (var i in t.AllInterfaces)
                {
                    var def = i.OriginalDefinition.Fq();
                    var art = def == _iDecider?.Fq() ? "decide" : def == _iApplier?.Fq() ? "apply" : null;
                    if (art == null || i.TypeArguments[0] is not INamedTypeSymbol state) continue;
                    var marker = art == "decide" ? _iCommand : _iEvent;
                    foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (m.MethodKind != MethodKind.Ordinary || m.Parameters.Length == 0) continue;
                        if (m.Parameters[0].Type is not INamedTypeSymbol disc || !Sym.Implements(disc, marker)) continue;
                        var syn = m.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<MethodDeclarationSyntax>()
                            .FirstOrDefault(s => !Projektlage.IstGeneriert(s.SyntaxTree));
                        if (syn != null) _slots.Add(new Slot(art, state, t, m, disc, syn));
                    }
                }
        _slots.Sort((a, b) => string.CompareOrdinal($"{a.State.Name}|{a.Art}|{a.Disc.Name}", $"{b.State.Name}|{b.Art}|{b.Disc.Name}"));
    }

    private IEnumerable<Slot> Nachbarn(Slot s, string? art = null) =>
        _slots.Where(x => x != s && x.State.Fq() == s.State.Fq() && (art == null || x.Art == art));

    // ── Karte ──

    private Arbeitskarte Baue(Slot s)
    {
        var decide = s.Art == "decide";
        var pos = s.Syntax.GetLocation().GetLineSpan();
        var p0 = s.Methode.Parameters[0].Name;
        var k = new Arbeitskarte
        {
            Art = s.Art, Aggregat = s.State.Name, Disc = s.Disc.Name,
            Datei = pos.Path, Zeile = pos.StartLinePosition.Line + 1,
            Rumpf = RumpfStatus(s),
            Signatur = $"{s.Methode.ReturnType.ToDisplayString(Kurz)} {s.Methode.Name}("
                       + string.Join(", ", s.Methode.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}")) + ")",
            Erreichbar = $"{p0} ({s.Disc.Name}), this.State ({s.State.Name})",
        };

        var ausgänge = decide ? Ausgänge(s.Methode) : new List<INamedTypeSymbol>();
        var ablehnungen = ausgänge.Where(o => Sym.Implements(o, _iTransient)).ToList();

        // VERTRAG — nur, was Compiler bzw. Framework-Laufzeit tatsächlich erzwingen.
        if (decide)
        {
            k.Vertrag.Add($"V1 Ausgaben ⊆ {{{string.Join(", ", ausgänge.Select(a => a.Name))}}}   — erzwungen: Compiler (OneOf-Signatur)");
            if (ablehnungen.Count > 0)
                k.Vertrag.Add($"V2 Ablehnung ({string.Join(", ", ablehnungen.Select(a => a.Name))}) nur als einzige Ausgabe   — erzwungen: Laufzeit (Aggregat-Actor wirft bei gemischtem Ergebnis)");
        }

        // KOMMENTARE — verbatim, mit Herkunft.
        if (PromptAus(s.Syntax) is { } prompt) k.Kommentare.Add($"[Prompt im Rumpf] {prompt}");
        if (DokuVon(s.Methode) is { } md) k.Kommentare.Add($"[/// an {s.Titel}] {md}");
        foreach (var c in Kommentare(s.Syntax)) k.Kommentare.Add($"[// vor {s.Titel}] {c}");
        foreach (var c in s.Disc.DeclaringSyntaxReferences.Where(r => !Projektlage.IstGeneriert(r.SyntaxTree)).Take(1).SelectMany(r => Kommentare(r.GetSyntax())))
            k.Kommentare.Add($"[// vor {s.Disc.Name}] {c}");

        // TYPEN
        k.Eingang = Eintrag(s.Disc, decide ? "Command" : "Event", k);
        foreach (var o in ausgänge) k.Ausgaenge.Add(Eintrag(o, Sym.Implements(o, _iTransient) ? "Ablehnung" : "Event", k));
        var member = ZustandsMember(s.State);
        k.Zustand = new KartenEintrag("", s.State.Name, DokuVon(s.State), member.Select(m => MemberZeile(m)).ToList());
        foreach (var m in member) k.Symbole.Add(m.OriginalDefinition.ToDisplayString());
        var helfer = Helfer(s);
        foreach (var h in helfer) { k.Helfer.Add(MemberZeile(h)); k.Symbole.Add(h.OriginalDefinition.ToDisplayString()); }
        var schon = new HashSet<string>(StringComparer.Ordinal) { s.Disc.Fq(), s.State.Fq() };
        foreach (var o in ausgänge) schon.Add(o.Fq());
        var saat = Feldtypen(s.Disc).Concat(ausgänge.SelectMany(Feldtypen))
            .Concat(member.SelectMany(m => Entpacke(MemberTypSymbol(m))))
            .Concat(helfer.SelectMany(m => m.Parameters.SelectMany(p => Entpacke(p.Type))));
        foreach (var t in Transitiv(saat, schon)) k.Typen.Add(Eintrag(t, t.TypeKind == TypeKind.Enum ? "Enum" : "Typ", k));

        // GRAPH-UMFELD
        if (decide) UmfeldDecide(s, ausgänge, k); else UmfeldApply(s, k);

        // BEZÜGE
        if (decide)
        {
            B1(s, ausgänge, k);
            B2Decide(s, ausgänge, member, k);
        }
        else
        {
            B2Apply(s, member, k);
            B3(s, member, k);
        }
        B4(s, member, k);

        k.Beispiel = Beispiel(s, ausgänge);
        k.Szenarien.AddRange(Szenarien(s));
        if (k.Rumpf == "geschrieben") k.NichtAufKarte = Gegenprobe(s, k);
        return k;
    }

    // ── Graph-Umfeld ──

    private Node? Knoten(NodeKind kind, INamedTypeSymbol t) =>
        _graph.Nodes.FirstOrDefault(n => n.Kind == kind && n.FullName == t.Fq())
        ?? _graph.Nodes.FirstOrDefault(n => n.Kind == kind && n.Name == t.Name);

    private void UmfeldDecide(Slot s, List<INamedTypeSymbol> ausgänge, Arbeitskarte k)
    {
        var cmd = Knoten(NodeKind.command, s.Disc);
        var quellen = new List<string>();
        if (cmd != null)
        {
            foreach (var e in _graph.Edges.Where(e => e.To == cmd.Id))
            {
                var von = _graph.Nodes.FirstOrDefault(n => n.Id == e.From);
                if (e.Kind is EdgeKind.sends or EdgeKind.compensates && von?.Process is { } pi && int.TryParse(e.Via, out var ri) && ri < pi.Rules.Count)
                    quellen.Add($"Prozess {von.Name} Regel {ri} ({(e.Kind == EdgeKind.compensates ? "Kompensation" : "wenn " + string.Join(" + ", pi.Rules[ri].When))}{(pi.Rules[ri].FanOut ? ", je Element" : "")})");
                else if (e.Kind == EdgeKind.pipelineEmits && von != null)
                    quellen.Add($"Pipeline {von.Name}");
            }
            if (cmd.Command!.Origin.Contains("client")) quellen.Insert(0, "Client");
        }
        k.Umfeld.Add($"{s.Disc.Name} kommt von: {(quellen.Count == 0 ? "— (nicht im Graphen)" : string.Join(" · ", quellen.Distinct()))}");
        foreach (var o in ausgänge) k.Umfeld.Add($"{o.Name} geht an: {Abnehmer(o, s.State)}");
    }

    private void UmfeldApply(Slot s, Arbeitskarte k)
    {
        var evt = Knoten(NodeKind.@event, s.Disc);
        var erzeuger = new List<string>();
        if (evt != null)
            foreach (var cmd in _graph.Nodes.Where(n => n.Kind == NodeKind.command && n.Command!.Produces.Any(p => p.Event == evt.Name)))
            {
                var guard = cmd.Command!.Produces.First(p => p.Event == evt.Name).Guard;
                erzeuger.Add($"Decide({cmd.Name}){(guard != null ? $" wenn `{guard}`" : " ohne umschließende Bedingung")}");
            }
        k.Umfeld.Add($"{s.Disc.Name} erzeugt von: {(erzeuger.Count == 0 ? "— (kein Decide im Graphen)" : string.Join(" · ", erzeuger))}");
        k.Umfeld.Add($"{s.Disc.Name} geht außerdem an: {Abnehmer(s.Disc, s.State, ohneApply: true)}");
    }

    /// <summary>Wer ein Event konsumiert — aus dem Graphen (Apply, Projektionen, Prozesse, Pipelines).</summary>
    private string Abnehmer(INamedTypeSymbol evt, INamedTypeSymbol state, bool ohneApply = false)
    {
        if (Sym.Implements(evt, _iTransient)) return "Aufrufer (Ablehnung, nicht im Log)";
        var ziele = new List<string>();
        if (!ohneApply)
        {
            var apply = _slots.FirstOrDefault(x => x.Art == "apply" && x.State.Fq() == state.Fq() && x.Disc.Fq() == evt.Fq());
            ziele.Add(apply == null ? $"Apply({evt.Name}) FEHLT" : $"Apply({evt.Name}) [{RumpfStatus(apply)}]");
        }
        var fo = _graph.Views.EventFanout.FirstOrDefault(f => f.Event == evt.Name);
        if (fo != null)
        {
            ziele.AddRange(fo.Projections.Select(p => $"Projektion {p}"));
            ziele.AddRange(fo.TriggersProcesses.Select(p => $"Prozess {p} (Auslöser)"));
            ziele.AddRange(fo.AdvancesProcesses.Select(p => $"Prozess {p} (Bedingung)"));
            ziele.AddRange(fo.Pipelines.Select(p => $"Pipeline {p}"));
        }
        return ziele.Count == 0 ? "—" : string.Join(" · ", ziele);
    }

    // ── Bezüge (Regeln) ──

    /// <summary>B1: unter welchen Guards erzeugen ANDERE Decide-Slots desselben Aggregats denselben Ausgangstyp? (Guards aus dem Graphen.)</summary>
    private void B1(Slot s, List<INamedTypeSymbol> ausgänge, Arbeitskarte k)
    {
        var agg = _dom.Aggregates.FirstOrDefault(a => a.Full == s.State.Fq());
        if (agg == null) return;
        foreach (var o in ausgänge)
        {
            var andere = agg.DecideOutcomes.Where(kv => kv.Key != s.Disc.Fq() && kv.Value.Contains(o.Fq()))
                .Select(kv => (Cmd: kv.Key.Split('.').Last(), Guard: agg.Guards.TryGetValue(kv.Key + "|" + o.Name, out var g) ? g : null))
                .ToList();
            if (andere.Count == 0) continue;
            var teile = andere.GroupBy(a => a.Guard ?? "")
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{(g.Key.Length == 0 ? "ohne umschließende Bedingung" : $"`{g.Key}`")} in {g.Count()} ({string.Join(", ", g.Select(a => a.Cmd))})");
            k.Bezuege.Add($"B1 {o.Name} — in {andere.Count} anderen Decide: {string.Join("; ", teile)}");
        }
    }

    /// <summary>B2 (Decide): je Konstruktor-Parameter eines Ausgangs die erreichbaren Werte exakt gleichen Typs.</summary>
    private void B2Decide(Slot s, List<INamedTypeSymbol> ausgänge, List<ISymbol> member, Arbeitskarte k)
    {
        var p0 = s.Methode.Parameters[0].Name;
        var quellen = Felder(s.Disc).Select(f => (Ausdruck: $"{p0}.{f.Name}", Typ: FeldTyp(f)))
            .Concat(member.OfType<IPropertySymbol>().Select(p => (Ausdruck: $"State.{p.Name}", Typ: (ITypeSymbol?)p.Type)))
            .ToList();
        // Je Typ EINE Zeile: alle Ausgangs-Parameter dieses Typs ← alle erreichbaren Werte dieses Typs.
        var parameter = ausgänge.SelectMany(o => (PrimärKonstruktor(o)?.Parameters ?? ImmutableEmpty).Select(p => (Ziel: $"{o.Name}.{p.Name}", p.Type)));
        foreach (var g in NachTyp(parameter.Select(x => (x.Ziel, x.Type))))
        {
            var treffer = quellen.Where(q => q.Typ != null && Gleich(q.Typ, g.Typ)).Select(q => q.Ausdruck).ToList();
            k.Bezuege.Add($"B2 {g.Typ.ToDisplayString(Kurz)}: {string.Join(", ", g.Namen)} ← {(treffer.Count == 0 ? "kein Wert dieses Typs erreichbar" : string.Join(", ", treffer))}");
        }
    }

    /// <summary>Gruppiert (Name, Typ)-Paare nach exakter Typgleichheit, in Auftretens-Reihenfolge.</summary>
    private static List<(ITypeSymbol Typ, List<string> Namen)> NachTyp(IEnumerable<(string Name, ITypeSymbol Typ)> paare)
    {
        var gruppen = new List<(ITypeSymbol Typ, List<string> Namen)>();
        foreach (var (name, typ) in paare)
        {
            var g = gruppen.FirstOrDefault(x => Gleich(x.Typ, typ));
            if (g.Typ == null) gruppen.Add((typ, new List<string> { name })); else g.Namen.Add(name);
        }
        return gruppen;
    }

    /// <summary>B2 (Apply): je Event-Feld die setzbaren State-Member gleichen Typs bzw. die Sammlungen mit diesem Elementtyp.</summary>
    private void B2Apply(Slot s, List<ISymbol> member, Arbeitskarte k)
    {
        var felder = Felder(s.Disc).Where(f => FeldTyp(f) != null).Select(f => ($"{s.Disc.Name}.{f.Name}", FeldTyp(f)!));
        foreach (var g in NachTyp(felder))
        {
            var ft = g.Typ;
            var fe = ElementTyp(ft);
            var ziele = new List<string>();
            // Generierte Member (Id/Version) gehören dem Framework — kein Schreibziel für Apply.
            foreach (var p in member.OfType<IPropertySymbol>().Where(IstHandgeschrieben))
            {
                if (p.SetMethod != null && Gleich(p.Type, ft)) ziele.Add($"State.{p.Name} (setzbar)");
                var pe = ElementTyp(p.Type);
                if (pe != null && (Gleich(pe, ft) || fe != null && Gleich(pe, fe)))
                    ziele.Add($"State.{p.Name} (Sammlung von {pe.ToDisplayString(Kurz)})");
            }
            k.Bezuege.Add($"B2 {ft.ToDisplayString(Kurz)}: {string.Join(", ", g.Namen)} → {(ziele.Count == 0 ? "kein Ziel gleichen Typs" : string.Join(", ", ziele))}");
        }
    }

    /// <summary>B3 (Apply): ein Zustands-Typ, dessen Konstruktor exakt die Typfolge der Event-Felder nimmt.</summary>
    private void B3(Slot s, List<ISymbol> member, Arbeitskarte k)
    {
        var folge = Felder(s.Disc).Select(FeldTyp).ToList();
        if (folge.Count == 0 || folge.Any(t => t == null)) return;
        foreach (var p in member.OfType<IPropertySymbol>().Where(p => p.SetMethod != null && IstHandgeschrieben(p)))
            if (p.Type is INamedTypeSymbol pt && PrimärKonstruktor(pt) is { } ctor && ctor.Parameters.Length == folge.Count
                && ctor.Parameters.Select((x, i) => Gleich(x.Type, folge[i]!)).All(x => x))
                k.Bezuege.Add($"B3 State.{p.Name} : {pt.Name} — Konstruktor {pt.Name}({string.Join(", ", ctor.Parameters.Select(x => x.Type.ToDisplayString(Kurz)))}) = Feldfolge von {s.Disc.Name}");
    }

    /// <summary>B4: Datenfluss der Nachbar-Slots auf den Zustand (liest / schreibt / ruft .Methode).</summary>
    private void B4(Slot s, List<ISymbol> member, Arbeitskarte k)
    {
        foreach (var m in member)
        {
            var teile = new List<string>();
            foreach (var art in new[] { "liest", "schreibt" })
            {
                var wer = Nachbarn(s).Where(n => _zugriffe[n].Any(z => z.Member == m.Name && z.Art == art)).Select(n => n.Titel).ToList();
                if (wer.Count > 0) teile.Add($"{(art == "liest" ? "gelesen" : "geschrieben")} von {string.Join(", ", wer)}");
            }
            foreach (var g in Nachbarn(s).SelectMany(n => _zugriffe[n].Where(z => z.Member == m.Name && z.Art.StartsWith('.')).Select(z => (z.Art, n.Titel)))
                         .GroupBy(x => x.Art))
                teile.Add($"{g.Key}() von {string.Join(", ", g.Select(x => x.Titel).Distinct())}");
            // Apply: ein handgeschriebenes, veränderbares Member, das KEIN anderer Apply-Slot schreibt oder verändert.
            if (s.Art == "apply" && IstHandgeschrieben(m) && m is IPropertySymbol { } pm && (pm.SetMethod != null || ElementTyp(pm.Type) != null)
                && !Nachbarn(s, "apply").Any(n => _zugriffe[n].Any(z => z.Member == m.Name && z.Art != "liest")))
                teile.Add("von keinem anderen Apply geschrieben oder verändert");
            if (teile.Count > 0) k.Bezuege.Add($"B4 State.{m.Name}: {string.Join("; ", teile)}");
        }
    }

    private static bool IstHandgeschrieben(ISymbol m) => m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree));

    private IEnumerable<Zugriff> Zugriffe(Slot s)
    {
        if (s.Syntax.Body is null && s.Syntax.ExpressionBody is null) yield break;
        var model = Model(s.Syntax.SyntaxTree);
        foreach (var id in s.Syntax.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (model.GetSymbolInfo(id).Symbol is not { } sym || sym.ContainingType?.Fq() != s.State.Fq()) continue;
            if (sym is not (IPropertySymbol or IMethodSymbol or IFieldSymbol)) continue;
            var e = id.Parent is MemberAccessExpressionSyntax ma && ma.Name == id ? (ExpressionSyntax)ma : id;
            if (e.Parent is AssignmentExpressionSyntax a && a.Left == e
                || e.Parent is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression })
                yield return new Zugriff(sym.Name, "schreibt");
            else if (e.Parent is MemberAccessExpressionSyntax aufruf && aufruf.Expression == e && aufruf.Parent is InvocationExpressionSyntax)
                yield return new Zugriff(sym.Name, "." + aufruf.Name.Identifier.Text);
            else
                yield return new Zugriff(sym.Name, "liest");
        }
    }

    // ── Gegenprobe ──

    /// <summary>
    /// Alle im eigenen Rumpf gebundenen Symbole aus Domänen-Assemblies (Typen, Member, Konstruktoren) — deklariert die Karte
    /// sie (Symbol-Identität, nicht Name)? Parameter/Lokale/BCL zählen nicht; <c>this.State</c> ist generierter Rahmen.
    /// </summary>
    private List<string> Gegenprobe(Slot s, Arbeitskarte k)
    {
        var model = Model(s.Syntax.SyntaxTree);
        var fehlt = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var n in s.Syntax.Body?.DescendantNodes().OfType<SimpleNameSyntax>() ?? Enumerable.Empty<SimpleNameSyntax>())
        {
            var sym = model.GetSymbolInfo(n).Symbol;
            if (sym is IMethodSymbol { MethodKind: MethodKind.Constructor } c) sym = c.ContainingType;
            if (sym is null or IParameterSymbol or ILocalSymbol or IRangeVariableSymbol or INamespaceSymbol) continue;
            var asm = (sym as ITypeSymbol ?? sym.ContainingType)?.ContainingAssembly?.Name;
            if (!_domänen.Contains(asm ?? "")) continue;
            if (sym is IPropertySymbol p && p.ContainingType.Fq() == s.Klasse.Fq() && SymbolEqualityComparer.Default.Equals(p.Type, s.State)) continue;
            var id = sym is ITypeSymbol ts ? ts.OriginalDefinition.Fq() : sym.OriginalDefinition.ToDisplayString();
            if (!k.Symbole.Contains(id)) fehlt.Add(sym is ITypeSymbol ? sym.Name : $"{sym.ContainingType?.Name}.{sym.Name}");
        }
        return fehlt.ToList();
    }

    // ── Typen / Member ──

    private static readonly System.Collections.Immutable.ImmutableArray<IParameterSymbol> ImmutableEmpty =
        System.Collections.Immutable.ImmutableArray<IParameterSymbol>.Empty;

    private List<INamedTypeSymbol> Ausgänge(IMethodSymbol m)
    {
        // IEnumerable<OneOf<A,B,C>> → A,B,C (per Symbol; Stelligkeit egal).
        var t = m.ReturnType as INamedTypeSymbol;
        var oneOf = t?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
        return Vertrag.IstOneOf(oneOf) ? oneOf!.TypeArguments.OfType<INamedTypeSymbol>().ToList() : new();
    }

    private static List<ISymbol> ZustandsMember(INamedTypeSymbol state) => state.GetMembers()
        .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsImplicitlyDeclared && !m.IsStatic)
        .Where(m => m is IPropertySymbol || m is IMethodSymbol { MethodKind: MethodKind.Ordinary })
        .Where(m => m.DeclaringSyntaxReferences.Length > 0)
        .OrderBy(m => Projektlage.IstGeneriert(m.DeclaringSyntaxReferences[0].SyntaxTree))   // handgeschrieben zuerst
        .ThenBy(m => m.DeclaringSyntaxReferences[0].SyntaxTree.FilePath, StringComparer.Ordinal)
        .ThenBy(m => m.DeclaringSyntaxReferences[0].Span.Start)
        .ToList();

    /// <summary>Handgeschriebene Nicht-Slot-Methoden der eigenen Decider-/Applier-Klasse (alle partiellen Teile).</summary>
    private List<IMethodSymbol> Helfer(Slot s)
    {
        var slotMethoden = _slots.Where(x => x.Klasse.Fq() == s.Klasse.Fq()).Select(x => x.Methode).ToList();
        return s.Klasse.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared
                        && !slotMethoden.Any(x => SymbolEqualityComparer.Default.Equals(x, m))
                        && m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree)))
            .ToList();
    }

    /// <summary>Eine Member-Zeile: Signatur, bei Expression-Bodies der Ausdruck als Code, dahinter der Doku-Kommentar (falls vorhanden).</summary>
    private static string MemberZeile(ISymbol m)
    {
        var sig = (m.IsStatic ? "static " : "") + m switch
        {
            IPropertySymbol p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}{Accessor(p)}",
            IMethodSymbol ms => $"{ms.ReturnType.ToDisplayString(Kurz)} {ms.Name}({string.Join(", ", ms.Parameters.Select(x => $"{x.Type.ToDisplayString(Kurz)} {x.Name}"))})",
            _ => m.Name,
        };
        if (Ausdruck(m) is { } expr) sig += " => " + expr;
        var doku = DokuVon(m);
        return doku == null ? sig : $"{sig}   /// {doku}";
    }

    /// <summary>Accessor-Satz einer Property als Code-Fakt (nur bei nicht-berechneten Properties).</summary>
    private static string Accessor(IPropertySymbol p)
    {
        if (Ausdruck(p) != null) return "";
        if (p.SetMethod == null) return " { get; }";
        return p.SetMethod.IsInitOnly ? " { get; init; }" : " { get; set; }";
    }

    private static string? Ausdruck(ISymbol m)
    {
        var syn = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var expr = syn switch
        {
            PropertyDeclarationSyntax p => p.ExpressionBody?.Expression,
            MethodDeclarationSyntax md => md.ExpressionBody?.Expression,
            _ => null,
        };
        return expr == null ? null : Regex.Replace(expr.ToString(), @"\s+", " ");
    }

    private static ITypeSymbol? MemberTypSymbol(ISymbol m) => m switch
    {
        IPropertySymbol p => p.Type, IMethodSymbol ms => ms.ReturnType, IFieldSymbol f => f.Type, _ => null,
    };

    private KartenEintrag Eintrag(INamedTypeSymbol t, string rolle, Arbeitskarte k)
    {
        k.Symbole.Add(t.OriginalDefinition.Fq());
        if (t.TypeKind == TypeKind.Enum)
        {
            var werte = t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
            foreach (var w in werte) k.Symbole.Add(w.OriginalDefinition.ToDisplayString());
            return new KartenEintrag(rolle, $"enum {t.Name} {{ {string.Join(", ", werte.Select(f => f.Name))} }}", DokuVon(t), new());
        }

        var ctor = PrimärKonstruktor(t);
        var felder = Felder(t);
        var dekl = ctor != null
            ? $"{t.Name}({string.Join(", ", ctor.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}"))})"
            : felder.Count == 0 ? $"{t.Name}()" : $"{t.Name} {{ {string.Join("; ", felder.Select(f => $"{FeldTyp(f)?.ToDisplayString(Kurz)} {f.Name}"))} }}";

        // Alle öffentlichen Member (auch die Record-Properties) sind Teil des Vokabulars.
        foreach (var m in t.GetMembers().Where(m => m.DeclaredAccessibility == Accessibility.Public))
            k.Symbole.Add(m.OriginalDefinition.ToDisplayString());

        // Zusätzliche öffentliche Oberfläche (berechnete/statische Properties, Methoden) — mit Ausdruck, ohne Block-Rümpfe.
        var ctorNamen = ctor?.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal) ?? new();
        var extra = t.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsImplicitlyDeclared
                        && m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree)))
            .Where(m => m is IPropertySymbol p && !ctorNamen.Contains(p.Name) && (ctor != null || p.SetMethod == null)
                        || m is IMethodSymbol { MethodKind: MethodKind.Ordinary })
            .Select(MemberZeile)
            .ToList();
        return new KartenEintrag(rolle, dekl, DokuVon(t), extra);
    }

    private static IMethodSymbol? PrimärKonstruktor(INamedTypeSymbol t) => t.InstanceConstructors
        .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0
                    && !(c.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, t)))
        .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();

    private static List<ISymbol> Felder(INamedTypeSymbol t)
    {
        var ctor = PrimärKonstruktor(t);
        if (ctor != null) return ctor.Parameters.Cast<ISymbol>().ToList();
        return t.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && p.SetMethod != null).Cast<ISymbol>().ToList();
    }

    private static ITypeSymbol? FeldTyp(ISymbol f) => f switch { IParameterSymbol p => p.Type, _ => MemberTypSymbol(f) };

    private static IEnumerable<ITypeSymbol> Feldtypen(INamedTypeSymbol t) => Felder(t).SelectMany(f => Entpacke(FeldTyp(f)));

    /// <summary>Typgleichheit inkl. Nullbarkeit (int ≠ int?, string ≠ string?).</summary>
    private static bool Gleich(ITypeSymbol a, ITypeSymbol b) => SymbolEqualityComparer.IncludeNullability.Equals(a, b);

    /// <summary>Elementtyp einer Sammlung (per Symbol: das <c>IEnumerable&lt;T&gt;</c>-Interface; <c>string</c> nicht).</summary>
    private static ITypeSymbol? ElementTyp(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_String) return null;
        if (t is IArrayTypeSymbol a) return a.ElementType;
        var ie = (t as INamedTypeSymbol)?.AllInterfaces.Concat(t is INamedTypeSymbol n ? new[] { n } : Array.Empty<INamedTypeSymbol>())
            .FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        return ie?.TypeArguments[0];
    }

    private static IEnumerable<ITypeSymbol> Entpacke(ITypeSymbol? t)
    {
        if (t == null) yield break;
        if (t is IArrayTypeSymbol a) { foreach (var x in Entpacke(a.ElementType)) yield return x; yield break; }
        yield return t;
        if (t is INamedTypeSymbol n)
            foreach (var arg in n.TypeArguments)
                foreach (var x in Entpacke(arg)) yield return x;
    }

    /// <summary>Transitive Hülle der Domänen-Quelltypen (VOs, Enums, …) — vollständig, ohne Tiefengrenze.</summary>
    private IEnumerable<INamedTypeSymbol> Transitiv(IEnumerable<ITypeSymbol> saat, HashSet<string> schon)
    {
        var offen = new Queue<ITypeSymbol>(saat);
        while (offen.Count > 0)
        {
            if (offen.Dequeue() is not INamedTypeSymbol t) continue;
            if (!_domänen.Contains(t.ContainingAssembly?.Name ?? "")) continue;
            if (!t.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree))) continue;
            if (!schon.Add(t.OriginalDefinition.Fq())) continue;
            yield return t;
            if (t.TypeKind != TypeKind.Enum) foreach (var x in Feldtypen(t)) offen.Enqueue(x);
        }
    }

    // ── Beispiel (B5) ──

    private (string, string)? Beispiel(Slot s, List<INamedTypeSymbol> ausgänge)
    {
        var eigene = ausgänge.Select(a => a.Fq()).ToHashSet(StringComparer.Ordinal);
        var kandidat = Nachbarn(s, s.Art).Where(g => RumpfStatus(g) == "geschrieben")
            .Select(g => (g, Rumpf: Rumpf(g.Syntax), Geteilt: Ausgänge(g.Methode).Count(a => eigene.Contains(a.Fq()))))
            .OrderByDescending(x => x.Geteilt).ThenBy(x => x.Rumpf.Length).ThenBy(x => x.g.Disc.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        return kandidat.g == null ? null : (kandidat.g.Disc.Name, kandidat.Rumpf);
    }

    /// <summary>„fehlt“ = der Scaffolder-Platzhalter (<c>throw new NotImplementedException</c>, per Symbol); ein leerer Rumpf ist bewusst.</summary>
    private string RumpfStatus(Slot s)
    {
        if (s.Syntax.ExpressionBody != null) return "geschrieben";
        if (s.Syntax.Body is not { } b) return "fehlt";
        var model = Model(s.Syntax.SyntaxTree);
        if (b.DescendantNodes().OfType<ThrowStatementSyntax>().Any(t => t.Expression != null
                && Vertrag.Ist(model.GetTypeInfo(t.Expression).Type, typeof(NotImplementedException))))
            return "fehlt";
        return b.Statements.Count > 0 ? "geschrieben" : "leer";
    }

    private static string Rumpf(MethodDeclarationSyntax m)
    {
        if (m.ExpressionBody is { } e) return e.Expression + ";";
        if (m.Body is not { } b) return "";
        var text = b.SyntaxTree.GetText().ToString(
            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(b.OpenBraceToken.Span.End, b.CloseBraceToken.SpanStart));
        var zeilen = text.Replace("\r\n", "\n").Split('\n').Where(l => !l.Contains(PromptMarke)).ToList();
        var min = zeilen.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select(l => (l.Length >= min ? l[min..] : l.TrimStart()).TrimEnd())).Trim('\n');
    }

    // ── Kommentare ──

    private static string? PromptAus(MethodDeclarationSyntax m) =>
        m.Body?.DescendantTrivia().Select(t => t.ToString().Trim())
            .FirstOrDefault(t => t.StartsWith(PromptMarke))?[PromptMarke.Length..].Trim();

    /// <summary>Zeilenkommentare vor einer Deklaration, verbatim (reine Trennlinien ohne Buchstaben ausgenommen).</summary>
    private static IEnumerable<string> Kommentare(SyntaxNode n) => n.GetLeadingTrivia()
        .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
        .Select(t => t.ToString().TrimStart('/').Trim())
        .Where(t => t.Any(char.IsLetter));

    /// <summary>Der <c>&lt;summary&gt;</c>-Doku-Kommentar verbatim (Whitespace normalisiert, Verweise als Name) — oder null.</summary>
    private static string? DokuVon(ISymbol s)
    {
        var xml = s.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            var summary = XElement.Parse(xml).Element("summary");
            if (summary == null) return null;
            foreach (var r in summary.Descendants().Where(e => e.Name == "see" || e.Name == "paramref" || e.Name == "c").ToList())
                r.ReplaceWith(r.Attribute("cref")?.Value.Split('.').Last().Split(':').Last() ?? r.Attribute("name")?.Value ?? r.Value);
            var text = Regex.Replace(summary.Value, @"\s+", " ").Trim();
            return text.Length == 0 ? null : text;
        }
        catch { return null; }
    }

    // ── Szenarien (aus bestehenden Tests, über die FORM der Test-DSL) ──

    private IEnumerable<KartenSzenario> Szenarien(Slot s)
    {
        var decide = s.Art == "decide";
        foreach (var comp in _tests)
            foreach (var tree in comp.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)))
            {
                var model = comp.GetSemanticModel(tree);
                foreach (var st in tree.GetRoot().DescendantNodes().OfType<ExpressionStatementSyntax>())
                {
                    var kette = Kette(st.Expression, model);
                    if (kette == null || kette.State.Fq() != s.State.Fq()) continue;
                    var passt = decide
                        ? kette.WennTyp?.Fq() == s.Disc.Fq()
                        : kette.HatZustandsPrüfung && kette.Erwähnt.Contains(s.Disc.Fq());
                    if (!passt) continue;
                    var name = st.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.Text ?? "";
                    var zeile = st.GetLocation().GetLineSpan();
                    yield return new KartenSzenario(name, $"{Path.GetFileName(zeile.Path)}:{zeile.StartLinePosition.Line + 1}", kette.Schritte);
                }
            }
    }

    private sealed record DslKette(INamedTypeSymbol State, ITypeSymbol? WennTyp, bool HatZustandsPrüfung, HashSet<string> Erwähnt, List<string> Schritte);

    /// <summary>
    /// Eine Test-DSL-Kette erkennen: irgendwo in der Kette ein Aufruf mit genau EINEM <c>ICommand</c>-Parameter auf einem
    /// generischen Typ, dessen Typargument ein <c>IState</c> ist (das „Wenn“). Die Glieder davor/danach sind Arrange/Assert.
    /// </summary>
    private DslKette? Kette(ExpressionSyntax e, SemanticModel model)
    {
        var glieder = new List<InvocationExpressionSyntax>();
        var cur = e;
        while (cur is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
        {
            glieder.Add(inv);
            cur = ma.Expression;
        }
        if (glieder.Count < 2) return null;
        glieder.Reverse();

        INamedTypeSymbol? state = null; ITypeSymbol? wennTyp = null; var prüft = false;
        var erwähnt = new HashSet<string>(StringComparer.Ordinal);
        var schritte = new List<string>();
        foreach (var g in glieder)
        {
            if (model.GetSymbolInfo(g).Symbol is not IMethodSymbol m) return null;
            var arg0 = m.ContainingType?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
            if (arg0 != null && Sym.Implements(arg0, _iState)) state ??= arg0;
            if (m.Parameters.Length == 1 && IstOderImplementiert(m.Parameters[0].Type, _iCommand) && !m.Parameters[0].IsParams
                && g.ArgumentList.Arguments.Count == 1)
                wennTyp = model.GetTypeInfo(g.ArgumentList.Arguments[0].Expression).Type;
            if (g.ArgumentList.Arguments.Any(a => a.Expression is LambdaExpressionSyntax)) prüft = true;
            foreach (var n in g.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                if (model.GetTypeInfo(n).Type is { } ot) erwähnt.Add(ot.Fq());
            foreach (var ta in m.TypeArguments) erwähnt.Add(ta.Fq());
            var name = ((MemberAccessExpressionSyntax)g.Expression).Name.ToString();
            schritte.Add($".{name}{Regex.Replace(Regex.Replace(g.ArgumentList.ToString(), @"\s+", " "), @"\( ", "(")}");
        }
        if (state == null || wennTyp == null) return null;
        return new DslKette(state, wennTyp, prüft, erwähnt, schritte.Skip(1).ToList());   // Glied 0 = Einstieg (Fabrik/Id)
    }

    // ── Hilfen ──

    private static bool IstOderImplementiert(ITypeSymbol t, INamedTypeSymbol? marker) =>
        marker != null && (t.Fq() == marker.Fq() || Sym.Implements(t, marker));

    private SemanticModel Model(SyntaxTree tree) => _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    private static IEnumerable<INamedTypeSymbol> AlleTypen(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in AlleTypen(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }
}

/// <summary>
/// CLI der Arbeitskarten: <c>--karte</c> (Übersicht aller Slots mit Größenvergleich), <c>--karte &lt;Disc|Aggregat.Disc&gt;</c>
/// (eine Karte als Text), <c>--karten &lt;verz&gt;</c> (alle Karten als .txt + übersicht.md + karten.json).
/// </summary>
public static class KartenCli
{
    public static async Task<int> LaufAsync(string[] args, Solution solution, Projektlage lage, DomainModel dom, KnowledgeGraph graph)
    {
        var bauer = await KartenBauer.ErstelleAsync(solution, lage, dom, graph);
        string? Wert(string flag) { var i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null; }

        var ziel = Wert("--karte");
        if (ziel != null)
        {
            var karten = bauer.Baue(ziel);
            if (karten.Count == 0) { Console.Error.WriteLine($"❌ Kein Decide-/Apply-Slot für '{ziel}'."); return 1; }
            foreach (var k in karten)
            {
                var text = k.AlsText();
                Console.WriteLine($"\n════ Arbeitskarte {k.Art} {k.Id}  ({Rel(k.Datei, solution)}:{k.Zeile})  ≈ {Arbeitskarte.Token(text)} Token ════\n");
                Console.WriteLine(text);
            }
            return 0;
        }

        var alle = bauer.Baue();
        var mass = new Massstab(lage);
        var zeilen = alle.Select(k => (k, Karte: Arbeitskarte.Token(k.AlsText()), Datei: mass.Datei(k.Datei), Ordner: mass.Ordner(k.Datei))).ToList();

        var verz = Wert("--karten");
        if (verz != null)
        {
            Directory.CreateDirectory(verz);
            foreach (var k in alle) await File.WriteAllTextAsync(Path.Combine(verz, $"{k.Art}-{k.Id}.txt"), k.AlsText());
            var md = new StringBuilder();
            md.AppendLine("| Art | Slot | Rumpf | Karte ≈Tok | Datei ≈Tok | Ordner ≈Tok | Szenarien | Kommentare | Beispiel | Gegenprobe |");
            md.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---|---|");
            foreach (var z in zeilen)
                md.AppendLine($"| {z.k.Art} | {z.k.Id} | {Symbol(z.k.Rumpf)} | {z.Karte} | {z.Datei} | {z.Ordner} | {z.k.Szenarien.Count} | {z.k.Kommentare.Count} | {z.k.Beispiel?.Disc ?? "—"} | {Probe(z.k)} |");
            md.AppendLine($"\nDomäne gesamt ≈ {mass.Domäne} Token (handgeschriebene Quelltexte der Domänen-Projekte).");
            await File.WriteAllTextAsync(Path.Combine(verz, "übersicht.md"), md.ToString());
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                domaeneToken = mass.Domäne,
                karten = zeilen.Select(z => new
                {
                    art = z.k.Art, id = z.k.Id, datei = Rel(z.k.Datei, solution), zeile = z.k.Zeile, rumpf = z.k.Rumpf,
                    token = new { karte = z.Karte, datei = z.Datei, ordner = z.Ordner },
                    szenarien = z.k.Szenarien.Count, kommentare = z.k.Kommentare.Count, beispiel = z.k.Beispiel?.Disc,
                    nichtAufKarte = z.k.NichtAufKarte, text = z.k.AlsText(),
                }),
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await File.WriteAllTextAsync(Path.Combine(verz, "karten.json"), json);
            Console.WriteLine($"✅ {alle.Count} Karten → {verz}");
        }

        Console.WriteLine($"\n── Arbeitskarten: {alle.Count} Slots (Decide/Apply) ──");
        Console.WriteLine($"   {"Art",-7}{"Slot",-44}{"Rumpf",-9}{"Karte",7}{"Datei",8}{"Ordner",8}{"Szen.",7}{"Komm.",7}  Gegenprobe");
        foreach (var z in zeilen)
            Console.WriteLine($"   {z.k.Art,-7}{z.k.Id,-44}{Symbol(z.k.Rumpf),-9}{z.Karte,7}{z.Datei,8}{z.Ordner,8}{z.k.Szenarien.Count,7}{z.k.Kommentare.Count,7}  {Probe(z.k)}");
        var geprüft = alle.Where(k => k.NichtAufKarte != null).ToList();
        Console.WriteLine($"\n   Gegenprobe: {geprüft.Count(k => k.NichtAufKarte!.Count == 0)}/{geprüft.Count} geschriebene Rümpfe benutzen nur Symbole, die ihre Karte deklariert.");
        if (zeilen.Count > 0)
        {
            var med = zeilen.Select(z => z.Karte).OrderBy(x => x).ElementAt(zeilen.Count / 2);
            Console.WriteLine($"   Karte: Median ≈ {med}, Max ≈ {zeilen.Max(z => z.Karte)} Token · Ordner Median ≈ "
                + $"{zeilen.Select(z => z.Ordner).OrderBy(x => x).ElementAt(zeilen.Count / 2)} · Domäne ≈ {mass.Domäne} Token (Schätzung Zeichen/3,3)");
        }
        return 0;
    }

    private static string Probe(Arbeitskarte k) =>
        k.NichtAufKarte == null ? "—" : k.NichtAufKarte.Count == 0 ? "✓ vollständig" : "✗ fehlt: " + string.Join(", ", k.NichtAufKarte);

    private static string Symbol(string rumpf) => rumpf switch { "geschrieben" => "✓", "leer" => "∅ leer", _ => "⚙ fehlt" };

    private static string Rel(string pfad, Solution s) =>
        Path.GetRelativePath(Path.GetDirectoryName(s.FilePath) ?? ".", pfad);

    /// <summary>Vergleichsgrößen: was ein LLM OHNE Karte typischerweise bekäme (Datei, Ordner, ganze Domäne).</summary>
    private sealed class Massstab(Projektlage lage)
    {
        private readonly Dictionary<string, int> _cache = new(StringComparer.Ordinal);
        private int Tok(string pfad) => _cache.TryGetValue(pfad, out var t) ? t
            : _cache[pfad] = File.Exists(pfad) ? Arbeitskarte.Token(File.ReadAllText(pfad)) : 0;
        public int Datei(string pfad) => Tok(pfad);
        public int Ordner(string pfad) => Directory.GetFiles(Path.GetDirectoryName(pfad)!, "*.cs").Sum(Tok);
        public int Domäne { get; } = lage.DomänenCompilations.SelectMany(c => c.SyntaxTrees)
            .Where(t => !Projektlage.IstGeneriert(t) && File.Exists(t.FilePath))
            .Select(t => t.FilePath).Distinct().Sum(p => Arbeitskarte.Token(File.ReadAllText(p)));
    }
}
