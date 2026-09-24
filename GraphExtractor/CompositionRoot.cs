using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Composition-Root-Extraktion (die dritte, sonst unsichtbare Ebene) — SEMANTISCH und ohne Namenswissen.
//
//  Kein Datei-, Pfad-, Projekt-, Methoden- oder Konfig-Key-Name ist hier bekannt. Alles wird über den
//  Framework-Vertrag (Typ-Anker, Vertrag.cs) und die Form des Codes gefunden:
//    • Frist-Router   = Aufruf in einem Host mit einem Lambda über den Vertrags-Typ `Frist`, das je Zweig
//                       `f.Kontext == <Konstante> ? new <Command>(…)` liefert.
//    • plant/storniert = Handler einer Pipeline, die (auch über private Helfer) IFristplan.PlaneAsync/EntferneAsync
//                       erreichen; Kontext = Konstante im `new Frist(…, Kontext)`; Dauer = Konfig-Feld im `Fällig`.
//    • Ingress        = Aufruf einer Methode, die sich per [Ingress(Art, Ort = nameof(param))] als Trigger-Ingress
//                       deklariert (Webhook/Timer/Datei); Route/Intervall/Pfad = Argument des genannten Parameters,
//                       Nachricht = die IPipelineTrigger, die das übergebene Lambda liefert.
//    • Dienst         = DI-Registrierung (Microsoft-API) <Vertrag, Impl> mit Vertrag aus einer Domänen-Assembly.
//    • HostSetting    = Argument einer DI-registrierten Konfig-Record-Erzeugung; Herkunft bis zum Key der Konfiguration
//                       verfolgt (auch über Parameter → Aufrufer im Host). Keys, die in keine Domänen-Konfiguration
//                       fließen, sind Infrastruktur und bleiben draußen.
//  Fehlt eine Quelle, bleibt die Liste leer — nichts wird erfunden.
// ════════════════════════════════════════════════════════════════════════════

public sealed class CompositionRoot
{
    public List<TriggerBinding> Triggers { get; } = new();
    public List<FristBinding> Frists { get; } = new();
    public List<DienstBinding> Dienste { get; } = new();
    public List<HostSetting> HostSettings { get; } = new();
    /// <summary>Pipeline-Name → genutzte Dienst-Verträge (Konstruktor-Injektion).</summary>
    public Dictionary<string, List<string>> PipelineDienste { get; } = new();
}

public sealed record TriggerBinding(string Name, string Modus, string? Route, string? Interval,
    string? Path, string MsgName, string? TargetPipelineId);
public sealed record FristBinding(string Name, string Kontext, string Sendet, string? Aggregat,
    List<string> Plant, List<string> Storniert, string? DauerSetting);
public sealed record DienstBinding(string Name, string Vertrag, string? Impl, bool Extern);
/// <summary>
/// Ein Betriebs-Parameter. <see cref="Konfig"/>/<see cref="Feld"/> = in welches Feld welches Konfigurations-Records
/// er fließt (die Kante HostSetting → Konfig → Pipeline).
/// </summary>
public sealed record HostSetting(string Name, string Typ, string Default, string EnvKey, string? Konfig = null, string? Feld = null);

public sealed class CompositionRootExtractor
{
    private readonly Solution _solution;
    private readonly Projektlage _lage;
    private readonly RoutingTruth _routing;
    private readonly DomainModel _dom;
    private readonly List<(Compilation Comp, SyntaxTree Tree)> _hostQuellen, _domänenQuellen;
    private readonly INamedTypeSymbol? _frist, _fristplan, _iCommand, _iTrigger;

