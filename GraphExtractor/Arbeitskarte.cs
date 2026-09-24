using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Arbeitskarte (Konzept: docs/konzept-llm-minimalkontext.md, Phase K1)
//
//  Je H-Slot (heute: Decide-/Apply-Rumpf) eine GESCHLOSSENE WELT für ein LLM: fixe Signatur, abschließendes
//  Vokabular, Regeln der Slot-Art, Absicht, ein Nachbar-Beispiel, Szenarien aus dem Bestand. Alles aus Code-Fakten
//  (Symbole, Marker, Signaturen, Aufrufe) — keine Namenskonvention. Der EIGENE Rumpf fließt nie in die Karte ein
//  (er ist die Antwort); einzige Ausnahme ist die „// 🤖 Prompt:“-Zeile (die Absicht).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Vokabular-Eintrag: eine Deklaration (so, wie das LLM sie benutzen darf) + Kurz-Doku + Member.</summary>
public sealed record KartenEintrag(string Rolle, string Deklaration, string? Doku, List<string> Member);

/// <summary>Ein Szenario aus einem bestehenden Test (Gegeben/Wenn/Dann-Kette der Test-DSL).</summary>
public sealed record KartenSzenario(string Name, List<string> Schritte);

public sealed class Arbeitskarte
{
    public required string Art { get; init; }              // "decide" | "apply"
    public required string Aggregat { get; init; }
    public required string Disc { get; init; }             // Command- bzw. Event-Name (identifiziert die Methode)
    public required string Datei { get; init; }
    public required int Zeile { get; init; }
    public required string Rumpf { get; init; }            // "geschrieben" | "leer" (bewusst, Marker) | "fehlt" (throw-Stub)
    public required string Signatur { get; init; }
    public List<string> Verfuegbar { get; } = new();
    public List<string> Absicht { get; } = new();
    public string? Prompt { get; set; }
    public KartenEintrag? Eingang { get; set; }
    public List<KartenEintrag> Ausgaenge { get; } = new();
    public KartenEintrag? Zustand { get; set; }
    public List<string> ZustandWeitere { get; } = new();
    /// <summary>Vorhandene Hilfsmethoden der Decider-/Applier-Klasse (dürfen benutzt, nicht neu geschrieben werden).</summary>
    public List<string> Helfer { get; } = new();
    public List<KartenEintrag> Typen { get; } = new();
    public string Bcl { get; set; } = "";
    public List<string> Regeln { get; } = new();
    public (string Disc, string Rumpf)? Beispiel { get; set; }
    public List<KartenSzenario> Szenarien { get; } = new();
    /// <summary>
    /// Gegenprobe gegen den ECHTEN Rumpf (nicht Teil der Karte): Domänen-Symbole, die der handgeschriebene Rumpf benutzt,
    /// die aber nicht im Vokabular der Karte stehen. Leer = die Karte reicht für den Bestand. null = kein Rumpf zum Prüfen.
    /// </summary>
    public List<string>? NichtAufKarte { get; set; }

    public string Id => $"{Aggregat}.{Disc}";

    /// <summary>Token-Schätzung ohne Tokenizer: Zeichen / 3,3 (gegen den Qwen-BPE auf den 64 Bestands-Karten kalibriert, ±10 %).</summary>
    public static int Token(string text) => (int)Math.Ceiling(text.Length / 3.3);

