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
//    • Frist          = je Command TCmd, den eine Pipeline-Signatur als `Frist<TCmd>` (plant) bzw. `FristStorno<TCmd>`
//                       (storniert) im OneOf trägt — reiner Typ-Fakt; der Router ist generiert (GeneratedFristen).
//    • Ingress        = Aufruf einer Methode, die sich per [Ingress(Art, Ort = nameof(param))] als Trigger-Ingress
//                       deklariert (Webhook/Timer/Datei); Route/Intervall/Pfad = Argument des genannten Parameters,
//                       Nachricht = die IPipelineTrigger, die das übergebene Lambda liefert.
//    • Dienst         = DI-Registrierung (Microsoft-API) <Vertrag, Impl> mit Vertrag aus einer Domänen-Assembly.
//    • HostSetting    = bewusst NICHT mehr extrahiert: die Herkunft eines Werts ist Datenfluss durch Ausdrücke (ein
//                       Rumpf-Fakt, nicht verlässlich) — die Liste bleibt leer, der Editor kann Settings nur entwerfen.
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
    string? Path, string MsgName, string? TargetPipelineId)
{
    /// <summary>Die Anweisung verbatim + ihre Datei und die Texte von Typ-Argument und Ort-Argument darin (Vorlage für neue Bindungen).</summary>
    public string? Datei { get; init; }
    public string? Anweisung { get; init; }
    public string? TypArgument { get; init; }
    public string? OrtArgument { get; init; }
}
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
    private readonly INamedTypeSymbol? _iCommand, _iTrigger;

    public CompositionRootExtractor(Solution solution, Projektlage lage, RoutingTruth routing, DomainModel dom)
    {
        _solution = solution;
        _lage = lage;
        _routing = routing;
        _dom = dom;
        _hostQuellen = lage.Hosts.SelectMany(h => h.Compilation.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)).Select(t => (h.Compilation, t))).ToList();
        _domänenQuellen = lage.DomänenCompilations.SelectMany(c => c.SyntaxTrees.Where(t => !Projektlage.IstGeneriert(t)).Select(t => (c, t))).ToList();
        INamedTypeSymbol? Get(string n) => lage.Compilations.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iCommand = Get(Vertrag.ICommand);
        _iTrigger = Get(Vertrag.IPipelineTrigger);
    }

    public Task<CompositionRoot> ExtractAsync()
    {
        var cr = new CompositionRoot();
        ExtractDeadlines(cr);
        ExtractIngress(cr);
        ExtractServices(cr);
        ExtractPipelineDienste(cr);
        return Task.FromResult(cr);
    }

    // ── 1+2) Fristen: aus den Planungs-Ausgängen der Pipeline-Signaturen (Frist<TCmd> / FristStorno<TCmd>) ──
    private void ExtractDeadlines(CompositionRoot cr)
    {
        foreach (var pipe in _dom.Pipelines)
            foreach (var (input, v) in pipe.HandleVertraege)
                foreach (var a in v.Ausgaenge.Where(a => a.Art is "frist" or "fristStorno"))
                {
                    var f = cr.Frists.FirstOrDefault(x => x.Kontext == a.Full);
                    if (f == null)
                        cr.Frists.Add(f = new FristBinding(Name: a.Typ, Kontext: a.Full, Sendet: a.Typ, Aggregat: AggregateOf(a.Full),
                            Plant: new(), Storniert: new(), DauerSetting: null));
                    var ein = input[(input.LastIndexOf('.') + 1)..];
                    var liste = a.Art == "frist" ? f.Plant : f.Storniert;
                    if (!liste.Contains(ein)) liste.Add(ein);
                }
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
                // Die Vorlage: die ganze Anweisung (nur eine Ausdrucks-Anweisung), das Typ-Argument, das auf den Trigger zeigt,
                //   und das Ort-Argument — beides als Text, damit eine neue Bindung dieselbe Form bekommt.
                var anweisung = inv.FirstAncestorOrSelf<ExpressionStatementSyntax>();
                var typArg = (inv.Expression switch { MemberAccessExpressionSyntax { Name: GenericNameSyntax g } => g, GenericNameSyntax g => g, _ => null })
                    ?.TypeArgumentList.Arguments.FirstOrDefault(a => model.GetTypeInfo(a).Type is INamedTypeSymbol at && at.Fq() == msg.Fq());
                var ortArg = ortParam == null ? null : Argument(inv, m, ortParam);
                cr.Triggers.Add(new TriggerBinding(Name: msg.Name, Modus: modus,
                    Route: modus == Vertrag.IngressModus[(int)Abstractions.IngressArt.Webhook] ? ort : null,
                    Interval: modus == Vertrag.IngressModus[(int)Abstractions.IngressArt.Timer] ? ort : null,
                    Path: modus == Vertrag.IngressModus[(int)Abstractions.IngressArt.Datei] ? ort : null,
                    MsgName: msg.Name, TargetPipelineId: PipelineHandling(msg.Fq()))
                {
                    Datei = tree.FilePath, Anweisung = anweisung?.ToString(), TypArgument = typArg?.ToString(), OrtArgument = ortArg?.ToString(),
                });
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
