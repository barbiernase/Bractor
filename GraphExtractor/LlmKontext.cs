using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  LLM-Kontext je Code-Block (docs/konzept-llm-minimalkontext.md)
//
//  Ein Aufruf = FESTER TEIL (Graph-Skelett, für alle Slots gleich) + SLOT-TEIL (je Code-Block). Alles aus Code-Fakten:
//  Roslyn-Symbole [S], Graph-/Editor-Kanten [K], Nachbar-Rümpfe [N]. Einzige menschliche Eingabe ist der Auftrag [L] —
//  aus dem LLM-Knoten im Board (intent → promptZiel → Code-Block), aus der „// 🤖 Prompt:“-Zeile im Rumpf oder per CLI.
//  Der EIGENE Rumpf fließt nie ein; im Skelett werden die Guards des eigenen Decide entfernt.
// ════════════════════════════════════════════════════════════════════════════

public sealed class SlotKontext
{
    public required SlotInventar.SlotRef Slot { get; init; }
    public required string Schlüssel { get; init; }         // stabile Slot-Identität (wie im Board)
    public required string Titel { get; init; }
    public required string RumpfStatus { get; init; }
    public string? Auftrag { get; set; }
    public string? AuftragQuelle { get; set; }
    public string Text { get; set; } = "";
    public int NachbarBlöcke { get; set; }
    public int KopplungBlöcke { get; set; }
}

public sealed class KontextBauer
{
    public const string PromptMarke = "// 🤖 Prompt:";   // Spiegel von SimHost.CodeSync.PromptMarke

    private static readonly SymbolDisplayFormat Kurz = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                              | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly string[] Arten = { "decide", "apply", "projektion", "reaktion", "reader", "pipeline", "store" };

    private readonly SlotInventar _inv;
    private readonly DomainModel _dom;
    private readonly KnowledgeGraph _graph;
    private readonly CompositionRoot _cr;
    private readonly JsonElement _board;        // Editor-Modell (ModellMapper.ZuBoardJson) — Quelle des Skeletts
    private readonly List<Compilation> _comps;
    private readonly HashSet<string> _domänen, _framework;
    private readonly INamedTypeSymbol? _iTransient, _iWriteStore, _iReadStore, _iPull, _iAppend, _iTrigger, _iSelf, _iEvent, _iCommand, _iQuery, _iReadModel, _iResponse;
    private readonly Dictionary<string, (string Intent, string Quelle)> _llmKnoten;

    public KontextBauer(Projektlage lage, DomainModel dom, KnowledgeGraph graph, CompositionRoot cr, string? boardDatei)
    {
        _inv = new SlotInventar(lage, dom);
        _dom = dom; _graph = graph; _cr = cr;
        _board = JsonDocument.Parse(ModellMapper.ZuBoardJson(graph, dom, cr)).RootElement;
        _comps = lage.Compilations;
        _domänen = new HashSet<string>(lage.DomänenAssemblies, StringComparer.Ordinal);
        _framework = lage.Analyse.Select(a => a.Compilation.AssemblyName ?? "").Where(n => !_domänen.Contains(n)).ToHashSet(StringComparer.Ordinal);
        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iTransient = Get(Vertrag.ITransientEvent); _iWriteStore = Get(Vertrag.IWriteStore); _iReadStore = Get(Vertrag.IReadStore);
        _iPull = Get(Vertrag.IPullSubscriber); _iAppend = Get(Vertrag.IAppendProjektion); _iTrigger = Get(Vertrag.IPipelineTrigger);
        _iSelf = Get(Vertrag.IPipelineSelfMessage); _iEvent = Get(Vertrag.IEvent); _iCommand = Get(Vertrag.ICommand);
        _iQuery = Get(Vertrag.IQuery); _iReadModel = Get(Vertrag.IReadModel); _iResponse = Get(Vertrag.IQueryResponse);
        _llmKnoten = LiesLlmKnoten(boardDatei);
    }

    public IEnumerable<SlotInventar.SlotRef> Slots => _inv.Slots.Where(s => Arten.Contains(s.Art))
        .OrderBy(s => Array.IndexOf(Arten, s.Art)).ThenBy(s => Besitzer(s), StringComparer.Ordinal).ThenBy(s => s.Disc, StringComparer.Ordinal);

    public List<SlotKontext> Baue(string? filter, string? cliAuftrag) => Slots
        .Where(s => filter == null || s.Disc == filter || $"{Besitzer(s)}.{s.Disc}" == filter || $"{s.Klasse.Name}.{s.Disc}" == filter)
        .Select(s => Baue(s, cliAuftrag)).ToList();

    // ── Identität je Slot: wie das Editor-Board ihn adressiert ──

    /// <summary>Decide/Apply → Aggregat; Store → Store-Interface; sonst die Klasse.</summary>
    private string Besitzer(SlotInventar.SlotRef s) => s.Art switch
    {
        "decide" or "apply" => s.State?.Name ?? s.Klasse.Name,
        "store" => StoreVon(s)?.Name ?? s.Klasse.Name,
        _ => s.Klasse.Name,
    };

    private string Schlüssel(SlotInventar.SlotRef s) => $"{s.Art}|{Besitzer(s)}|{s.Disc}";

    private StoreRaw? StoreVon(SlotInventar.SlotRef s) => _dom.Stores.FirstOrDefault(st => st.ImplsFull.Contains(s.Klasse.Fq())
        && st.Fns.Any(f => f.Name == s.Disc));

    // ── Auftrag aus dem Board: LLM-Knoten → promptZiel (Code-Knoten) → Slot, der den Code-Knoten als codeSrc trägt ──