    public CompositionRootExtractor(Solution solution, Projektlage lage, RoutingTruth routing, DomainModel dom)
    {
        _solution = solution;
        _lage = lage;
        _routing = routing;
        _dom = dom;
        _hostQuellen = lage.Hosts.SelectMany(h => h.Compilation.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)).Select(t => (h.Compilation, t))).ToList();
        _domänenQuellen = lage.DomänenCompilations.SelectMany(c => c.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)).Select(t => (c, t))).ToList();
        INamedTypeSymbol? Get(string n) => lage.Compilations.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _frist = Get(Vertrag.Frist);
        _fristplan = Get(Vertrag.IFristplan);
        _iCommand = Get(Vertrag.ICommand);
        _iTrigger = Get(Vertrag.IPipelineTrigger);
    }

    public async Task<CompositionRoot> ExtractAsync()
    {
        var cr = new CompositionRoot();
        ExtractDeadlines(cr);
        AttachFristPlanCancel(cr);
        ExtractIngress(cr);
        ExtractServices(cr);
        await ExtractKonfigSettingsAsync(cr);
        LinkFristDauer(cr);
        ExtractPipelineDienste(cr);
        return cr;
    }

    // Symbol-Vergleich über Compilation-Grenzen (Host vs. Domäne sehen denselben Typ als verschiedene Symbole).
    private static bool Gleich(ITypeSymbol? a, ITypeSymbol? b) => a != null && b != null && a.ToDisplayString() == b.ToDisplayString();

    // ── 1) Frist-Router: Lambda über `Frist` mit Zweigen `f.Kontext == K ? new Cmd(…)` ─────────
    private void ExtractDeadlines(CompositionRoot cr)
    {
        if (_frist == null) return;
        foreach (var (comp, tree) in _hostQuellen)
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var lambda in tree.GetRoot().DescendantNodes().OfType<LambdaExpressionSyntax>())
            {
                if (model.GetSymbolInfo(lambda).Symbol is not IMethodSymbol lm || lm.Parameters.Length != 1
                    || !Gleich(lm.Parameters[0].Type, _frist)) continue;
                // Jeder Zweig über f.Kontext: Ternär, if, switch-Ausdruck, switch-Anweisung → (Kontext-Konstante, Zweig).
                var zweige = new List<(string? Kontext, SyntaxNode Zweig)>();
                foreach (var n in lambda.DescendantNodesAndSelf())
                    switch (n)
                    {
                        case ConditionalExpressionSyntax c: zweige.Add((KontextKonstante(c.Condition, model), c.WhenTrue)); break;
                        case IfStatementSyntax i: zweige.Add((KontextKonstante(i.Condition, model), i.Statement)); break;
                        case SwitchExpressionSyntax sw when IstFristKontext(sw.GoverningExpression, model):
                            foreach (var arm in sw.Arms)
                                if (arm.Pattern is ConstantPatternSyntax cp && model.GetConstantValue(cp.Expression) is { HasValue: true, Value: string k })
                                    zweige.Add((k, arm.Expression));
                            break;
                        case SwitchStatementSyntax ss when IstFristKontext(ss.Expression, model):
                            foreach (var sec in ss.Sections)
                                foreach (var lab in sec.Labels.OfType<CaseSwitchLabelSyntax>())
                                    if (model.GetConstantValue(lab.Value) is { HasValue: true, Value: string k }) zweige.Add((k, sec));
                            break;
                    }
                foreach (var (kontext, zweig) in zweige)
                {
                    var cmd = zweig.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>()
                        .Select(oc => model.GetTypeInfo(oc).Type).OfType<INamedTypeSymbol>()
                        .FirstOrDefault(t => Sym.Implements(t, _iCommand));
                    if (kontext == null || cmd == null || cr.Frists.Any(f => f.Kontext == kontext)) continue;
                    cr.Frists.Add(new FristBinding(Name: kontext, Kontext: kontext, Sendet: cmd.Name,
                        Aggregat: AggregateOf(cmd.Fq()), Plant: new(), Storniert: new(), DauerSetting: null));
                }
            }
        }
    }

    /// <summary>In <c>f.Kontext == X</c> die Konstante der Seite, die NICHT die Frist-Eigenschaft ist.</summary>
    private string? KontextKonstante(ExpressionSyntax bedingung, SemanticModel model)
    {
        foreach (var bin in bedingung.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Where(b => b.IsKind(SyntaxKind.EqualsExpression)))
        {
            var andere = IstFristKontext(bin.Left, model) ? bin.Right : IstFristKontext(bin.Right, model) ? bin.Left : null;
            if (andere != null && model.GetConstantValue(andere) is { HasValue: true, Value: string s }) return s;
        }
        return null;
    }

    /// <summary>Ist der Ausdruck die Vertrags-Eigenschaft <c>Frist.Kontext</c> (per Symbol)?</summary>
    private bool IstFristKontext(ExpressionSyntax e, SemanticModel model) =>
        model.GetSymbolInfo(e).Symbol is IPropertySymbol p && p.Name == Vertrag.FristKontext && Gleich(p.ContainingType, _frist);

    // ── 2) plant/storniert/Dauer aus den Pipelines (über IFristplan, inkl. privater Helfer) ──
    private readonly Dictionary<string, string> _pipelineKontext = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Konfig, string Feld)> _kontextDauer = new(StringComparer.Ordinal);

    private void AttachFristPlanCancel(CompositionRoot cr)
    {
        if (_fristplan == null || _frist == null) return;
        foreach (var pipe in _dom.Pipelines)
        {
            var t = _lage.Compilations.Select(c => c.GetTypeByMetadataName(pipe.Full)).FirstOrDefault(x => x != null);
            if (t == null) continue;
            // Direkte Frist-Aufrufe + Aufrufe typ-eigener Methoden je Methode → transitive Hülle.
            var direkt = new Dictionary<IMethodSymbol, (bool Plant, bool Storniert)>(SymbolEqualityComparer.Default);
            var ruft = new Dictionary<IMethodSymbol, List<IMethodSymbol>>(SymbolEqualityComparer.Default);
            foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
            {
                var decl = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (decl == null) continue;
                var model = Model(decl.SyntaxTree);
                bool plant = false, storniert = false;
                var eigene = new List<IMethodSymbol>();
                foreach (var inv in decl.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol ziel) continue;
                    if (Gleich(ziel.ContainingType, _fristplan))
                    {
                        if (ziel.Name == Vertrag.FristPlane) { plant = true; LeseFristErzeugung(inv, model, pipe.Name); }
                        if (ziel.Name == Vertrag.FristEntferne) storniert = true;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(ziel.ContainingType, t)) eigene.Add(ziel.OriginalDefinition);
                }
                direkt[m] = (plant, storniert);
                ruft[m] = eigene;
            }
            (bool, bool) Erreicht(IMethodSymbol m, HashSet<IMethodSymbol> besucht)
            {
                if (!besucht.Add(m)) return (false, false);
                var (p, s) = direkt.GetValueOrDefault(m);
                foreach (var z in ruft.GetValueOrDefault(m) ?? new())
                {
                    var (zp, zs) = Erreicht(z, besucht);
                    p |= zp; s |= zs;
                }
                return (p, s);
            }
            if (!_pipelineKontext.TryGetValue(pipe.Name, out var kontext)) continue;
            var frist = cr.Frists.FirstOrDefault(f => f.Kontext == kontext);
            if (frist == null) continue;
            // Nur die echten Handler: handgeschrieben, öffentlich, mit PipelineContext-Parameter (nicht die generierte Dispatch-Methode).
            var ctxTyp = _lage.Compilations.Select(c => c.GetTypeByMetadataName(Vertrag.PipelineContext)).FirstOrDefault(x => x != null);
            foreach (var m in direkt.Keys.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.MethodKind == MethodKind.Ordinary
                         && m.Parameters.Length > 1 && m.Parameters.Skip(1).Any(p => Gleich(p.Type, ctxTyp))
                         && m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree))))
            {
                var (p, s) = Erreicht(m, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
                var evt = m.Parameters[0].Type.Name;
                if (p && !frist.Plant.Contains(evt)) frist.Plant.Add(evt);
                if (s && !frist.Storniert.Contains(evt)) frist.Storniert.Add(evt);
            }
        }
    }

    /// <summary>Aus <c>PlaneAsync(new Frist(…))</c>: Kontext-Konstante und Konfig-Feld im Fälligkeits-Ausdruck.</summary>
    private void LeseFristErzeugung(InvocationExpressionSyntax plane, SemanticModel model, string pipeline)
    {
        var erzeugung = plane.ArgumentList.Arguments.Select(a => a.Expression).SelectMany(e => e.DescendantNodesAndSelf())
            .OfType<BaseObjectCreationExpressionSyntax>()
            .FirstOrDefault(oc => Gleich(model.GetTypeInfo(oc).Type, _frist));
        if (erzeugung?.ArgumentList == null || model.GetSymbolInfo(erzeugung).Symbol is not IMethodSymbol ctor) return;
        string? kontext = null; ExpressionSyntax? fällig = null;
        for (var i = 0; i < erzeugung.ArgumentList.Arguments.Count; i++)
        {
            var arg = erzeugung.ArgumentList.Arguments[i];
            var param = arg.NameColon is { } nc ? ctor.Parameters.FirstOrDefault(p => p.Name == nc.Name.Identifier.Text)
                : i < ctor.Parameters.Length ? ctor.Parameters[i] : null;
            if (param?.Name == Vertrag.FristKontext && model.GetConstantValue(arg.Expression) is { HasValue: true, Value: string s }) kontext = s;
            if (param?.Name == Vertrag.FristFällig) fällig = arg.Expression;
        }
        if (kontext == null) return;
        _pipelineKontext[pipeline] = kontext;
        if (fällig != null && KonfigFeldIn(fällig, model, 0) is { } kf) _kontextDauer[kontext] = kf;
    }

    /// <summary>Das (erste) Konfig-Record-Feld, das in den Ausdruck einfließt — auch über lokale Variablen.</summary>
    private (string Konfig, string Feld)? KonfigFeldIn(ExpressionSyntax e, SemanticModel model, int tiefe)
    {
        if (tiefe > 4) return null;
        var konfigs = _dom.Konfigs.Select(k => k.Full).ToHashSet(StringComparer.Ordinal);
        foreach (var n in e.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            var sym = model.GetSymbolInfo(n).Symbol;
            if (sym is IPropertySymbol p && konfigs.Contains(p.ContainingType.Fq())) return (p.ContainingType.Name, p.Name);
            if (sym is ILocalSymbol l && l.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer: { } ini }
                && KonfigFeldIn(ini.Value, model, tiefe + 1) is { } kf) return kf;
        }
        return null;
    }

    // ── 3) Trigger-Ingress: Aufrufe von Methoden, die sich per [Ingress] als Ingress DEKLARIEREN ──
    private void ExtractIngress(CompositionRoot cr)
    {
        if (_iTrigger == null) return;
        foreach (var (comp, tree) in _hostQuellen.Concat(_domänenQuellen))
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var inv in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol m) continue;
                var basis = (m.ReducedFrom ?? m).OriginalDefinition;
                var attr = basis.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Vertrag.IngressAttribute);
                if (attr?.ConstructorArguments.FirstOrDefault().Value is not int art || !Vertrag.IngressModus.TryGetValue(art, out var modus)) continue;

                // Der Ort (Route/Intervall/Pfad) = das Argument des Parameters, den das Attribut per nameof nennt.
                var ortParam = attr.NamedArguments.FirstOrDefault(a => a.Key == Vertrag.IngressOrt).Value.Value as string;
                var ort = ortParam == null ? null : Argument(inv, m, ortParam) is { } oa
                    ? (model.GetConstantValue(oa) is { HasValue: true, Value: string s } ? s : oa.ToString()) : null;

                // Die Nachricht = der IPipelineTrigger, den ein übergebenes Lambda liefert (Ausdruck bzw. return).
                var msg = inv.ArgumentList.Arguments.Select(a => a.Expression).OfType<LambdaExpressionSyntax>()
                    .SelectMany(l => l.Body is ExpressionSyntax body ? new[] { body }
                        : l.Body.DescendantNodes().OfType<ReturnStatementSyntax>().Select(r => r.Expression).OfType<ExpressionSyntax>())
                    .Select(e => model.GetTypeInfo(e).Type).OfType<INamedTypeSymbol>().FirstOrDefault(t => Sym.Implements(t, _iTrigger));
                if (msg == null || cr.Triggers.Any(x => x.MsgName == msg.Name && x.Modus == modus && x.Route == ort)) continue;
                cr.Triggers.Add(new TriggerBinding(Name: msg.Name, Modus: modus,
                    Route: modus == Vertrag.IngressModus[(int)Abstractions.IngressArt.Webhook] ? ort : null,
                    Interval: modus == Vertrag.IngressModus[(int)Abstractions.IngressArt.Timer] ? ort : null,
                    Path: modus == Vertrag.IngressModus[(int)Abstractions.IngressArt.Datei] ? ort : null,
                    MsgName: msg.Name, TargetPipelineId: PipelineHandling(msg.Fq())));
            }
        }
    }

    /// <summary>Das Argument für den Parameter <paramref name="param"/> (benannt oder positionell; Extension-Aufrufe berücksichtigt).</summary>
    private static ExpressionSyntax? Argument(InvocationExpressionSyntax inv, IMethodSymbol m, string param)
    {
        var args = inv.ArgumentList.Arguments;
        var benannt = args.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == param);
        if (benannt != null) return benannt.Expression;
        var p = m.Parameters.FirstOrDefault(x => x.Name == param);
        return p != null && p.Ordinal < args.Count && args[p.Ordinal].NameColon == null ? args[p.Ordinal].Expression : null;
    }

    // ── 4) Dienste: DI-Registrierung <Vertrag, Impl> (Microsoft-API) mit Domänen-Vertrag ──
    private void ExtractServices(CompositionRoot cr)
    {
        foreach (var (comp, tree) in _domänenQuellen.Concat(_hostQuellen))
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var inv in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (DiRegistrierung(inv, model) is not { TypeArguments.Length: >= 1 } m) continue;
                if (m.TypeArguments[0] is not INamedTypeSymbol { TypeKind: TypeKind.Interface } vertrag
                    || !_lage.DomänenAssemblies.Contains(vertrag.ContainingAssembly?.Name ?? "")) continue;
                if (cr.Dienste.Any(d => d.Vertrag == vertrag.Name)) continue;
                // Implementierung: zweites Typ-Argument; sonst der Typ, den die Fabrik liefert bzw. die übergebene Instanz hat.
                var impl = m.TypeArguments.Length > 1 ? m.TypeArguments[1]
                    : inv.ArgumentList.Arguments.Select(a => a.Expression)
                        .Select(e => e is LambdaExpressionSyntax { Body: ExpressionSyntax b } ? b : e)
                        .Select(e => model.GetTypeInfo(e).Type).FirstOrDefault(t => t is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct });
                cr.Dienste.Add(new DienstBinding(Name: impl?.Name ?? vertrag.Name, Vertrag: vertrag.Name, Impl: impl?.Name, Extern: impl == null));
            }
        }
    }

    private static IMethodSymbol? DiRegistrierung(InvocationExpressionSyntax inv, SemanticModel model)
    {
        if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol m) return null;
        var basis = m.ReducedFrom ?? m;
        var typ = basis.ContainingType?.ToDisplayString();
        return typ == Vertrag.DiErweiterungen || typ == Vertrag.DiTryErweiterungen ? m : null;
    }

    // ── 5) HostSettings: Argumente DI-registrierter Konfig-Erzeugungen, Herkunft bis zum Konfig-Key ──
    private async Task ExtractKonfigSettingsAsync(CompositionRoot cr)
    {
        var konfigs = _dom.Konfigs.Select(k => k.Full).ToHashSet(StringComparer.Ordinal);
        if (konfigs.Count == 0) return;
        foreach (var (comp, tree) in _domänenQuellen.Concat(_hostQuellen))
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var oc in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(oc).Symbol is not IMethodSymbol ctor || !konfigs.Contains(ctor.ContainingType.Fq())) continue;
                var reg = oc.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                if (reg == null || DiRegistrierung(reg, model) == null) continue;
                var args = oc.ArgumentList?.Arguments ?? default;
                for (var i = 0; i < args.Count; i++)
                {
                    var arg = args[i];
                    var param = arg.NameColon is { } nc ? ctor.Parameters.FirstOrDefault(p => p.Name == nc.Name.Identifier.Text)
                        : i < ctor.Parameters.Length ? ctor.Parameters[i] : null;
                    if (param == null) continue;
                    var (key, def) = await HerkunftAsync(arg.Expression, model, 0);
                    var name = key.Length > 0 ? key.Split(':').Last() : param.Name;
                    if (cr.HostSettings.Any(h => h.Name == name)) name = ctor.ContainingType.Name + "." + param.Name;
                    cr.HostSettings.Add(new HostSetting(name, param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        def, key, ctor.ContainingType.Name, param.Name));
                }
            }
        }
    }

    /// <summary>
    /// Herkunft eines Werts: (Konfig-Key, Default). Konstante → Default; lokale Variable → Initializer;
    /// <c>a ?? b</c> → Key aus a, Default aus b; Konfig-Lesen (Microsoft-API) → Key = konstantes Argument;
    /// Methoden-Parameter → Argument an den Aufrufern in den Hosts. Sonst: ("", Ausdruckstext).
    /// </summary>
    private async Task<(string Key, string Default)> HerkunftAsync(ExpressionSyntax e, SemanticModel model, int tiefe)
    {
        if (tiefe > 8) return ("", e.ToString());
        if (model.GetConstantValue(e) is { HasValue: true } k) return ("", k.Value?.ToString() ?? "null");
        if (e is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.CoalesceExpression))
        {
            var (key, _) = await HerkunftAsync(bin.Left, model, tiefe + 1);
            var (_, def) = await HerkunftAsync(bin.Right, model, tiefe + 1);
            return (key, def);
        }
        if (e is InvocationExpressionSyntax inv && model.GetSymbolInfo(inv).Symbol is IMethodSymbol ms
            && (ms.ReducedFrom ?? ms).ContainingType?.ToDisplayString() == Vertrag.KonfigBinder && ms.Name == Vertrag.KonfigGetValue)
        {
            var konst = inv.ArgumentList.Arguments.Select(a => model.GetConstantValue(a.Expression)).Where(c => c is { HasValue: true, Value: string }).ToList();
            return (konst.Count > 0 ? (string)konst[0].Value! : "", konst.Count > 1 ? (string)konst[1].Value! : "");
        }
        // cfg["Key"] — der Indexer von IConfiguration (auch über abgeleitete Typen wie ConfigurationManager).
        if (e is ElementAccessExpressionSyntax ea && model.GetSymbolInfo(ea).Symbol is IPropertySymbol { IsIndexer: true } ix
            && (ix.ContainingType.ToDisplayString() == Vertrag.KonfigSchnittstelle
                || ix.ContainingType.AllInterfaces.Any(i => i.ToDisplayString() == Vertrag.KonfigSchnittstelle))
            && ea.ArgumentList.Arguments.Count == 1 && model.GetConstantValue(ea.ArgumentList.Arguments[0].Expression) is { HasValue: true, Value: string ik })
            return (ik, "");
        // Environment.GetEnvironmentVariable("X") — die Umgebungsvariable ist der Key.
        if (e is InvocationExpressionSyntax envInv && model.GetSymbolInfo(envInv).Symbol is IMethodSymbol envM
            && envM.ContainingType?.ToDisplayString() == Vertrag.Umgebung && envM.Name == Vertrag.UmgebungLesen
            && envInv.ArgumentList.Arguments.Count >= 1 && model.GetConstantValue(envInv.ArgumentList.Arguments[0].Expression) is { HasValue: true, Value: string ek })
            return (ek, "");
        var sym = model.GetSymbolInfo(e).Symbol;
        if (sym is ILocalSymbol loc && loc.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer: { } ini })
            return await HerkunftAsync(ini.Value, model, tiefe + 1);
        if (sym is IParameterSymbol par && par.ContainingSymbol is IMethodSymbol methode)
        {
            var dflt = par.HasExplicitDefaultValue ? par.ExplicitDefaultValue?.ToString() ?? "null" : "";
            var hosts = _lage.Hosts.Select(h => h.Projekt.Id).ToHashSet();
            foreach (var aufrufer in await SymbolFinder.FindCallersAsync(methode, _solution))
                foreach (var ort in aufrufer.Locations.Where(l => l.IsInSource))
                {
                    var doc = _solution.GetDocument(ort.SourceTree);
                    if (doc == null || !hosts.Contains(doc.Project.Id)) continue;
                    var aufrufModel = await doc.GetSemanticModelAsync();
                    var aufruf = (await doc.GetSyntaxRootAsync())?.FindNode(ort.SourceSpan).AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    if (aufrufModel == null || aufruf == null) continue;
                    var reduziert = (aufrufModel.GetSymbolInfo(aufruf).Symbol as IMethodSymbol)?.ReducedFrom != null;
                    var ordinal = par.Ordinal - (reduziert ? 1 : 0);
                    var a = aufruf.ArgumentList.Arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.Text == par.Name)
                            ?? (ordinal >= 0 && ordinal < aufruf.ArgumentList.Arguments.Count && aufruf.ArgumentList.Arguments[ordinal].NameColon == null
                                ? aufruf.ArgumentList.Arguments[ordinal] : null);
                    if (a == null) continue;
                    var (key, def) = await HerkunftAsync(a.Expression, aufrufModel, tiefe + 1);
                    if (key.Length > 0) return (key, def.Length > 0 ? def : dflt);
                }
            return ("", dflt);
        }
        return ("", e.ToString());
    }

    // ── 6) Frist-Dauer = das Setting des Konfig-Felds im Fälligkeits-Ausdruck der planenden Pipeline ──
    private void LinkFristDauer(CompositionRoot cr)
    {
        for (var i = 0; i < cr.Frists.Count; i++)
        {
            if (!_kontextDauer.TryGetValue(cr.Frists[i].Kontext, out var kf)) continue;
            var setting = cr.HostSettings.FirstOrDefault(h => h.Konfig == kf.Konfig && h.Feld == kf.Feld);
            if (setting != null) cr.Frists[i] = cr.Frists[i] with { DauerSetting = setting.Name };
        }
    }

    // ── 7) Pipeline → injizierte Dienste (Konstruktor-Parameter, die einen Dienst-Vertrag treffen) ──
    private void ExtractPipelineDienste(CompositionRoot cr)
    {
        if (cr.Dienste.Count == 0) return;
        var vertraege = cr.Dienste.Select(d => d.Vertrag).ToHashSet(StringComparer.Ordinal);
        foreach (var pipe in _dom.Pipelines)
        {
            var t = _lage.Compilations.Select(c => c.GetTypeByMetadataName(pipe.Full)).FirstOrDefault(x => x != null);
            var ctor = t?.InstanceConstructors.OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
            if (ctor == null) continue;
            var used = ctor.Parameters.Select(p => p.Type.Name).Where(vertraege.Contains).Distinct().ToList();
            if (used.Count > 0) cr.PipelineDienste[pipe.Name] = used;
        }
    }

    // ── Helfer ───────────────────────────────────────────────────────────────────
    private SemanticModel Model(SyntaxTree tree) => _lage.Compilations.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    /// <summary>Aggregat-Name für einen Command aus der autoritativen Routing-Wahrheit.</summary>
    private string? AggregateOf(string cmdFull) => _routing.CommandToAggregate.TryGetValue(cmdFull, out var agg) ? agg : null;

    private string? PipelineHandling(string triggerFull) =>
        _dom.Pipelines.FirstOrDefault(p => p.Handles.Any(h => h.InputFull == triggerFull && h.InputKind == "trigger"))?.PipelineId;
}
