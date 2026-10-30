using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DomainEditor;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  LLM-Kontext je Code-Block (LLM-Minimalkontext, Konzept im Git-Verlauf bei 861ea7f)
//
//  Ein Aufruf = FESTER TEIL (Graph-Skelett, für alle Slots gleich) + SLOT-TEIL (je Code-Block). Alles aus Code-Fakten:
//  Roslyn-Symbole [S], Graph-/Editor-Kanten [K], Nachbar-Rümpfe [N]. Einzige menschliche Eingabe ist der Auftrag [L] —
//  aus dem LLM-Knoten im Board (intent → promptZiel → Code-Block), aus der „// 🤖 Prompt:“-Zeile im Rumpf oder per CLI.
//  Der EIGENE Rumpf fließt nie ein. Das Skelett trägt nur Signatur-Fakten (keine Rumpf-Guards).
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
    public const string PromptMarke = "// 🤖 Prompt:";   // Spiegel von Werkbank.CodeSync.PromptMarke

    private static readonly SymbolDisplayFormat Kurz = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                              | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly string[] Arten = { "decide", "apply", "projektion", "reader", "store" };

    private readonly SlotInventar _inv;
    private readonly DomainModel _dom;
    private readonly KnowledgeGraph _graph;
    private readonly JsonElement _board;        // Editor-Modell (ModellMapper.ZuBoardJson) — Quelle des Skeletts
    private readonly List<Compilation> _comps;
    private readonly HashSet<string> _domänen, _framework;
    private readonly INamedTypeSymbol? _iTransient, _iWriteStore, _iReadStore, _iEvent, _iCommand, _iQuery, _iReadModel, _iResponse;
    private readonly Dictionary<string, (string Intent, string Quelle)> _llmKnoten;

    /// <summary>Welche Syntaxbäume handgeschrieben sind (je Solution-Stand).</summary>
    private readonly Quelltexte _quelltexte;

    public KontextBauer(Projektlage lage, DomainModel dom, KnowledgeGraph graph, CompositionRoot cr, string? boardDatei)
    {
        _quelltexte = lage.Quelltexte;
        _inv = new SlotInventar(lage, dom);
        _dom = dom; _graph = graph;
        _board = JsonDocument.Parse(ModellMapper.ZuBoardJson(graph, dom, cr)).RootElement;
        _comps = lage.Compilations;
        _domänen = new HashSet<string>(lage.DomänenAssemblies, StringComparer.Ordinal);
        _framework = lage.Analyse.Select(a => a.Compilation.AssemblyName ?? "").Where(n => !_domänen.Contains(n)).ToHashSet(StringComparer.Ordinal);
        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iTransient = Get(Vertrag.ITransientEvent); _iWriteStore = Get(Vertrag.IWriteStore); _iReadStore = Get(Vertrag.IReadStore);
        _iEvent = Get(Vertrag.IEvent); _iCommand = Get(Vertrag.ICommand);
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
        foreach (var r in A(b, "reader")) foreach (var h in A(r, "handles")) Merke(h, $"reader|{S(r, "name")}|{S(h, "query")}");
        foreach (var st in A(b, "stores"))
            foreach (var f in A(st, "writeFns").Concat(A(st, "readFns"))) Merke(f, $"store|{S(st, "name")}|{S(f, "name")}");
        foreach (var c in A(b, "clients"))
        {
            var n = S(c, "name") ?? ""; var client = n.Length > 1 && n[0] == 'I' && char.IsUpper(n[1]) ? n[1..] : n;
            foreach (var i in A(c, "implementierungen")) foreach (var r in A(i, "rumpfe")) Merke(r, $"zusage|{client}|{S(r, "event")}@{S(i, "klasse")}");
        }
        foreach (var l in A(b, "llmNodes"))
            if (S(l, "promptZiel") is { } z && code.TryGetValue(z, out var schl) && !string.IsNullOrWhiteSpace(S(l, "intent")))
                erg[schl] = (S(l, "intent")!, $"LLM-Knoten „{S(l, "name")}“ im Board");
        return erg;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FESTER TEIL: das Graph-Skelett (aus dem Editor-Modell — Signaturen + Kanten, keine Rümpfe)
    // ════════════════════════════════════════════════════════════════════════

    public string Skelett()
    {
        var b = new StringBuilder();
        string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        bool Bo(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
        IEnumerable<JsonElement> A(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();
        IEnumerable<string> Str(JsonElement e, string p) => A(e, p).Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!);
        string F(JsonElement e, string p = "felder") => string.Join(", ", A(e, p).Select(f => $"{S(f, "name")}:{S(f, "typ")}"));

        var rec = A(_board, "records").ToDictionary(r => S(r, "name")!, r => r, StringComparer.Ordinal);
        b.AppendLine("# GRAPH-SKELETT der Domäne (Signaturen und Kanten — ohne Rümpfe)");
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
                var outs = string.Join(" | ", A(d, "ergibt").Select(o => S(o, "event")));
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
        foreach (var f in A(_board, "bausteinFunktionen"))
            b.AppendLine($"FUNKTION {S(f, "name")} ({(Bo(f, "klasse") ? "im Host" : "draußen")}): " + string.Join("; ", A(f, "eingaenge").Select(e =>
                $"{S(e, "eingang")} -> {(Bo(e, "strom") ? "Strom " : "")}{string.Join(" | ", Str(e, "ausgaben"))}")));
        var fnName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var st in A(_board, "stores"))
        {
            var fns = A(st, "writeFns").Concat(A(st, "readFns")).ToList();
            foreach (var f in fns) if (S(f, "_id") is { } id) fnName[id] = $"{S(st, "name")}.{S(f, "name")}";
            b.AppendLine($"STORE {S(st, "name")}: " + string.Join("; ", fns.Select(f =>
                $"{S(f, "name")}({string.Join(", ", A(f, "params").Select(p => $"{S(p, "name")}:{S(p, "typ")}"))}){(S(f, "rueckgabe") is { } r ? " -> " + r : "")}")));
        }
        foreach (var rm in A(_board, "readModels")) b.AppendLine($"readmodel {S(rm, "name")}({F(rm)}) @ {S(rm, "store")}");
        foreach (var p in A(_board, "projektionen"))
            b.AppendLine($"PROJEKTION {S(p, "name")}{(Bo(p, "mindestensEinmal") ? " [MindestensEinmal]" : "")}: " + string.Join("; ", A(p, "handles").Select(h =>
                $"{S(h, "event")} -> {string.Join(", ", Str(h, "fns").Select(x => fnName.GetValueOrDefault(x, x)))}")));
        foreach (var r in A(_board, "reader"))
            b.AppendLine($"READER {S(r, "name")} liest {string.Join(", ", Str(r, "projektionen"))}: " + string.Join("; ", A(r, "handles").Select(h =>
                $"{S(h, "query")} -> {string.Join(", ", Str(h, "fns").Select(x => fnName.GetValueOrDefault(x, x)))} => {string.Join(" | ", Str(h, "responses"))}")));
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

    // ── Getragene Funktion bzw. Kenntnis eines Clients DRAUSSEN (docs/konzept-akteure.md §4): der Python-Rumpf einer Client-Implementierung ──

    public sealed record PythonSlot(string Id, string Titel, string Text, string Client, string Event, string Klasse, string Methode,
        string? Signatur, IReadOnlyList<string> Ausgaenge, string Datei, int Zeile, bool Fehlt, string? Auftrag, string SlotDatei);

    /// <summary>
    /// Je Client-Implementierung (Klasse, die die generierte Basis erbt) und getragenem Funktions-Eingang bzw. Kenntnis ein Slot: Signatur aus
    /// der Basis, Ausgabe-Vertrag, erreichbar (Eingangs-DTO, Herkunft bzw. ctx, state, self), Typen der Domäne, als Nachbarn die übrigen
    /// Methoden der Klasse (wörtlich). Die Slot-Art heißt im Protokoll mit dem Editor weiter <c>zusage</c>.
    /// </summary>
    public IEnumerable<PythonSlot> PythonSlots(string solutionDir)
    {
        string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        IEnumerable<JsonElement> A(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();
        var rec = A(_board, "records").Where(r => S(r, "name") != null).GroupBy(r => S(r, "name")!).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        string Snake(string n) => Regex.Replace(n, "(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", "_$1$2").ToLowerInvariant();
        string Typzeile(string name, string rolle)
        {
            if (!rec.TryGetValue(name, out var r)) return $"{name}   [{rolle}]   (unbekannt)";
            var felder = A(r, "felder").Select(f => $"{S(f, "name")}: {S(f, "typ")}  → .{Snake(S(f, "name") ?? "")}").ToList();
            var doku = S(r, "doku");
            return $"{S(r, "kind")} {name}Dto   [{rolle}]   Felder: {(felder.Count == 0 ? "—" : string.Join(" · ", felder))}{(doku is { Length: > 0 } ? $"   /// {Regex.Replace(doku, @"\s+", " ")}" : "")}";
        }
        var ctxFelder = MessageContextFelder(solutionDir);
        foreach (var c in A(_board, "clients"))
        {
            var iname = S(c, "name") ?? "";
            var client = iname.Length > 1 && iname[0] == 'I' && char.IsUpper(iname[1]) ? iname[1..] : iname;
            foreach (var impl in A(c, "implementierungen"))
            {
                var datei = S(impl, "datei") ?? ""; var klasse = S(impl, "klasse") ?? "";
                var voll = Path.Combine(solutionDir, datei);
                var text = File.Exists(voll) ? File.ReadAllText(voll) : "";
                foreach (var r in A(impl, "rumpfe"))
                {
                    var ev = S(r, "event") ?? ""; var methode = S(r, "methode") ?? "";
                    var ausg = A(r, "ausgaenge").Select(x => x.GetString() ?? "").ToList();
                    var sig = S(r, "signatur");
                    var m = PythonQuelle.FindeMethode(text, klasse, methode);
                    // Die Parameter-NAMEN gelten, wie die Implementierung sie schreibt (z. B. „event“ statt „e“); fehlt die Methode, die der Basis.
                    var zeilenImpl = text.Replace("\r\n", "\n").Split('\n');
                    var defImpl = m == null ? sig : string.Join(" ", zeilenImpl.Skip(m.DefZeile).Take(m.RumpfStart - m.DefZeile).Select(x => x.Trim()));
                    var pn = Regex.Match(defImpl ?? "", @"def\s+\w+\s*\((.*)\)").Groups[1].Value.Split(',').Select(x => x.Split(':')[0].Trim()).Where(x => x.Length > 0 && x != "self").ToList();
                    string P(int i, string std) => i < pn.Count ? pn[i] : std;
                    var id = $"zusage|{client}|{ev}@{klasse}";
                    var titel = $"zusage {client}.{ev} @ {klasse}";
                    var auftrag = _llmKnoten.TryGetValue(id, out var llm) ? llm.Intent : null;
                    var b = new StringBuilder();
                    void Kopf(string t) { b.AppendLine(); b.AppendLine("## " + t); }
                    b.AppendLine($"# SLOT-TEIL {titel}");
                    Kopf("AUFTRAG [L]");
                    b.AppendLine(auftrag ?? "— kein LLM-Knoten an diesem Code-Block (ohne Auftrag wird kein Aufruf ausgelöst)");
                    Kopf("ANKER [S]");
                    var funktion = S(r, "funktion");
                    b.AppendLine($"{klasse}.{methode}   {datei}:{(m == null ? "— (Methode fehlt, wird mit der Signatur der Basis angelegt)" : (m.DefZeile + 1).ToString())}   Implementierung des Clients {client}{(funktion is { } fn ? $", trägt die Funktion {fn}" : "")}");
                    Kopf("SIGNATUR [S]");
                    b.AppendLine(defImpl ?? $"async def {methode}(self, e, ctx, state):");
                    if (m != null && sig != null && defImpl != sig) b.AppendLine($"(Basis: {sig})");
                    Kopf("VERTRAG (nur Erzwungenes) [S]");
                    b.AppendLine(funktion == null
                        ? "Kenntnis: keine Ausgabe — Framework (die generierte Basis prüft jede Rückgabe gegen den Vertrag)"
                        : $"Funktion {funktion}: liefert nur {string.Join(" | ", ausg.Select(x => x + "Dto"))} — Framework (Basis prüft jede Ausgabe gegen die Signatur der Funktion)");
                    if (funktion != null) b.AppendLine("Festgehalten und gesendet wird beim Funktions-Actor des Servers; die erste Antwort gewinnt");
                    Kopf("ERREICHBAR IM RUMPF [S]");
                    b.AppendLine($"Parameter  {P(0, "e")} : {ev}Dto   (der Eingang — Felder in snake_case, siehe TYPEN)");
                    b.AppendLine(funktion == null
                        ? $"Parameter  {P(1, "ctx")} : MessageContext   ({(ctxFelder.Count == 0 ? "Metadaten des Envelopes" : string.Join(", ", ctxFelder))})"
                        : $"Parameter  {P(1, "h")} : Herkunft   (Stream, Version, Zeit, Akteur des Eingangs)");
                    b.AppendLine($"Parameter  {P(2, "state")} : der Zustand der Implementierung (siehe NACHBAR-CODE)");
                    b.AppendLine("self       : die Implementierung (Felder und Hilfsmethoden siehe NACHBAR-CODE)");
                    Kopf("TYPEN (Domänen-Records als DTO; /// = Doku-Kommentar) [S]");
                    b.AppendLine(Typzeile(ev, "Eingang"));
                    foreach (var a in ausg) b.AppendLine(Typzeile(a, "Ausgabe"));
                    Kopf($"NACHBAR-CODE: {Path.GetFileName(datei)} ohne diesen Rumpf [N]");
                    var z = text.Replace("\r\n", "\n").Split('\n').ToList();
                    if (m != null) { z.RemoveRange(m.RumpfStart, m.RumpfEnde - m.RumpfStart); z.Insert(m.RumpfStart, "        ...  # ◄ dieser Rumpf"); }
                    b.AppendLine(string.Join("\n", z.Take(400)));
                    yield return new PythonSlot(id, titel, b.ToString().TrimStart('\n'), client, ev, klasse, methode, sig, ausg, datei,
                        m == null ? 0 : m.DefZeile + 1, m == null, auftrag, Regex.Replace($"zusage-{client}.{ev}@{klasse}", @"[^\w.@-]", "_") + ".txt");
                }
            }
        }
    }

    /// <summary>Die öffentlichen Felder von <c>MessageContext</c> aus dem Python-SDK (Code-Fakt: die Dataclass), sonst leer.</summary>
    private static List<string> MessageContextFelder(string solutionDir)
    {
        foreach (var f in Directory.EnumerateFiles(solutionDir, "router.py", SearchOption.AllDirectories).Where(p => !p.Contains("site-packages") && !p.Contains("/.")))
        {
            var t = File.ReadAllText(f).Replace("\r\n", "\n").Split('\n');
            var i = Array.FindIndex(t, z => z.StartsWith("class MessageContext"));
            if (i < 0) continue;
            return t.Skip(i + 1).TakeWhile(z => z.Length == 0 || z.StartsWith(' ')).Select(z => Regex.Match(z, @"^    ([a-z]\w*)\s*:")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
        }
        return [];
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
                yield return "Rückgabe Task, keine Nachricht; schreibt nur über die Schreib-Fähigkeiten genau eines Stores — Analyzer CQRS057, Generator CQRS052";
                yield return "rechnet bei gleicher Eingabe gleich: keine Uhr, kein Zufall, kein Lesen fremder Daten; Zeit aus der Herkunft — Analyzer CQRS102";
                var pr = _dom.Projections.FirstOrDefault(p => p.Full == s.Klasse.Fq());
                foreach (var k in (pr?.HandleFaehigkeiten.Values.SelectMany(cs => cs.Select(c => c.Store)) ?? []).Distinct()
                             .Select(st => _dom.Stores.FirstOrDefault(x => x.Name == st)).OfType<StoreRaw>().SelectMany(st => st.Klassen).Distinct())
                    yield return k switch
                    {
                        1 => "Store der Klasse 1 (atomar): Wirkungen und Fortschritt in einer Transaktion — jedes Event wirkt genau einmal, in Reihenfolge je Stream",
                        2 => "Store der Klasse 2 (wiederholungsfest): eine Wiederholung muss dasselbe ergeben — Event-Identität aus der Herkunft im Schlüssel oder als Bedingung",
                        _ => "Store der Klasse 3 (mindestens einmal): Wirkungen können doppelt stehen — nur mit [MindestensEinmal] (Generator CQRS110)",
                    };
                break;
            case "reader":
                yield return $"Antworten ⊆ {{{string.Join(", ", aus.Select(a => a.Name))}}} — Compiler (Signatur)";
                break;
            case "store":
                yield return "Signatur vom Store-Interface vorgegeben — Compiler";
                if (IstAtomarerStore(s.Klasse) && StoreRolle(s) is { } schreibt)
                    yield return schreibt
                        ? "Schreib-Fähigkeit: nur puffern über die Puffer-Methoden der Basis, keine committeten Daten lesen (Lesen-Ändern-Schreiben = Puffer-Methode mit Transformation) — Analyzer CQRS066/067"
                        : "Lese-Fähigkeit: " + Abstractions.StoreZugriff.Hinweis + "; nichts puffern — Analyzer CQRS067";
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
                         + (c.DeclaringSyntaxReferences.All(r => _quelltexte.IstGeneriert(r.SyntaxTree)) ? "   [generiert]" : "");
        foreach (var f in Felder(s))
            yield return $"Feld       {f.Name} : {TypVon(f)!.ToDisplayString(Kurz)}   [{FeldArt(TypVon(f)!)}]";
        for (var bt = s.Klasse.BaseType; bt != null && bt.SpecialType != SpecialType.System_Object; bt = bt.BaseType)
        {
            if (!_domänen.Contains(bt.ContainingAssembly?.Name ?? "") && !_framework.Contains(bt.ContainingAssembly?.Name ?? "")) continue;
            foreach (var mm in bt.GetMembers().Where(x => x.DeclaredAccessibility is Accessibility.Protected or Accessibility.Public
                                                          && !x.IsImplicitlyDeclared && x is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol))
                yield return $"Geerbt     {Sig(mm)}   (aus {bt.Name})"
                             + (IstAtomarerStore(s.Klasse) && IstDatenzugriff(TypVon(mm)) ? "   — " + Abstractions.StoreZugriff.Hinweis : "");
        }
        foreach (var h in Helfer(s)) yield return $"Helfer     {Sig(h)}";
        foreach (var c in s.Klasse.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsConst || f.IsStatic && f.IsReadOnly).Where(IstHand))
            yield return $"Konstante  {c.Type.ToDisplayString(Kurz)} {c.Name}{(c.HasConstantValue ? " = " + c.ConstantValue : "")}";
    }

    // ── Store-Zugriff (CQRS066/067): dieselbe Positivliste wie der Analyzer (Abstractions.StoreZugriff) ──

    /// <summary>Store der Klasse 1: eine Basis trägt den Fortschritt (<c>IAtomaresZiel</c>) — wie im Analyzer CQRS066/067, über Symbole.</summary>
    private static bool IstAtomarerStore(INamedTypeSymbol t)
    {
        for (var b = t.BaseType; b != null; b = b.BaseType)
            if (Infrastructure.SourceGeneration.Projektionsform.StoreKlassen(b).Contains(1)) return true;
        return false;
    }

    private static bool IstDatenzugriff(ITypeSymbol? t) =>
        t != null && Abstractions.StoreZugriff.DatenzugriffAssemblies.Contains(t.ContainingAssembly?.Name ?? "");

    /// <summary>true = der Slot implementiert eine Schreib-Fähigkeit, false = eine Lese-Fähigkeit, null = keine.</summary>
    private bool? StoreRolle(SlotInventar.SlotRef s)
    {
        foreach (var f in s.Klasse.AllInterfaces.Where(i => Sym.Implements(i, _iWriteStore) || Sym.Implements(i, _iReadStore)))
            foreach (var m in f.GetMembers().OfType<IMethodSymbol>())
                if (SymbolEqualityComparer.Default.Equals(s.Klasse.FindImplementationForInterfaceMember(m), s.Methode))
                    return Sym.Implements(f, _iWriteStore);
        return null;
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
        var syn = s.Methode!.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).First(n => !_quelltexte.IstGeneriert(n.SyntaxTree));
        if (DokuVon(s.Methode!) is { } md) yield return $"[/// an {s.Methode!.Name}] {md}";
        foreach (var c in Zeilenkommentare(syn)) yield return $"[// vor {s.Methode!.Name}] {c}";
        if (s.Parameter.FirstOrDefault()?.Type is INamedTypeSymbol ein && _domänen.Contains(ein.ContainingAssembly?.Name ?? ""))
            foreach (var r in ein.DeclaringSyntaxReferences.Where(r => !_quelltexte.IstGeneriert(r.SyntaxTree)).Take(1))
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
        && t.DeclaringSyntaxReferences.Any(r => !_quelltexte.IstGeneriert(r.SyntaxTree));

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
                    foreach (var e in _graph.Edges.Where(e => e.To == cmd.Id && e.Kind is EdgeKind.darf))
                        quellen.Add(e.Kind == EdgeKind.darf ? $"Akteur {e.From["akteur:".Length..]}" : $"Client {e.From["client:".Length..]}");
                }
                // Funktionen, deren Signatur den Command ausgibt (je Auf ein Eingang).
                foreach (var f in _dom.BausteinFunktionen)
                    foreach (var ein in f.Eingaenge.Where(x => x.AusgabenFull.Contains(disc.Fq())))
                        quellen.Add($"Funktion {f.Name} (auf {ein.EingangFull.Split('.').Last()})");
                // Fristen: ein Decide, dessen Signatur Frist<Command> ausgibt.
                foreach (var a in _dom.Aggregates)
                    foreach (var (cmdFull, _) in a.DecideOutcomes.Where(kv => kv.Value.Contains($"Frist<{disc.Fq()}>")))
                        quellen.Add($"Frist aus Decide({cmdFull.Split('.').Last()}) von {a.Name}");
                yield return $"{disc.Name} kommt von: {(quellen.Count == 0 ? "—" : string.Join(" · ", quellen.Distinct()))}";
                if (cmd?.Command?.IsCreation == true) yield return $"{disc.Name} ist Erzeugungs-Command (ICreationCommand)";
                foreach (var o in Ausgänge(s)) yield return $"{o.Name} geht an: {Abnehmer(o, s.State)}";
                break;
            }
            case "apply":
            {
                var evt = Knoten(NodeKind.@event, disc);
                var erzeuger = evt == null ? new List<string>() : _graph.Nodes.Where(n => n.Kind == NodeKind.command && n.Command!.Produces.Any(p => p.Event == evt.Name))
                    .Select(n => $"Decide({n.Name})").ToList();
                yield return $"{disc.Name} erzeugt von: {(erzeuger.Count == 0 ? "—" : string.Join(" · ", erzeuger))}";
                yield return $"{disc.Name} geht außerdem an: {Abnehmer(disc as INamedTypeSymbol, s.State, ohneApply: true)}";
                break;
            }
            case "projektion":
            {
                var pr = _dom.Projections.FirstOrDefault(p => p.Full == s.Klasse.Fq());
                var calls = pr?.HandleFaehigkeiten.GetValueOrDefault(disc.Name) ?? new();
                yield return $"Eingang: {disc.Name} (erzeugt von Aggregat {string.Join(", ", ErzeugerAggregate(disc))})";
                yield return $"Fähigkeiten (Signatur): {(calls.Count == 0 ? "—" : string.Join(", ", calls.Select(c => $"{c.Store}.{c.Method}")))}";
                foreach (var c in calls) if (FnSig(c.Store, c.Method) is { } sig) yield return $"    {sig}";
                foreach (var st in calls.Select(c => c.Store).Distinct())
                    yield return $"ReadModels von {st}: {string.Join(", ", ReadModelsVon(st))}";
                break;
            }
            case "reader":
            {
                var rd = _dom.Readers.FirstOrDefault(r => r.Full == s.Klasse.Fq());
                var calls = rd?.HandleFaehigkeiten.GetValueOrDefault(disc.Name) ?? new();
                yield return $"hinter der Frage (wer in denselben Store schreibt): {(rd is { Projektionen.Count: > 0 } ? string.Join(", ", rd.Projektionen) : "—")}";
                yield return $"Fähigkeiten (Signatur): {(calls.Count == 0 ? "—" : string.Join(", ", calls.Select(c => $"{c.Store}.{c.Method}")))}";
                foreach (var c in calls) if (FnSig(c.Store, c.Method) is { } sig) yield return $"    {sig}";
                foreach (var st in calls.Select(c => c.Store).Distinct())
                    yield return $"ReadModels von {st}: {string.Join(", ", ReadModelsVon(st))}";
                break;
            }
            case "store":
            {
                var st = StoreVon(s);
                if (st == null) break;
                var fn = st.Fns.First(f => f.Name == s.Disc);
                yield return $"Store {st.Name}: {(fn.IsRead ? "Lese" : "Schreib")}-Funktion {s.Disc}; Store-Klasse {(st.Klassen.Count == 0 ? "—" : string.Join(", ", st.Klassen))}";
                yield return $"ReadModels: {string.Join(", ", ReadModelsVon(st.Name))}";
                var aufrufer = Aufrufer(st.Name, s.Disc).Select(x => x.Titel).ToList();
                yield return $"aufgerufen von: {(aufrufer.Count == 0 ? "—" : string.Join(", ", aufrufer))}";
                foreach (var p in _dom.Projections.Where(p => p.HandleFaehigkeiten.Values.Any(cs => cs.Any(c => c.Store == st.Name && c.Method == s.Disc))))
                    yield return $"    {p.Name}{(p.MindestensEinmal ? " [MindestensEinmal]" : "")}";
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
            ziele.AddRange(fo.Funktionen.Select(f => $"Funktion {f}"));
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
            case "reader":
            {
                var calls = s.Art == "reader"
                    ? _dom.Readers.FirstOrDefault(r => r.Full == s.Klasse.Fq())?.HandleFaehigkeiten.GetValueOrDefault(disc.Name)
                    : _dom.Projections.FirstOrDefault(p => p.Full == s.Klasse.Fq())?.HandleFaehigkeiten.GetValueOrDefault(disc.Name);
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
            foreach (var (evt, calls) in p.HandleFaehigkeiten)
                if (calls.Any(c => c.Store == store && c.Method == method)
                    && _inv.Slots.FirstOrDefault(x => x.Klasse.Fq() == p.Full && x.Disc == evt) is { } sl)
                    yield return ($"{p.Name}.{Abstractions.Projektionsvertrag.Auf}({evt})", sl.Rumpf);
        foreach (var r in _dom.Readers)
            foreach (var (q, calls) in r.HandleFaehigkeiten)
                if (calls.Any(c => c.Store == store && c.Method == method)
                    && _inv.Slots.FirstOrDefault(x => x.Klasse.Fq() == r.Full && x.Disc == q) is { } sl)
                    yield return ($"{r.Name}.{Abstractions.Projektionsvertrag.Frage}({q})", sl.Rumpf);
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

    private bool IstHand(ISymbol m) => m.DeclaringSyntaxReferences.Any(r => !_quelltexte.IstGeneriert(r.SyntaxTree));

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
                var skel = bauer.Skelett();
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
            File.WriteAllText(Path.Combine(verz, DateiName(k)), k.Text);
            md.AppendLine($"| {k.Titel} | {k.RumpfStatus} | {(k.Auftrag == null ? "—" : "✓")} | {Token(k.Text)} | {k.NachbarBlöcke} | {k.KopplungBlöcke} | {skelTok + Token(k.Text)} |");
        }
        File.WriteAllText(Path.Combine(verz, "übersicht.md"), md.ToString());

        // index.json — was die LLM-Konsole (Werkbank) je Slot braucht: Anker zum Schreiben, Slot-Teil-Datei,
        // und den Auftrag, falls einer im Code/Board steht.
        var index = alle.Select(k =>
        {
            var m = k.Slot.Methode!;
            var decl = m.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).First(n => !lage.Quelltexte.IstGeneriert(n.SyntaxTree));
            var pos = decl.GetLocation().GetLineSpan();
            // Parametertyp so, wie er im Quelltext steht (Basisname) — der Anker, mit dem CodeSync die Methode wiederfindet.
            var pTyp = (decl as Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax)?.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString();
            return (object)new
            {
                id = k.Schlüssel + (k.Slot.Art == "store" ? "@" + k.Slot.Klasse.Name : ""),   // eindeutig (Store: mehrere Impls je Interface)
                schluessel = k.Schlüssel, titel = k.Titel, art = k.Slot.Art, besitzer = k.Schlüssel.Split('|')[1], disc = k.Slot.Disc,
                aggregat = k.Slot.State?.Name,
                klasse = k.Slot.Klasse.Name, methode = m.Name,
                parameterTyp = pTyp == null ? null : pTyp.Split('<')[0].Split('.').Last().TrimEnd('?').Trim(),
                datei = Path.GetRelativePath(solutionDir, pos.Path).Replace('\\', '/'), zeile = pos.StartLinePosition.Line + 1,
                rumpf = k.RumpfStatus, auftrag = k.Auftrag,
                slotDatei = DateiName(k), tokenSlot = Token(k.Text),
            };
        }).ToList();
        // Rümpfe der Clients DRAUSSEN: je Client-Implementierung (Python) und Methode der Basis ein Slot — eigener Anker (Datei + Klasse + Methode).
        foreach (var py in bauer.PythonSlots(solutionDir))
        {
            File.WriteAllText(Path.Combine(verz, py.SlotDatei), py.Text);
            md.AppendLine($"| {py.Titel} | {(py.Fehlt ? "fehlt" : "geschrieben")} | {(py.Auftrag == null ? "—" : "✓")} | {Token(py.Text)} | 0 | 0 | {skelTok + Token(py.Text)} |");
            index.Add(new
            {
                id = py.Id, schluessel = py.Id, titel = py.Titel, art = "zusage", besitzer = py.Client, disc = py.Event,
                klasse = py.Klasse, methode = py.Methode, signatur = py.Signatur, ausgaenge = py.Ausgaenge, datei = py.Datei, zeile = py.Zeile,
                rumpf = py.Fehlt ? "fehlt" : "geschrieben", auftrag = py.Auftrag, slotDatei = py.SlotDatei, tokenSlot = Token(py.Text),
            });
        }
        File.WriteAllText(Path.Combine(verz, "übersicht.md"), md.ToString());
        File.WriteAllText(Path.Combine(verz, "index.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            skelettDatei = "00-graph-skelett.txt", tokenSkelett = Token(skelett),
            // Das Laufzeit-Projekt (generierte Routing-Tabelle): sein Build fährt ALLE Code-Generatoren — die Konsole baut es nach dem Schreiben.
            laufzeitProjekt = lage.Laufzeit.FilePath is { } lp ? Path.GetRelativePath(solutionDir, lp).Replace('\\', '/') : null,
            slots = index,
        },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"✅ {alle.Count} Slot-Teile + Graph-Skelett (≈ {skelTok} Token) → {verz}");
        foreach (var g in alle.GroupBy(k => k.Slot.Art))
        {
            var t = g.Select(k => Token(k.Text)).OrderBy(x => x).ToList();
            Console.WriteLine($"   {g.Key,-11} {g.Count(),3} Slots   Slot-Teil Median ≈ {t[t.Count / 2],5}   Max ≈ {t[^1],5}   (+ Skelett ≈ {skelTok})");
        }
        return 0;
    }

    /// <summary>Dateiname des Slot-Teils; ein Store-Interface kann mehrere Implementierungen haben (→ Klasse im Namen).</summary>
    private static string DateiName(SlotKontext k) =>
        $"{k.Slot.Art}-{k.Schlüssel.Split('|')[1]}.{k.Slot.Disc}{(k.Slot.Art == "store" ? "@" + k.Slot.Klasse.Name : "")}.txt";

    /// <summary>Zeichen / 3,3 — gegen den Qwen-BPE kalibriert (siehe Konzept).</summary>
    public static int Token(string text) => (int)Math.Ceiling(text.Length / 3.3);
}