    private static Dictionary<string, (string, string)> LiesLlmKnoten(string? boardDatei)
    {
        var erg = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        if (boardDatei == null || !File.Exists(boardDatei)) return erg;
        var b = JsonDocument.Parse(File.ReadAllText(boardDatei)).RootElement;
        string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        IEnumerable<JsonElement> A(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();
        var code = new Dictionary<string, string>(StringComparer.Ordinal);   // codeNode-Id → Slot-Schlüssel
        void Merke(JsonElement n, string schlüssel) { if (S(n, "codeSrc") is { } c) code[c] = schlüssel; }
        foreach (var d in A(b, "decider")) Merke(d, $"decide|{S(d, "aggregat")}|{S(d, "command")}");
        foreach (var d in A(b, "applier")) Merke(d, $"apply|{S(d, "aggregat")}|{S(d, "event")}");
        foreach (var p in A(b, "projektionen")) foreach (var h in A(p, "handles")) Merke(h, $"projektion|{S(p, "name")}|{S(h, "event")}");
        foreach (var p in A(b, "reaktionen")) foreach (var h in A(p, "handles")) Merke(h, $"reaktion|{S(p, "name")}|{S(h, "event")}");
        foreach (var r in A(b, "reader")) foreach (var h in A(r, "handles")) Merke(h, $"reader|{S(r, "name")}|{S(h, "query")}");
        foreach (var p in A(b, "pipelines")) foreach (var h in A(p, "handles")) Merke(h, $"pipeline|{S(p, "name")}|{S(h, "input") ?? S(h, "event")}");
        foreach (var st in A(b, "stores"))
            foreach (var f in A(st, "writeFns").Concat(A(st, "readFns"))) Merke(f, $"store|{S(st, "name")}|{S(f, "name")}");
        foreach (var l in A(b, "llmNodes"))
            if (S(l, "promptZiel") is { } z && code.TryGetValue(z, out var schl) && !string.IsNullOrWhiteSpace(S(l, "intent")))
                erg[schl] = (S(l, "intent")!, $"LLM-Knoten „{S(l, "name")}“ im Board");
        return erg;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FESTER TEIL: das Graph-Skelett (aus dem Editor-Modell — Signaturen + Kanten + Guards, keine Rümpfe)
    // ════════════════════════════════════════════════════════════════════════

    public string Skelett(string? ohneGuardsVon = null)
    {
        var b = new StringBuilder();
        string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        bool Bo(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
        IEnumerable<JsonElement> A(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();
        IEnumerable<string> Str(JsonElement e, string p) => A(e, p).Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!);
        string F(JsonElement e, string p = "felder") => string.Join(", ", A(e, p).Select(f => $"{S(f, "name")}:{S(f, "typ")}"));

        var rec = A(_board, "records").ToDictionary(r => S(r, "name")!, r => r, StringComparer.Ordinal);
        b.AppendLine("# GRAPH-SKELETT der Domäne (Signaturen, Kanten, Guards — ohne Rümpfe)");
        foreach (var e in A(_board, "enums")) b.AppendLine($"enum {S(e, "name")} {{{string.Join(", ", Str(e, "werte"))}}}");
        foreach (var r in A(_board, "records"))
            if (S(r, "kind") is "valueobject" or "konfig" or "query" or "queryresponse")
                b.AppendLine($"{S(r, "kind")} {S(r, "name")}({F(r)})");
        foreach (var a in A(_board, "aggregate"))
        {
            var an = S(a, "name");
            b.AppendLine($"AGGREGAT {an}  state: {F(a, "state")}");
            foreach (var d in A(_board, "decider").Where(d => S(d, "aggregat") == an))
            {
                var eigen = ohneGuardsVon == $"decide|{an}|{S(d, "command")}";
                var outs = string.Join(" | ", A(d, "ergibt").Select(o => S(o, "event") + (!eigen && S(o, "guard") is { } g ? $" wenn {g}" : "")));
                var c = rec.GetValueOrDefault(S(d, "command") ?? "");
                b.AppendLine($"  decide {S(d, "command")}({(c.ValueKind == JsonValueKind.Object ? F(c) : "")}) -> {outs}");
            }
            foreach (var ap in A(_board, "applier").Where(x => S(x, "aggregat") == an))
            {
                var e = rec.GetValueOrDefault(S(ap, "event") ?? "");
                b.AppendLine($"  apply {S(ap, "event")}({(e.ValueKind == JsonValueKind.Object ? F(e) : "")})");
            }
        }
        foreach (var r in A(_board, "records").Where(r => S(r, "kind") == "rejection")) b.AppendLine($"ablehnung {S(r, "name")}({F(r)})");
        foreach (var s in A(_board, "sagas"))
            foreach (var st in A(s, "schritte"))
                b.AppendLine($"PROZESS {S(s, "name")}: wenn {string.Join(" + ", Str(st, "wenn"))} -> sende {S(st, "sende")}{(Bo(st, "sendeJe") ? " (je Element)" : "")}");
        var fnName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var st in A(_board, "stores"))
        {
            var fns = A(st, "writeFns").Concat(A(st, "readFns")).ToList();
            foreach (var f in fns) if (S(f, "_id") is { } id) fnName[id] = $"{S(st, "name")}.{S(f, "name")}";
            b.AppendLine($"STORE {S(st, "name")}: " + string.Join("; ", fns.Select(f =>
                $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}){(S(f, "rueckgabe") is { } r ? " -> " + r : "")}")));
        }
        foreach (var rm in A(_board, "readModels")) b.AppendLine($"readmodel {S(rm, "name")}({F(rm)}) @ {S(rm, "store")}");
        foreach (var p in A(_board, "projektionen").Concat(A(_board, "reaktionen")))
            b.AppendLine($"PROJEKTION {S(p, "name")} pull={Bo(p, "pull")} append={Bo(p, "append")}: " + string.Join("; ", A(p, "handles").Select(h =>
                $"{S(h, "event")} -> {string.Join(", ", Str(h, "fns").Select(x => fnName.GetValueOrDefault(x, x)))}{(Str(h, "publishes").Any() ? " / veröffentlicht " + string.Join(", ", Str(h, "publishes")) : "")}{(Str(h, "sends").Any() ? " / sendet " + string.Join(", ", Str(h, "sends")) : "")}")));
        foreach (var r in A(_board, "reader"))
            b.AppendLine($"READER {S(r, "name")} liest {S(r, "projektion")}: " + string.Join("; ", A(r, "handles").Select(h =>
                $"{S(h, "query")} -> {string.Join(", ", Str(h, "fns").Select(x => fnName.GetValueOrDefault(x, x)))} => {string.Join(" | ", Str(h, "responses"))}")));
        foreach (var p in A(_board, "pipelines"))
            b.AppendLine($"PIPELINE {S(p, "name")} dienste=[{string.Join(", ", Str(p, "dienste"))}] konfigs=[{string.Join(", ", Str(p, "konfigs"))}]: " + string.Join("; ", A(p, "handles").Select(h =>
                $"{S(h, "input")} ({S(h, "inputKind")}) -> {string.Join(", ", Str(h, "sends").Concat(Str(h, "emits")))}")));
        foreach (var t in A(_board, "triggers")) b.AppendLine($"trigger {S(t, "name")}({F(t)})");
        foreach (var f in A(_board, "frists"))
            b.AppendLine($"FRIST {S(f, "name")}: plant bei {string.Join(", ", Str(f, "plant"))}; storniert bei {string.Join(", ", Str(f, "storniert"))}; fällig -> {S(f, "sendet")}");
        foreach (var d in A(_board, "dienste")) b.AppendLine($"dienst {S(d, "vertrag")} -> {S(d, "name")}");
        return b.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SLOT-TEIL
    // ════════════════════════════════════════════════════════════════════════

    private SlotKontext Baue(SlotInventar.SlotRef s, string? cliAuftrag)
    {
        var m = s.Methode!;
        var model = Model(s.Rumpf.SyntaxTree);
        var k = new SlotKontext { Slot = s, Schlüssel = Schlüssel(s), Titel = $"{s.Art} {Besitzer(s)}.{s.Disc}{(s.Art == "store" ? " @ " + s.Klasse.Name : "")}", RumpfStatus = RumpfStatus(s, model) };
        if (_llmKnoten.TryGetValue(k.Schlüssel, out var llm)) { k.Auftrag = llm.Intent; k.AuftragQuelle = llm.Quelle; }
        else if (PromptAus(s) is { } p) { k.Auftrag = p; k.AuftragQuelle = "„// 🤖 Prompt:“ im Rumpf"; }
        else if (cliAuftrag != null) { k.Auftrag = cliAuftrag; k.AuftragQuelle = "Kommandozeile (--auftrag, steht für den LLM-Knoten)"; }

        var b = new StringBuilder();
        void Kopf(string t) { b.AppendLine(); b.AppendLine("## " + t); }
        var pos = m.DeclaringSyntaxReferences[0].GetSyntax().GetLocation().GetLineSpan();

        b.AppendLine($"# SLOT-TEIL {k.Titel}");
        Kopf("AUFTRAG [L]");
        b.AppendLine(k.Auftrag == null ? "— kein LLM-Knoten an diesem Code-Block (ohne Auftrag wird kein Aufruf ausgelöst)" : $"{k.Auftrag}   (Quelle: {k.AuftragQuelle})");

        Kopf("ANKER [S]");
        b.AppendLine($"{s.Klasse.Name}.{m.Name}   {Rel(pos.Path)}:{pos.StartLinePosition.Line + 1}   Rumpf: {k.RumpfStatus}{(k.RumpfStatus == "geschrieben" ? " (wird nicht mitgegeben)" : "")}");

        Kopf("SIGNATUR [S]");
        b.AppendLine($"{m.ReturnType.ToDisplayString(Kurz)} {m.Name}({string.Join(", ", m.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}"))})");

        Kopf("VERTRAG (nur Erzwungenes) [S]");
        foreach (var v in Vertragszeilen(s)) b.AppendLine(v);

        Kopf("ERREICHBAR IM RUMPF [S]");
        foreach (var e in Erreichbar(s)) b.AppendLine(e);

        var kommentare = Kommentare(s).ToList();
        Kopf("KOMMENTARE (wörtlich) [S]");
        if (kommentare.Count == 0) b.AppendLine("keine");
        foreach (var c in kommentare) b.AppendLine(c);

        Kopf("TYPEN (transitive Hülle der Domänen-Typen; /// = Doku-Kommentar) [S]");
        foreach (var t in Typen(s)) b.AppendLine(t);

        Kopf("GRAPH-UMFELD [K]");
        foreach (var u in Umfeld(s)) b.AppendLine(u);

        var nachbarn = Nachbarn(s).ToList();
        k.NachbarBlöcke = nachbarn.Count;
        Kopf($"NACHBAR-CODE-BLÖCKE: alle anderen Code-Blöcke von {s.Klasse.Name} [N]");
        if (nachbarn.Count == 0) b.AppendLine(ArtgenossenHinweis(s));
        foreach (var (titel, text) in nachbarn) { b.AppendLine($"// ── {titel}"); b.AppendLine(text); }

        var kopplung = Kopplung(s).ToList();
        k.KopplungBlöcke = kopplung.Count;
        Kopf("KOPPLUNG: Rümpfe gekoppelter Code-Blöcke [K]");
        if (kopplung.Count == 0) b.AppendLine("keine");
        foreach (var (titel, text) in kopplung) { b.AppendLine($"// ── {titel}"); b.AppendLine(text); }

        k.Text = b.ToString().TrimStart('\n');
        return k;
    }

    // ── Vertrag: nur, was Compiler/Marker/Framework erzwingen ──

    private IEnumerable<string> Vertragszeilen(SlotInventar.SlotRef s)
    {
        var aus = Ausgänge(s).ToList();
        switch (s.Art)
        {
            case "decide":
                yield return $"Ausgaben ⊆ {{{string.Join(", ", aus.Select(a => a.Name))}}} — Compiler (OneOf-Signatur)";
                var abl = aus.Where(a => Sym.Implements(a, _iTransient)).ToList();
                if (abl.Count > 0) yield return $"Ablehnung ({string.Join(", ", abl.Select(a => a.Name))}) nur als einzige Ausgabe — Framework-Laufzeit (Aggregat-Actor wirft bei gemischtem Ergebnis)";
                break;
            case "apply":
                yield return "Rückgabe void — Compiler";
                break;
            case "projektion":
            case "reaktion":
                yield return Sym.Implements(s.Klasse, _iPull)
                    ? "Transport: geordneter Pull (IPullSubscriber) — jedes Event genau einmal, in Reihenfolge"
                    : "Transport: Signal — best-effort, darf verloren/doppelt/ungeordnet ankommen";
                if (s.Art == "projektion")
                    yield return Sym.Implements(s.Klasse, _iAppend)
                        ? "Garantie: append-artig (IAppendProjektion) — Co-Commit-Store, exactly-once (Boot-Check GA-1)"
                        : "Garantie: Upsert — at-least-once, Schreiben muss idempotent sein";
                else
                    yield return "Senden nur per yield — Analyzer CQRS020/021";
                break;
            case "reader":
                var rd = _dom.Readers.FirstOrDefault(r => r.Full == s.Klasse.Fq());
                yield return $"Antworten ⊆ {{{string.Join(", ", aus.Select(a => a.Name))}}} — Compiler (Signatur)";
                if (rd != null) yield return $"TrackDeps = {rd.TrackDeps.ToString().ToLowerInvariant()} (Attribut [ProjectionReader], Default true)";
                break;
            case "pipeline":
                var input = s.Parameter[0].Type;
                yield return "Eingangskanal: " + (Sym.Implements(input, _iTrigger) ? "Trigger (IPipelineTrigger)"
                    : Sym.Implements(input, _iSelf) ? "Self-Nachricht (IPipelineSelfMessage)"
                    : Sym.Implements(input, _iTransient) ? "Ablehnung (ITransientEvent)" : "Event (IEvent)") + " — Marker";
                break;
            case "store":
                yield return "Signatur vom Store-Interface vorgegeben — Compiler";
                break;
        }
    }

    // ── Erreichbar: Parameter, State, Konstruktor, Felder, geerbte Member, Helfer, Konstanten ──

    private IEnumerable<string> Erreichbar(SlotInventar.SlotRef s)
    {
        foreach (var p in s.Parameter) yield return $"Parameter  {p.Name} : {p.Type.ToDisplayString(Kurz)}";
        if (s.State != null) yield return $"State      this.State : {s.State.Name}   ({(s.Art == "decide" ? "nur lesen" : "schreibbar")})";
        foreach (var c in s.Klasse.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared || c.Parameters.Length > 0))
            yield return $"Konstrukt. {s.Klasse.Name}({string.Join(", ", c.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}"))})"
                         + (c.DeclaringSyntaxReferences.All(r => Projektlage.IstGeneriert(r.SyntaxTree)) ? "   [generiert]" : "");
        foreach (var f in Felder(s))
            yield return $"Feld       {f.Name} : {TypVon(f)!.ToDisplayString(Kurz)}   [{FeldArt(TypVon(f)!)}]";
        for (var bt = s.Klasse.BaseType; bt != null && bt.SpecialType != SpecialType.System_Object; bt = bt.BaseType)
        {
            if (!_domänen.Contains(bt.ContainingAssembly?.Name ?? "") && !_framework.Contains(bt.ContainingAssembly?.Name ?? "")) continue;
            foreach (var mm in bt.GetMembers().Where(x => x.DeclaredAccessibility is Accessibility.Protected or Accessibility.Public
                                                          && !x.IsImplicitlyDeclared && x is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol))
                yield return $"Geerbt     {Sig(mm)}   (aus {bt.Name})";
        }
        foreach (var h in Helfer(s)) yield return $"Helfer     {Sig(h)}";
        foreach (var c in s.Klasse.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsConst || f.IsStatic && f.IsReadOnly).Where(IstHand))
            yield return $"Konstante  {c.Type.ToDisplayString(Kurz)} {c.Name}{(c.HasConstantValue ? " = " + c.ConstantValue : "")}";
    }

    private IEnumerable<ISymbol> Felder(SlotInventar.SlotRef s) => s.Klasse.GetMembers()
        .Where(x => !x.IsStatic && !x.IsImplicitlyDeclared && IstHand(x) && x is IFieldSymbol { IsConst: false } or IPropertySymbol)
        .Where(x => TypVon(x) is { } t && !(s.State != null && SymbolEqualityComparer.Default.Equals(t, s.State)));

    /// <summary>Kategorie eines injizierten Felds — über Marker, nicht über Namen.</summary>
    private string FeldArt(ITypeSymbol t)
    {
        if (Sym.Implements(t, _iWriteStore) || Sym.Implements(t, _iReadStore)) return "Store";
        if (_dom.Konfigs.Any(k => k.Full == t.Fq())) return "Konfig";
        var asm = t.ContainingAssembly?.Name ?? "";
        if (t.SpecialType != SpecialType.None) return "Wert";
        if (_domänen.Contains(asm)) return "Domänen-Dienst";
        if (_framework.Contains(asm)) return "Framework";
        return "Bibliothek";
    }

    private List<IMethodSymbol> Helfer(SlotInventar.SlotRef s)
    {
        var slotMethoden = _inv.Slots.Where(x => x.Klasse.Fq() == s.Klasse.Fq()).Select(x => x.Methode).ToList();
        return s.Klasse.GetMembers().OfType<IMethodSymbol>()
            .Where(x => x.MethodKind == MethodKind.Ordinary && !x.IsImplicitlyDeclared && IstHand(x)
                        && !slotMethoden.Any(y => SymbolEqualityComparer.Default.Equals(x, y)))
            .ToList();
    }

    // ── Kommentare: wörtlich, mit Herkunft ──

    private IEnumerable<string> Kommentare(SlotInventar.SlotRef s)
    {
        var syn = s.Methode!.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).First(n => !Projektlage.IstGeneriert(n.SyntaxTree));
        if (DokuVon(s.Methode!) is { } md) yield return $"[/// an {s.Methode!.Name}] {md}";
        foreach (var c in Zeilenkommentare(syn)) yield return $"[// vor {s.Methode!.Name}] {c}";
        if (s.Parameter.FirstOrDefault()?.Type is INamedTypeSymbol ein && _domänen.Contains(ein.ContainingAssembly?.Name ?? ""))
            foreach (var r in ein.DeclaringSyntaxReferences.Where(r => !Projektlage.IstGeneriert(r.SyntaxTree)).Take(1))
                foreach (var c in Zeilenkommentare(r.GetSyntax())) yield return $"[// vor {ein.Name}] {c}";
        if (DokuVon(s.Klasse) is { } kd) yield return $"[/// an {s.Klasse.Name}] {kd}";
    }

    private static IEnumerable<string> Zeilenkommentare(SyntaxNode n) => n.GetLeadingTrivia()
        .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
        .Select(t => t.ToString().TrimStart('/').Trim())
        .Where(t => t.Any(char.IsLetter));

    // ── Typen ──

    private IEnumerable<INamedTypeSymbol> Ausgänge(SlotInventar.SlotRef s)
    {
        var aus = Entfalte(s.Methode!.ReturnType).Where(IstDomäne).ToList();
        if (s.Art == "pipeline")
            foreach (var p in _dom.Pipelines.Where(p => p.Full == s.Klasse.Fq()))
                foreach (var h in p.Handles.Where(h => h.InputFull == s.Parameter[0].Type.Fq()))
                    foreach (var e in h.EmitsFull) if (Typ(e) is { } t) aus.Add(t);
        return aus.GroupBy(t => t.Fq()).Select(g => g.First());
    }

    /// <summary>Task&lt;T&gt;, IEnumerable&lt;T&gt;, IAsyncEnumerable&lt;T&gt;, OneOf&lt;…&gt; → die Element-Typen.</summary>
    private static IEnumerable<INamedTypeSymbol> Entfalte(ITypeSymbol t)
    {
        if (t is not INamedTypeSymbol n) yield break;
        if (Vertrag.IstOneOf(n)) { foreach (var a in n.TypeArguments) foreach (var x in Entfalte(a)) yield return x; yield break; }
        if (n.IsGenericType && (Vertrag.Ist(n, typeof(Task<>)) || Vertrag.Ist(n, typeof(IEnumerable<>)) || Vertrag.Ist(n, typeof(IAsyncEnumerable<>))))
        { foreach (var x in Entfalte(n.TypeArguments[0])) yield return x; yield break; }
        yield return n;
    }

    private IEnumerable<string> Typen(SlotInventar.SlotRef s)
    {
        var saat = new List<ITypeSymbol>();
        saat.AddRange(s.Parameter.Select(p => p.Type));
        saat.AddRange(Ausgänge(s));
        if (s.State != null) saat.Add(s.State);
        saat.AddRange(Felder(s).Select(f => TypVon(f)!));
        saat.AddRange(Helfer(s).SelectMany(h => h.Parameters.Select(p => p.Type).Append(h.ReturnType)));
        if (s.Art == "store" && StoreVon(s) is { } st)
            saat.AddRange(_dom.ReadModels.Where(r => r.Store == st.Name || r.StoreKandidaten?.Contains(st.Name) == true).Select(r => Typ(r.Full)).OfType<INamedTypeSymbol>());
        var ausgang = Ausgänge(s).Select(a => a.Fq()).ToHashSet(StringComparer.Ordinal);
        var eingang = s.Parameter.Take(1).Select(p => p.Type.Fq()).ToHashSet(StringComparer.Ordinal);
        foreach (var t in Transitiv(saat))
        {
            var rolle = t.TypeKind == TypeKind.Enum ? "Enum"
                : s.State != null && t.Fq() == s.State.Fq() ? "State"
                : Sym.Implements(t, _iTransient) ? "Ablehnung"
                : Sym.Implements(t, _iEvent) ? "Event"
                : Sym.Implements(t, _iCommand) ? "Command"
                : Sym.Implements(t, _iQuery) ? "Query"
                : Sym.Implements(t, _iResponse) ? "Antwort"
                : Sym.Implements(t, _iReadModel) ? "ReadModel"
                : Sym.Implements(t, _iWriteStore) || Sym.Implements(t, _iReadStore) ? "Store"
                : t.TypeKind == TypeKind.Interface ? "Vertrag" : "Typ";
            if (eingang.Contains(t.Fq())) rolle += ", Eingang";
            if (ausgang.Contains(t.Fq())) rolle += ", Ausgang";
            foreach (var zeile in Eintrag(t, rolle)) yield return zeile;
        }
    }

    private IEnumerable<INamedTypeSymbol> Transitiv(IEnumerable<ITypeSymbol> saat)
    {
        var schon = new HashSet<string>(StringComparer.Ordinal);
        var offen = new Queue<ITypeSymbol>(saat);
        while (offen.Count > 0)
        {
            var t = offen.Dequeue();
            if (t is IArrayTypeSymbol a) { offen.Enqueue(a.ElementType); continue; }
            if (t is not INamedTypeSymbol n) continue;
            foreach (var arg in n.TypeArguments) offen.Enqueue(arg);
            if (!IstDomäne(n) || !schon.Add(n.OriginalDefinition.Fq())) continue;
            yield return n;
            if (n.TypeKind == TypeKind.Enum) continue;
            foreach (var mm in n.GetMembers().Where(x => x.DeclaredAccessibility == Accessibility.Public && !x.IsImplicitlyDeclared))
            {
                if (TypVon(mm) is { } mt) offen.Enqueue(mt);
                if (mm is IMethodSymbol ms) foreach (var p in ms.Parameters) offen.Enqueue(p.Type);
            }
        }
    }

    private bool IstDomäne(INamedTypeSymbol t) => _domänen.Contains(t.ContainingAssembly?.Name ?? "")
        && t.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree));

    private IEnumerable<string> Eintrag(INamedTypeSymbol t, string rolle)
    {
        var doku = DokuVon(t) is { } d ? "   /// " + d : "";
        if (t.TypeKind == TypeKind.Enum)
        {
            yield return $"enum {t.Name} {{ {string.Join(", ", t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name))} }}   [{rolle}]{doku}";
            yield break;
        }
        var ctor = PrimärKonstruktor(t);
        var art = t.TypeKind == TypeKind.Interface ? "interface" : t.IsRecord ? "record" : "class";
        yield return (ctor != null ? $"{art} {t.Name}({string.Join(", ", ctor.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}"))})" : $"{art} {t.Name}")
                     + $"   [{rolle}]{doku}";
        var ctorNamen = ctor?.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal) ?? new();
        foreach (var mm in t.GetMembers().Where(x => (x.DeclaredAccessibility == Accessibility.Public || t.TypeKind == TypeKind.Interface)
                                                     && !x.IsImplicitlyDeclared
                                                     && x is IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }))
        {
            if (mm is IPropertySymbol pp && ctorNamen.Contains(pp.Name)) continue;
            yield return "    " + MemberZeile(mm);   // auch generierte Member (z. B. Id/Version des States)
        }
    }

    private static IMethodSymbol? PrimärKonstruktor(INamedTypeSymbol t) => t.InstanceConstructors
        .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0
                    && !(c.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, t)))
        .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();

    private static string MemberZeile(ISymbol m)
    {
        var zeile = Sig(m);
        if (Ausdruck(m) is { } e) zeile += " => " + e;
        return DokuVon(m) is { } d ? $"{zeile}   /// {d}" : zeile;
    }

    private static string Sig(ISymbol m) => (m.IsStatic ? "static " : "") + m switch
    {
        IPropertySymbol p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}{(Ausdruck(p) != null ? "" : p.SetMethod == null ? " { get; }" : p.SetMethod.IsInitOnly ? " { get; init; }" : " { get; set; }")}",
        IMethodSymbol ms => $"{ms.ReturnType.ToDisplayString(Kurz)} {ms.Name}{(ms.TypeParameters.Length > 0 ? "<" + string.Join(", ", ms.TypeParameters.Select(t => t.Name)) + ">" : "")}({string.Join(", ", ms.Parameters.Select(x => $"{x.Type.ToDisplayString(Kurz)} {x.Name}"))})",
        _ => m.Name,
    };

    private static string? Ausdruck(ISymbol m)
    {
        var expr = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() switch
        {
            PropertyDeclarationSyntax p => p.ExpressionBody?.Expression,
            MethodDeclarationSyntax md => md.ExpressionBody?.Expression,
            _ => null,
        };
        return expr == null ? null : Regex.Replace(expr.ToString(), @"\s+", " ");
    }

    // ── Graph-Umfeld je Slot-Art ──

    private IEnumerable<string> Umfeld(SlotInventar.SlotRef s)
    {
        var disc = s.Parameter.FirstOrDefault()?.Type!;   // Store-Funktionen dürfen parameterlos sein (disc dort unbenutzt)
        switch (s.Art)
        {
            case "decide":
            {
                var cmd = Knoten(NodeKind.command, disc);
                var quellen = new List<string>();
                if (cmd != null)
                {
                    if (cmd.Command!.Origin.Contains("client")) quellen.Add("Client");
                    foreach (var e in _graph.Edges.Where(e => e.To == cmd.Id))
                    {
                        var von = _graph.Nodes.FirstOrDefault(n => n.Id == e.From);
                        if (e.Kind is EdgeKind.sends or EdgeKind.compensates && von?.Process is { } pi && int.TryParse(e.Via, out var ri) && ri < pi.Rules.Count)
                            quellen.Add($"Prozess {von.Name} Regel {ri} ({(e.Kind == EdgeKind.compensates ? "Kompensation" : "wenn " + string.Join(" + ", pi.Rules[ri].When))})");
                        else if (e.Kind == EdgeKind.pipelineEmits && von != null) quellen.Add($"Pipeline {von.Name}");
                    }
                }
                foreach (var f in _cr.Frists.Where(f => f.Sendet == disc.Name))
                    quellen.Add($"Frist {f.Name} (fällig; geplant bei {string.Join(", ", f.Plant)}, storniert bei {string.Join(", ", f.Storniert)})");
                yield return $"{disc.Name} kommt von: {(quellen.Count == 0 ? "—" : string.Join(" · ", quellen.Distinct()))}";
                if (cmd?.Command?.IsCreation == true) yield return $"{disc.Name} ist Erzeugungs-Command (ICreationCommand)";
                foreach (var o in Ausgänge(s)) yield return $"{o.Name} geht an: {Abnehmer(o, s.State)}";
                break;
            }
            case "apply":
            {
                var evt = Knoten(NodeKind.@event, disc);
                var erzeuger = evt == null ? new List<string>() : _graph.Nodes.Where(n => n.Kind == NodeKind.command && n.Command!.Produces.Any(p => p.Event == evt.Name))
                    .Select(n => { var g = n.Command!.Produces.First(p => p.Event == evt.Name).Guard; return $"Decide({n.Name}){(g != null ? $" wenn `{g}`" : " ohne umschließende Bedingung")}"; }).ToList();
                yield return $"{disc.Name} erzeugt von: {(erzeuger.Count == 0 ? "—" : string.Join(" · ", erzeuger))}";
                yield return $"{disc.Name} geht außerdem an: {Abnehmer(disc as INamedTypeSymbol, s.State, ohneApply: true)}";
                break;
            }
            case "projektion":
            case "reaktion":
            {
                var pr = _dom.Projections.FirstOrDefault(p => p.Full == s.Klasse.Fq());
                var calls = pr?.HandleStoreCalls.GetValueOrDefault(disc.Name) ?? new();
                yield return $"Trigger-Event: {disc.Name} (erzeugt von Aggregat {string.Join(", ", ErzeugerAggregate(disc))})";
                yield return $"Store-Aufrufe (verdrahtet): {(calls.Count == 0 ? "—" : string.Join(", ", calls.Select(c => $"{c.Store}.{c.Method}")))}";
                foreach (var c in calls) if (FnSig(c.Store, c.Method) is { } sig) yield return $"    {sig}";
                foreach (var st in calls.Select(c => c.Store).Distinct())
                    yield return $"ReadModels von {st}: {string.Join(", ", ReadModelsVon(st))}";
                if (pr?.HandleSends.GetValueOrDefault(disc.Name) is { Count: > 0 } sends) yield return $"sendet: {string.Join(", ", sends)}";
                if (pr?.HandlePublishes.GetValueOrDefault(disc.Name) is { Count: > 0 } pubs) yield return $"veröffentlicht (reaktiv, verlierbar): {string.Join(", ", pubs)}";
                break;
            }
            case "reader":
            {
                var rd = _dom.Readers.FirstOrDefault(r => r.Full == s.Klasse.Fq());
                var calls = rd?.HandleStoreCalls.GetValueOrDefault(disc.Name) ?? new();
                yield return $"liest Projektion: {rd?.ProjectionName ?? "—"}";
                yield return $"Store-Aufrufe (verdrahtet): {(calls.Count == 0 ? "—" : string.Join(", ", calls.Select(c => $"{c.Store}.{c.Method}")))}";
                foreach (var c in calls) if (FnSig(c.Store, c.Method) is { } sig) yield return $"    {sig}";
                foreach (var st in calls.Select(c => c.Store).Distinct())
                    yield return $"ReadModels von {st}: {string.Join(", ", ReadModelsVon(st))}";
                break;
            }
            case "pipeline":
            {
                var pl = _dom.Pipelines.FirstOrDefault(p => p.Full == s.Klasse.Fq());
                var h = pl?.Handles.FirstOrDefault(x => x.InputFull == disc.Fq());
                var quelle = _dom.Aggregates.Where(a => a.DecideOutcomes.Values.Any(o => o.Contains(disc.Fq()))).Select(a => "Aggregat " + a.Name).ToList();
                yield return $"Eingang {disc.Name} kommt von: {(quelle.Count == 0 ? "Trigger/Self (siehe Skelett)" : string.Join(", ", quelle))}";
                yield return $"sendet: {(h?.EmitsFull.Count > 0 ? string.Join(", ", h.Value.EmitsFull.Select(e => e.Split('.').Last())) : "—")}";
                if (pl?.HandleEmitsTriggers.GetValueOrDefault(disc.Fq()) is { Count: > 0 } tr) yield return $"erzeugt Trigger: {string.Join(", ", tr)}";
                if (pl?.HandleSchedules.GetValueOrDefault(disc.Fq()) is { Count: > 0 } sc) yield return $"plant Self-Tick: {string.Join(", ", sc.Select(x => $"{x.Name} nach {x.Delay}"))}";
                foreach (var ziel in h?.EmitsFull ?? new())
                    if (_dom.Aggregates.FirstOrDefault(a => a.HandlesCommandsFull.Contains(ziel)) is { } agg)
                        yield return $"{ziel.Split('.').Last()} geht an: Aggregat {agg.Name}";
                break;
            }
            case "store":
            {
                var st = StoreVon(s);
                if (st == null) break;
                var fn = st.Fns.First(f => f.Name == s.Disc);
                yield return $"Store {st.Name}: {(fn.IsRead ? "Lese" : "Schreib")}-Funktion {s.Disc}";
                yield return $"ReadModels: {string.Join(", ", ReadModelsVon(st.Name))}";
                var aufrufer = Aufrufer(st.Name, s.Disc).Select(x => x.Titel).ToList();
                yield return $"aufgerufen von: {(aufrufer.Count == 0 ? "—" : string.Join(", ", aufrufer))}";
                foreach (var p in _dom.Projections.Where(p => p.HandleStoreCalls.Values.Any(cs => cs.Any(c => c.Store == st.Name && c.Method == s.Disc))))
                    yield return $"    {p.Name}: {(p.Append ? "append-artig (exactly-once)" : "Upsert (at-least-once)")}";
                if (st.MehrdeutigeImpls.Count > 0) yield return $"mehrere Implementierungen: {string.Join(", ", st.MehrdeutigeImpls.Select(x => x.Split('.').Last()))}";
                break;
            }
        }
    }

    private IEnumerable<string> ErzeugerAggregate(ITypeSymbol evt) =>
        _dom.Aggregates.Where(a => a.DecideOutcomes.Values.Any(o => o.Contains(evt.Fq()))).Select(a => a.Name).DefaultIfEmpty("—");

    private string? FnSig(string store, string method)
    {
        var fn = _dom.Stores.FirstOrDefault(s => s.Name == store)?.Fns.FirstOrDefault(f => f.Name == method);
        return fn == null ? null : $"{method}({string.Join(", ", fn.Params.Select(p => $"{p.Type} {p.Name}"))}){(fn.Return != null ? " -> " + fn.Return : "")}";
    }

    private IEnumerable<string> ReadModelsVon(string store) =>
        _dom.ReadModels.Where(r => r.Store == store || r.StoreKandidaten?.Contains(store) == true).Select(r => r.Name).DefaultIfEmpty("—");

    private Node? Knoten(NodeKind kind, ITypeSymbol t) =>
        _graph.Nodes.FirstOrDefault(n => n.Kind == kind && n.FullName == t.Fq()) ?? _graph.Nodes.FirstOrDefault(n => n.Kind == kind && n.Name == t.Name);

    private string Abnehmer(INamedTypeSymbol? evt, INamedTypeSymbol? state, bool ohneApply = false)
    {
        if (evt == null) return "—";
        if (Sym.Implements(evt, _iTransient)) return "Aufrufer (Ablehnung, nicht im Log)";
        var ziele = new List<string>();
        if (!ohneApply && state != null)
        {
            var ap = _inv.Slots.FirstOrDefault(x => x.Art == "apply" && x.State?.Fq() == state.Fq() && x.Parameter[0].Type.Fq() == evt.Fq());
            ziele.Add(ap == null ? $"Apply({evt.Name}) FEHLT" : $"Apply({evt.Name}) [{RumpfStatus(ap, Model(ap.Rumpf.SyntaxTree))}]");
        }
        if (_graph.Views.EventFanout.FirstOrDefault(f => f.Event == evt.Name) is { } fo)
        {
            ziele.AddRange(fo.Projections.Select(p => $"Projektion {p}"));
            ziele.AddRange(fo.TriggersProcesses.Select(p => $"Prozess {p} (Auslöser)"));
            ziele.AddRange(fo.AdvancesProcesses.Select(p => $"Prozess {p} (Bedingung)"));
            ziele.AddRange(fo.Pipelines.Select(p => $"Pipeline {p}"));
        }
        return ziele.Count == 0 ? "—" : string.Join(" · ", ziele);
    }

    // ── Nachbar-Code-Blöcke: ALLE anderen Code-Blöcke derselben Klasse (Slots + Helfer) ──

    private IEnumerable<(string Titel, string Text)> Nachbarn(SlotInventar.SlotRef s)
    {
        foreach (var n in _inv.Slots.Where(x => x != s && x.Methode != null && x.Klasse.Fq() == s.Klasse.Fq() && Arten.Contains(x.Art)))
            yield return (Deklaration(n.Methode!), RumpfText(n.Rumpf));
        foreach (var h in Helfer(s))
            if (h.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } md
                && ((SyntaxNode?)md.Body ?? md.ExpressionBody) is { } body)
                yield return ("Helfer " + Deklaration(h), RumpfText(body));
    }

    private string ArtgenossenHinweis(SlotInventar.SlotRef s)
    {
        var andere = _inv.Slots.Where(x => x.Art == s.Art && x.Klasse.Fq() != s.Klasse.Fq()).Select(x => x.Klasse.Name).Distinct().ToList();
        return andere.Count == 0
            ? $"keine — und kein anderer {s.Art}-Code-Block im System"
            : $"keine in der Klasse — {s.Art}-Code-Blöcke gibt es in: {string.Join(", ", andere)}";
    }

    // ── Kopplung: Rümpfe gekoppelter Code-Blöcke (über Graph-Kanten) ──

    private IEnumerable<(string Titel, string Text)> Kopplung(SlotInventar.SlotRef s)
    {
        var disc = s.Parameter.FirstOrDefault()?.Type!;   // Store-Funktionen dürfen parameterlos sein (disc dort unbenutzt)
        switch (s.Art)
        {
            case "decide":   // persistente Ausgänge → deren Apply
                foreach (var o in Ausgänge(s).Where(o => !Sym.Implements(o, _iTransient)))
                    foreach (var ap in _inv.Slots.Where(x => x.Art == "apply" && x.State?.Fq() == s.State?.Fq() && x.Parameter[0].Type.Fq() == o.Fq()))
                        yield return ($"Apply({o.Name}) — wendet den Ausgang {o.Name} an", RumpfText(ap.Rumpf));
                break;
            case "apply":    // erzeugende Decide
                foreach (var d in _inv.Slots.Where(x => x.Art == "decide" && x.State?.Fq() == s.State?.Fq()
                                                         && Ausgänge(x).Any(o => o.Fq() == disc.Fq())))
                    yield return ($"Decide({d.Disc}) — erzeugt {disc.Name}", RumpfText(d.Rumpf));
                break;
            case "projektion":
            case "reaktion":
            case "reader":
            {
                var calls = s.Art == "reader"
                    ? _dom.Readers.FirstOrDefault(r => r.Full == s.Klasse.Fq())?.HandleStoreCalls.GetValueOrDefault(disc.Name)
                    : _dom.Projections.FirstOrDefault(p => p.Full == s.Klasse.Fq())?.HandleStoreCalls.GetValueOrDefault(disc.Name);
                foreach (var c in calls ?? new())
                    foreach (var impl in _inv.Slots.Where(x => x.Art == "store" && x.Disc == c.Method && StoreVon(x)?.Name == c.Store))
                        yield return ($"Store-Impl {impl.Klasse.Name}.{c.Method}", RumpfText(impl.Rumpf));
                break;
            }
            case "store":
                if (StoreVon(s) is { } st)
                    foreach (var a in Aufrufer(st.Name, s.Disc))
                        yield return ($"{a.Titel} — ruft {s.Disc}", RumpfText(a.Rumpf));
                break;
        }
    }

    private IEnumerable<(string Titel, SyntaxNode Rumpf)> Aufrufer(string store, string method)
    {
        foreach (var p in _dom.Projections)
            foreach (var (evt, calls) in p.HandleStoreCalls)
                if (calls.Any(c => c.Store == store && c.Method == method)
                    && _inv.Slots.FirstOrDefault(x => x.Klasse.Fq() == p.Full && x.Disc == evt) is { } sl)
                    yield return ($"{p.Name}.Handle({evt})", sl.Rumpf);
        foreach (var r in _dom.Readers)
            foreach (var (q, calls) in r.HandleStoreCalls)
                if (calls.Any(c => c.Store == store && c.Method == method)
                    && _inv.Slots.FirstOrDefault(x => x.Klasse.Fq() == r.Full && x.Disc == q) is { } sl)
                    yield return ($"{r.Name}.Handle({q})", sl.Rumpf);
    }

    // ── Hilfen ──

    private string RumpfStatus(SlotInventar.SlotRef s, SemanticModel model)
    {
        if (s.Rumpf is ArrowExpressionClauseSyntax) return "geschrieben";
        if (s.Rumpf is not BlockSyntax b) return "geschrieben";
        if (b.DescendantNodes().OfType<ThrowStatementSyntax>().Any(t => t.Expression != null
                && Vertrag.Ist(model.GetTypeInfo(t.Expression).Type, typeof(NotImplementedException))))
            return "fehlt (Platzhalter)";
        return b.Statements.Count > 0 ? "geschrieben" : "leer";
    }

    private static string? PromptAus(SlotInventar.SlotRef s) =>
        s.Rumpf.DescendantTrivia().Select(t => t.ToString().Trim()).FirstOrDefault(t => t.StartsWith(PromptMarke))?[PromptMarke.Length..].Trim();

    private static string Deklaration(IMethodSymbol m) =>
        $"{m.ReturnType.ToDisplayString(Kurz)} {m.Name}({string.Join(", ", m.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}"))})";

    private static string RumpfText(SyntaxNode rumpf)
    {
        if (rumpf is ArrowExpressionClauseSyntax a) return "=> " + a.Expression + ";";
        var text = rumpf.ToString().Replace("\r\n", "\n");
        var zeilen = text.Split('\n').Where(l => !l.Contains(PromptMarke)).ToList();
        var min = zeilen.Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select((l, i) => i == 0 ? l.Trim() : (l.Length >= min ? l[min..] : l.TrimStart()).TrimEnd()));
    }

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

    private static ITypeSymbol? TypVon(ISymbol m) => m switch
    {
        IFieldSymbol f => f.Type, IPropertySymbol p => p.Type, IMethodSymbol mm => mm.ReturnType, _ => null,
    };

    private static bool IstHand(ISymbol m) => m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree));

    private INamedTypeSymbol? Typ(string full) => _comps.Select(c => c.GetTypeByMetadataName(full)).FirstOrDefault(x => x != null);

    private SemanticModel Model(SyntaxTree tree) => _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    private string Rel(string pfad)
    {
        var wurzel = _dom.Wurzel;
        return string.IsNullOrEmpty(wurzel) ? pfad : Path.GetRelativePath(wurzel, pfad);
    }
}