    public string AlsText()
    {
        var b = new StringBuilder();
        void Kopf(string t) { if (b.Length > 0) b.AppendLine(); b.AppendLine("## " + t); }
        static string Doku(string? d) => d == null ? "" : "   // " + d;

        Kopf("AUFGABE");
        b.AppendLine("Schreibe NUR den Methodenrumpf — ohne Signatur, ohne die äußeren geschweiften Klammern, ohne using.");

        Kopf("SIGNATUR (fix)");
        b.AppendLine(Signatur);
        foreach (var v in Verfuegbar) b.AppendLine("Verfügbar: " + v);

        Kopf("ABSICHT");
        if (Prompt != null) b.AppendLine("🤖 " + Prompt);
        foreach (var a in Absicht) b.AppendLine(a);
        if (Prompt == null && Absicht.Count == 0) b.AppendLine("(keine hinterlegt — `// 🤖 Prompt:` im Rumpf ergänzen)");

        Kopf("VOKABULAR (abschließend — nichts anderes existiert)");
        void Eintrag(string label, KartenEintrag e)
        {
            b.AppendLine($"{label,-10}{e.Deklaration}{(e.Rolle.Length > 0 ? "   [" + e.Rolle + "]" : "")}{Doku(e.Doku)}");
            foreach (var m in e.Member) b.AppendLine($"{"",-12}{m}");
        }
        if (Eingang != null) Eintrag("Eingang", Eingang);
        if (Zustand != null)
        {
            Eintrag("Zustand", Zustand);
            if (ZustandWeitere.Count > 0) b.AppendLine($"{"",-12}weitere: {string.Join(", ", ZustandWeitere)}");
        }
        for (var i = 0; i < Helfer.Count; i++) b.AppendLine($"{(i == 0 ? "Helfer" : ""),-10}{Helfer[i]}");
        for (var i = 0; i < Ausgaenge.Count; i++) Eintrag(i == 0 ? "Ausgänge" : "", Ausgaenge[i]);
        for (var i = 0; i < Typen.Count; i++) Eintrag(i == 0 ? "Typen" : "", Typen[i]);
        b.AppendLine(Bcl);

        Kopf($"REGELN (Slot-Art: {Art})");
        foreach (var r in Regeln) b.AppendLine(r);

        if (Beispiel is { } bsp)
        {
            Kopf("BEISPIEL (Nachbar-Rumpf derselben Art, echter Code)");
            b.AppendLine($"// {(Art == "decide" ? "Decide" : "Apply")}({bsp.Disc})");
            b.AppendLine(bsp.Rumpf);
        }

        Kopf("MUSS BESTEHEN (Szenarien)");
        if (Szenarien.Count == 0)
            b.AppendLine("(keine im Bestand — in der Simulation erzeugen „📋 Als Test“ oder vorschlagen lassen und bestätigen)");
        for (var i = 0; i < Szenarien.Count; i++)
        {
            b.AppendLine($"S{i + 1} {Szenarien[i].Name}");
            foreach (var s in Szenarien[i].Schritte) b.AppendLine("   " + s);
        }
        return b.ToString();
    }
}

/// <summary>
/// Baut Arbeitskarten aus der Solution. Findet die Slots über den Vertrag (<c>IDecider&lt;T&gt;</c>/<c>IApplier&lt;T&gt;</c>,
/// erster Parameter <c>ICommand</c>/<c>IEvent</c>) und die Szenarien über die Test-DSL-FORM (generischer Typ über einen
/// State mit einer Methode, die genau ein <c>ICommand</c> nimmt) — nicht über ihren Namen.
/// </summary>
public sealed class KartenBauer
{
    /// <summary>Spiegel von <c>SimHost.CodeSync.PromptMarke</c> (SimHost referenziert den Extractor nicht).</summary>
    public const string PromptMarke = "// 🤖 Prompt:";

    /// <summary>Bis zu dieser Größe wird der ganze Zustand gezeigt; darüber greift der Relevanz-Schnitt.</summary>
    private const int ZustandVoll = 8;

    private static readonly SymbolDisplayFormat Kurz = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                              | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private sealed record Slot(string Art, INamedTypeSymbol State, INamedTypeSymbol Klasse, IMethodSymbol Methode,
        INamedTypeSymbol Disc, MethodDeclarationSyntax Syntax);

    private readonly List<Compilation> _comps;
    private readonly HashSet<string> _domänen;
    private readonly List<Compilation> _tests;
    private readonly INamedTypeSymbol? _iCommand, _iEvent, _iTransient, _iState, _iDecider, _iApplier;
    private readonly List<Slot> _slots = new();

    private KartenBauer(List<Compilation> comps, HashSet<string> domänen, List<Compilation> tests)
    {
        _comps = comps;
        _domänen = domänen;
        _tests = tests;
        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iCommand = Get(Vertrag.ICommand);
        _iEvent = Get(Vertrag.IEvent);
        _iTransient = Get(Vertrag.ITransientEvent);
        _iState = Get(Vertrag.IState);
        _iDecider = Get(Vertrag.IDecider);
        _iApplier = Get(Vertrag.IApplier);
        SammleSlots();
    }