/// <summary>
/// CLI: <c>--kontext &lt;Disc|Besitzer.Disc&gt; [--auftrag "…"]</c> gibt Skelett-Größe + Slot-Teil aus;
/// <c>--kontexte &lt;verz&gt;</c> schreibt <c>00-graph-skelett.txt</c>, je Slot eine Datei und <c>übersicht.md</c>.
/// </summary>
public static class KontextCli
{
    public static int Lauf(string[] args, Projektlage lage, DomainModel dom, KnowledgeGraph graph, CompositionRoot cr, string solutionDir)
    {
        string? Wert(string flag) { var i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null; }
        var board = Path.Combine(solutionDir, "board-model.json");
        var bauer = new KontextBauer(lage, dom, graph, cr, board);
        Console.WriteLine(File.Exists(board) ? $"   Board: {board} (LLM-Knoten werden gelesen)" : "   Board: keins (board-model.json fehlt) — Auftrag nur aus „// 🤖 Prompt:“ oder --auftrag");

        var ziel = Wert("--kontext");
        if (ziel != null)
        {
            var ks = bauer.Baue(ziel, Wert("--auftrag"));
            if (ks.Count == 0) { Console.Error.WriteLine($"❌ Kein Code-Block für '{ziel}'."); return 1; }
            foreach (var k in ks)
            {
                var skel = bauer.Skelett(k.Schlüssel);
                Console.WriteLine($"\n════ {k.Titel} — fester Teil ≈ {Token(skel)} Token (Graph-Skelett, siehe --kontexte), Slot-Teil ≈ {Token(k.Text)} Token ════\n");
                Console.WriteLine(k.Text);
            }
            return 0;
        }

        var verz = Wert("--kontexte") ?? throw new ArgumentException("--kontexte <verz>");
        Directory.CreateDirectory(verz);
        var skelett = bauer.Skelett();
        File.WriteAllText(Path.Combine(verz, "00-graph-skelett.txt"), skelett);
        var alle = bauer.Baue(null, null);
        var md = new StringBuilder();
        md.AppendLine("| Slot | Rumpf | Auftrag | Slot-Teil ≈Tok | Nachbar-Blöcke | Kopplung-Blöcke | Aufruf gesamt ≈Tok |");
        md.AppendLine("|---|---|---|---:|---:|---:|---:|");
        var skelTok = Token(skelett);
        foreach (var k in alle)
        {
            var impl = k.Slot.Art == "store" ? "@" + k.Slot.Klasse.Name : "";   // ein Store-Interface kann mehrere Implementierungen haben
            File.WriteAllText(Path.Combine(verz, $"{k.Slot.Art}-{k.Schlüssel.Split('|')[1]}.{k.Slot.Disc}{impl}.txt"), k.Text);
            md.AppendLine($"| {k.Titel} | {k.RumpfStatus} | {(k.Auftrag == null ? "—" : "✓")} | {Token(k.Text)} | {k.NachbarBlöcke} | {k.KopplungBlöcke} | {skelTok + Token(k.Text)} |");
        }
        File.WriteAllText(Path.Combine(verz, "übersicht.md"), md.ToString());
        Console.WriteLine($"✅ {alle.Count} Slot-Teile + Graph-Skelett (≈ {skelTok} Token) → {verz}");
        foreach (var g in alle.GroupBy(k => k.Slot.Art))
        {
            var t = g.Select(k => Token(k.Text)).OrderBy(x => x).ToList();
            Console.WriteLine($"   {g.Key,-11} {g.Count(),3} Slots   Slot-Teil Median ≈ {t[t.Count / 2],5}   Max ≈ {t[^1],5}   (+ Skelett ≈ {skelTok})");
        }
        return 0;
    }

    /// <summary>Zeichen / 3,3 — gegen den Qwen-BPE kalibriert (siehe Konzept).</summary>
    public static int Token(string text) => (int)Math.Ceiling(text.Length / 3.3);
}