    public static async Task<KartenBauer> ErstelleAsync(Solution solution, Projektlage lage)
    {
        // Test-Projekte = Nicht-Analyse-Projekte, die eine Domänen-Assembly referenzieren (dort liegen die Szenarien).
        var analyse = lage.Analyse.Select(a => a.Projekt.Id).ToHashSet();
        var domänenIds = lage.Analyse.Where(a => lage.DomänenAssemblies.Contains(a.Compilation.AssemblyName ?? ""))
            .Select(a => a.Projekt.Id).ToHashSet();
        var graph = solution.GetProjectDependencyGraph();
        var tests = new List<Compilation>();
        foreach (var p in solution.Projects.Where(p => !analyse.Contains(p.Id)))
            if (graph.GetProjectsThatThisProjectTransitivelyDependsOn(p.Id).Any(domänenIds.Contains)
                && await p.GetCompilationAsync() is { } c)
                tests.Add(c);
        return new KartenBauer(lage.Compilations, lage.DomänenAssemblies, tests);
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

    // ── Karte ──

    private Arbeitskarte Baue(Slot s)
    {
        var decide = s.Art == "decide";
        var pos = s.Syntax.GetLocation().GetLineSpan();
        var k = new Arbeitskarte
        {
            Art = s.Art, Aggregat = s.State.Name, Disc = s.Disc.Name,
            Datei = pos.Path, Zeile = pos.StartLinePosition.Line + 1,
            Rumpf = RumpfStatus(s),
            Signatur = $"{s.Methode.ReturnType.ToDisplayString(Kurz)} {s.Methode.Name}("
                       + string.Join(", ", s.Methode.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}")) + ")",
        };
        k.Verfuegbar.Add($"this.State (Typ {s.State.Name}, {(decide ? "nur lesen" : "schreibbar")}), {s.Methode.Parameters[0].Name}");

        // Absicht: Prompt-Zeile im eigenen Rumpf + Doku/Banner-Kommentare an Methode und Eingangstyp.
        k.Prompt = PromptAus(s.Syntax);
        // (Die Doku des Eingangstyps steht beim Eingang im Vokabular — hier nicht doppelt.)
        var absicht = new[] { DokuVon(s.Methode) }.Concat(Banner(s.Syntax)).Concat(TypBanner(s.Disc))
            .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!).Distinct().ToList();
        k.Absicht.AddRange(absicht.Where(a => !absicht.Any(x => x != a && x.StartsWith(a, StringComparison.Ordinal))));

        k.Eingang = Eintrag(s.Disc, decide ? "Command" : "Event");

        var ausgänge = decide ? Ausgänge(s.Methode) : new List<INamedTypeSymbol>();
        foreach (var o in ausgänge) k.Ausgaenge.Add(Eintrag(o, Sym.Implements(o, _iTransient) ? "Ablehnung" : "Event"));

        // Zustand mit Relevanz-Schnitt (Darstellung, keine Erkenntnis: gezeigt wird alles, nur unterschiedlich ausführlich).
        var geschwister = _slots.Where(x => x.Art == s.Art && x.State.Fq() == s.State.Fq() && x.Disc.Fq() != s.Disc.Fq()).ToList();
        var member = ZustandsMember(s.State);
        var relevant = RelevanteMember(s, member, ausgänge, geschwister);
        k.Zustand = new KartenEintrag("", $"{s.State.Name}{(decide ? "  (nur lesen)" : "  (schreibbar über this.State)")}", null,
            member.Where(m => relevant.Contains(m.Name)).Select(m => MemberZeile(m, decide)).ToList());
        k.ZustandWeitere.AddRange(member.Where(m => !relevant.Contains(m.Name)).Select(m => $"{m.Name}:{MemberTyp(m)}"));

        // Vorhandene Helfer der eigenen Klasse (alle partiellen Teile): Nicht-Slot-Methoden, handgeschrieben — nur Signatur.
        var slotMethoden = _slots.Where(x => x.Klasse.Fq() == s.Klasse.Fq()).Select(x => x.Methode).ToList();
        var helfer = s.Klasse.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared
                        && !slotMethoden.Any(x => SymbolEqualityComparer.Default.Equals(x, m))
                        && m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree)))
            .ToList();
        k.Helfer.AddRange(helfer.Select(m => MemberZeile(m, decide)));

        // Transitive Domänen-Typen (VOs, Enums, …) aus Eingang, Ausgängen und relevantem Zustand — Tiefe 2.
        var schon = new HashSet<string>(StringComparer.Ordinal) { s.Disc.Fq(), s.State.Fq() };
        foreach (var o in ausgänge) schon.Add(o.Fq());
        var saat = Feldtypen(s.Disc).Concat(ausgänge.SelectMany(Feldtypen))
            .Concat(member.Where(m => relevant.Contains(m.Name)).SelectMany(m => Entpacke(MemberTypSymbol(m))))
            .Concat(helfer.SelectMany(m => m.Parameters.SelectMany(p => Entpacke(p.Type))));
        foreach (var t in Transitiv(saat, schon, 2)) k.Typen.Add(Eintrag(t, t.TypeKind == TypeKind.Enum ? "Enum" : "Typ"));

        var regeln = Regeln(s.Art);
        k.Bcl = regeln.FirstOrDefault(r => r.StartsWith("BCL:")) ?? "";
        k.Regeln.AddRange(regeln.Where(r => !r.StartsWith("BCL:")));

        k.Beispiel = Beispiel(s, ausgänge, geschwister);
        k.Szenarien.AddRange(Szenarien(s));
        if (k.Rumpf == "geschrieben") k.NichtAufKarte = Gegenprobe(s, k);
        return k;
    }

    // ── Gegenprobe (Vorstufe der Kartenwand W2) ──

    /// <summary>
    /// Alle im eigenen Rumpf gebundenen Symbole aus Domänen-Assemblies (Typen, Member, Konstruktoren) — steht ihr Name
    /// im VOKABULAR-Abschnitt der Karte? Parameter/Lokale/BCL zählen nicht (die sind Signatur bzw. BCL-Zeile).
    /// </summary>
    private List<string> Gegenprobe(Slot s, Arbeitskarte k)
    {
        var text = k.AlsText();
        var a = text.IndexOf("## VOKABULAR", StringComparison.Ordinal);
        var e = text.IndexOf("## REGELN", StringComparison.Ordinal);
        var vokabular = a >= 0 && e > a ? text[a..e] : text;
        var model = Model(s.Syntax.SyntaxTree);
        var fehlt = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var n in s.Syntax.Body?.DescendantNodes().OfType<SimpleNameSyntax>() ?? Enumerable.Empty<SimpleNameSyntax>())
        {
            var sym = model.GetSymbolInfo(n).Symbol;
            if (sym is IMethodSymbol { MethodKind: MethodKind.Constructor } c) sym = c.ContainingType;
            if (sym is null or IParameterSymbol or ILocalSymbol or IRangeVariableSymbol or INamespaceSymbol) continue;
            var asm = (sym as ITypeSymbol ?? sym.ContainingType)?.ContainingAssembly?.Name;
            if (!_domänen.Contains(asm ?? "")) continue;
            // this.State selbst ist generierter Rahmen (steht als „Verfügbar“ in der Signatur-Sektion).
            if (sym is IPropertySymbol { Name: "State" } p && p.ContainingType.Fq() == s.Klasse.Fq()) continue;
            if (!Regex.IsMatch(vokabular, $@"(?<![\wÄÖÜäöüß]){Regex.Escape(sym.Name)}(?![\wÄÖÜäöüß])"))
                fehlt.Add(sym is ITypeSymbol ? sym.Name : $"{sym.ContainingType?.Name}.{sym.Name}");
        }
        return fehlt.ToList();
    }

    // ── Ausgänge / Zustand ──

    private List<INamedTypeSymbol> Ausgänge(IMethodSymbol m)
    {
        // IEnumerable<OneOf<A,B,C>> → A,B,C (per Symbol; Stelligkeit egal).
        var t = m.ReturnType as INamedTypeSymbol;
        var oneOf = t?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
        return Vertrag.IstOneOf(oneOf) ? oneOf!.TypeArguments.OfType<INamedTypeSymbol>().ToList() : new();
    }

    private List<ISymbol> ZustandsMember(INamedTypeSymbol state) => state.GetMembers()
        .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsImplicitlyDeclared && !m.IsStatic)
        .Where(m => m is IPropertySymbol || m is IMethodSymbol { MethodKind: MethodKind.Ordinary })
        .Where(m => m.DeclaringSyntaxReferences.Length > 0)
        .OrderBy(m => m.DeclaringSyntaxReferences[0].SyntaxTree.FilePath, StringComparer.Ordinal)
        .ThenBy(m => m.DeclaringSyntaxReferences[0].Span.Start)
        .ToList();

    private HashSet<string> RelevanteMember(Slot s, List<ISymbol> member, List<INamedTypeSymbol> ausgänge, List<Slot> geschwister)
    {
        var namen = member.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        // Generierte Framework-Member (Id/Version) nur, wenn ein Nachbar sie benutzt.
        var hand = member.Where(m => m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree))).ToList();
        if (hand.Count <= ZustandVoll) return hand.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);

        var rel = new HashSet<string>(StringComparer.Ordinal);
        // (1) von Nachbar-Rümpfen derselben Art gelesen/geschrieben (Datenfluss über das Semantic Model)
        foreach (var g in geschwister)
            foreach (var n in StateZugriffe(g)) if (namen.Contains(n)) rel.Add(n);
        // (2) Decide: berechnete bool-Helfer (billig, hoher Nutzen)
        if (s.Art == "decide")
            foreach (var p in hand.OfType<IPropertySymbol>().Where(p => p.SetMethod == null && p.Type.SpecialType == SpecialType.System_Boolean))
                rel.Add(p.Name);
        // (3) Wortgleichheit mit Eingang/Ausgängen/Feldern (nur Ranking — gezeigt wird ohnehin alles)
        var wörter = Wörter(s.Disc.Name).Concat(ausgänge.SelectMany(a => Wörter(a.Name)))
            .Concat(Felder(s.Disc).SelectMany(f => Wörter(f.Name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var m in hand) if (Wörter(m.Name).Any(wörter.Contains)) rel.Add(m.Name);
        return rel;
    }

    private IEnumerable<string> StateZugriffe(Slot s)
    {
        if (s.Syntax.Body is null && s.Syntax.ExpressionBody is null) yield break;
        var model = Model(s.Syntax.SyntaxTree);
        foreach (var id in s.Syntax.DescendantNodes().OfType<SimpleNameSyntax>())
            if (model.GetSymbolInfo(id).Symbol is { } sym && sym.ContainingType?.Fq() == s.State.Fq()
                && sym is IPropertySymbol or IMethodSymbol)
                yield return sym.Name;
    }

    private static IEnumerable<string> Wörter(string name) =>
        Regex.Matches(name, "[A-ZÄÖÜ][a-zäöüß]+|[a-zäöüß]+").Select(m => m.Value).Where(w => w.Length >= 4);

    private string MemberZeile(ISymbol m, bool decide)
    {
        var sig = (m.IsStatic ? "static " : "") + m switch
        {
            IPropertySymbol p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}{(!decide && p.SetMethod == null && !IstSammlung(p.Type) ? "   (berechnet)" : "")}",
            IMethodSymbol ms => $"{ms.ReturnType.ToDisplayString(Kurz)} {ms.Name}({string.Join(", ", ms.Parameters.Select(x => $"{x.Type.ToDisplayString(Kurz)} {x.Name}"))})",
            _ => m.Name,
        };
        var info = DokuVon(m) ?? KurzAusdruck(m);
        return info == null ? sig : $"{sig,-40} // {info}";
    }

    private static string MemberTyp(ISymbol m) => MemberTypSymbol(m)?.ToDisplayString(Kurz) ?? "?";
    private static ITypeSymbol? MemberTypSymbol(ISymbol m) => m switch
    {
        IPropertySymbol p => p.Type, IMethodSymbol ms => ms.ReturnType, IFieldSymbol f => f.Type, _ => null,
    };

    private static bool IstSammlung(ITypeSymbol t) => t is INamedTypeSymbol n && n.TypeArguments.Length > 0 || t is IArrayTypeSymbol;

    /// <summary>Expression-bodied Helfer als Kurzform (<c>=&gt; Status == …</c>), wenn kurz — sonst null.</summary>
    private static string? KurzAusdruck(ISymbol m)
    {
        var syn = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var expr = syn switch
        {
            PropertyDeclarationSyntax p => p.ExpressionBody?.Expression,
            MethodDeclarationSyntax md => md.ExpressionBody?.Expression,
            _ => null,
        };
        var text = expr == null ? null : Regex.Replace(expr.ToString(), @"\s+", " ");
        return text is { Length: <= 70 } ? "=> " + text : null;
    }

    // ── Typen ──

    private KartenEintrag Eintrag(INamedTypeSymbol t, string rolle)
    {
        if (t.TypeKind == TypeKind.Enum)
            return new KartenEintrag(rolle, $"enum {t.Name} {{ {string.Join(", ", t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name))} }}",
                DokuVon(t), new());

        var felder = Felder(t);
        var ctor = PrimärKonstruktor(t);
        string dekl;
        if (ctor != null)
            dekl = $"{t.Name}({string.Join(", ", ctor.Parameters.Select(p => $"{p.Type.ToDisplayString(Kurz)} {p.Name}"))})";
        else
            dekl = felder.Count == 0 ? $"{t.Name}()" : $"{t.Name} {{ {string.Join("; ", felder.Select(f => $"{MemberTyp(f)} {f.Name}"))} }}";

        // Zusätzliche öffentliche Oberfläche (berechnete Properties, statische Werte, Methoden) — ohne Rümpfe.
        var ctorNamen = ctor?.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal) ?? new();
        var extra = t.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsImplicitlyDeclared
                        && m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree)))
            .Where(m => m is IPropertySymbol p && !ctorNamen.Contains(p.Name) && (ctor != null || p.SetMethod == null)
                        || m is IMethodSymbol { MethodKind: MethodKind.Ordinary })
            .Select(m => MemberZeile(m, true))
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

    private static IEnumerable<ITypeSymbol> Feldtypen(INamedTypeSymbol t) =>
        Felder(t).SelectMany(f => Entpacke(f switch { IParameterSymbol p => p.Type, _ => MemberTypSymbol(f) }));

    private static IEnumerable<ITypeSymbol> Entpacke(ITypeSymbol? t)
    {
        if (t == null) yield break;
        if (t is IArrayTypeSymbol a) { foreach (var x in Entpacke(a.ElementType)) yield return x; yield break; }
        yield return t;
        if (t is INamedTypeSymbol n)
            foreach (var arg in n.TypeArguments)
                foreach (var x in Entpacke(arg)) yield return x;
    }

    private IEnumerable<INamedTypeSymbol> Transitiv(IEnumerable<ITypeSymbol> saat, HashSet<string> schon, int tiefe)
    {
        var ebene = saat.ToList();
        for (var d = 0; d < tiefe && ebene.Count > 0; d++)
        {
            var nächste = new List<ITypeSymbol>();
            foreach (var t in ebene.OfType<INamedTypeSymbol>())
            {
                if (!_domänen.Contains(t.ContainingAssembly?.Name ?? "")) continue;
                if (!t.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree))) continue;
                if (!schon.Add(t.OriginalDefinition.Fq())) continue;
                yield return t;
                if (t.TypeKind != TypeKind.Enum) nächste.AddRange(Feldtypen(t));
            }
            ebene = nächste;
        }
    }

    // ── Beispiel ──

    private (string, string)? Beispiel(Slot s, List<INamedTypeSymbol> ausgänge, List<Slot> geschwister)
    {
        var eigene = ausgänge.Select(a => a.Fq()).ToHashSet(StringComparer.Ordinal);
        var kandidaten = geschwister.Where(g => RumpfStatus(g) == "geschrieben")
            .Select(g => (g, Rumpf: Rumpf(g.Syntax), Geteilt: Ausgänge(g.Methode).Count(a => eigene.Contains(a.Fq()))))
            .Where(x => Regex.IsMatch(x.Rumpf, @"[;}]"))   // mindestens eine Anweisung (kein bloßer Kommentar)
            .OrderByDescending(x => x.Geteilt).ThenBy(x => x.Rumpf.Length).ThenBy(x => x.g.Disc.Name, StringComparer.Ordinal)
            .ToList();
        return kandidaten.Count == 0 ? null : (kandidaten[0].g.Disc.Name, kandidaten[0].Rumpf);
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

    /// <summary>Ist <paramref name="t"/> der Marker selbst oder implementiert ihn?</summary>
    private static bool IstOderImplementiert(ITypeSymbol t, INamedTypeSymbol? marker) =>
        marker != null && (t.Fq() == marker.Fq() || Sym.Implements(t, marker));

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

    // ── Absicht ──

    private static string? PromptAus(MethodDeclarationSyntax m) =>
        m.Body?.DescendantTrivia().Select(t => t.ToString().Trim())
            .FirstOrDefault(t => t.StartsWith(PromptMarke))?[PromptMarke.Length..].Trim();

    /// <summary>Banner-/Zeilenkommentare direkt vor der Methode (ohne reine Trennlinien) — handgeschriebene Absicht.</summary>
    private static IEnumerable<string> Banner(SyntaxNode n) => n.GetLeadingTrivia()
        .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
        .Select(t => t.ToString().TrimStart('/').Trim())
        .Where(t => t.Length > 0 && t.Any(char.IsLetter));

    private static IEnumerable<string> TypBanner(INamedTypeSymbol t) =>
        t.DeclaringSyntaxReferences.Where(r => !Projektlage.IstGeneriert(r.SyntaxTree)).Take(1).SelectMany(r => Banner(r.GetSyntax()));

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
            return text.Length <= 220 ? text : text[..217].TrimEnd() + "…";
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
                    var name = st.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.Text.Replace('_', ' ') ?? "";
                    yield return new KartenSzenario(name, kette.Schritte);
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
            var besitzer = m.ContainingType;
            var arg0 = besitzer?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
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

    private SemanticModel Model(SyntaxTree tree) => _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    private static List<string> Regeln(string art)
    {
        var name = $"GraphExtractor.Karten.{art}.txt";
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Regelblock '{name}' fehlt (EmbeddedResource).");
        using var r = new StreamReader(s);
        return r.ReadToEnd().Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToList();
    }

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
    public static async Task<int> LaufAsync(string[] args, Solution solution, Projektlage lage)
    {
        var bauer = await KartenBauer.ErstelleAsync(solution, lage);
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
            var md = new System.Text.StringBuilder();
            md.AppendLine("| Art | Slot | Rumpf | Karte ≈Tok | Datei ≈Tok | Ordner ≈Tok | Szenarien | Beispiel | Gegenprobe |");
            md.AppendLine("|---|---|---|---:|---:|---:|---:|---|---|");
            foreach (var z in zeilen)
                md.AppendLine($"| {z.k.Art} | {z.k.Id} | {Symbol(z.k.Rumpf)} | {z.Karte} | {z.Datei} | {z.Ordner} | {z.k.Szenarien.Count} | {z.k.Beispiel?.Disc ?? "—"} | {Probe(z.k)} |");
            md.AppendLine($"\nDomäne gesamt ≈ {mass.Domäne} Token (handgeschriebene Quelltexte der Domänen-Projekte).");
            await File.WriteAllTextAsync(Path.Combine(verz, "übersicht.md"), md.ToString());
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                domaeneToken = mass.Domäne,
                karten = zeilen.Select(z => new
                {
                    art = z.k.Art, id = z.k.Id, datei = Rel(z.k.Datei, solution), zeile = z.k.Zeile, rumpf = z.k.Rumpf,
                    token = new { karte = z.Karte, datei = z.Datei, ordner = z.Ordner },
                    szenarien = z.k.Szenarien.Count, beispiel = z.k.Beispiel?.Disc, nichtAufKarte = z.k.NichtAufKarte, text = z.k.AlsText(),
                }),
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await File.WriteAllTextAsync(Path.Combine(verz, "karten.json"), json);
            Console.WriteLine($"✅ {alle.Count} Karten → {verz}");
        }

        Console.WriteLine($"\n── Arbeitskarten: {alle.Count} Slots (Decide/Apply) ──");
        Console.WriteLine($"   {"Art",-7}{"Slot",-44}{"Rumpf",-9}{"Karte",7}{"Datei",8}{"Ordner",8}{"Szen.",7}  Gegenprobe");
        foreach (var z in zeilen)
            Console.WriteLine($"   {z.k.Art,-7}{z.k.Id,-44}{Symbol(z.k.Rumpf),-9}{z.Karte,7}{z.Datei,8}{z.Ordner,8}{z.k.Szenarien.Count,7}  {Probe(z.k)}");
        var geprüft = alle.Where(k => k.NichtAufKarte != null).ToList();
        Console.WriteLine($"\n   Gegenprobe: {geprüft.Count(k => k.NichtAufKarte!.Count == 0)}/{geprüft.Count} geschriebene Rümpfe benutzen nur Symbole ihrer Karte.");
        if (zeilen.Count > 0)
        {
            var med = zeilen.Select(z => z.Karte).OrderBy(x => x).ElementAt(zeilen.Count / 2);
            Console.WriteLine($"\n   Karte: Median ≈ {med}, Max ≈ {zeilen.Max(z => z.Karte)} Token · Ordner Median ≈ "
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
