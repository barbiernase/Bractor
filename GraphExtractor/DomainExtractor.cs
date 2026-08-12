using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// Zwischen-Repräsentation (raw), die der GraphBuilder zum Property-Graph zusammensetzt.

public sealed class DomainModel
{
    public List<AggregateRaw> Aggregates { get; } = new();
    public List<ProcessRaw> Processes { get; } = new();
    public List<ProjectionRaw> Projections { get; } = new();
    public List<QueryRaw> Queries { get; } = new();
    public List<ReaderRaw> Readers { get; } = new();
    public List<PipelineRaw> Pipelines { get; } = new();

    public HashSet<string> RegisteredProcesses { get; } = new(StringComparer.Ordinal);

    /// <summary>Event-FullName → (Simple, Persisted).</summary>
    public Dictionary<string, EventType> Events { get; } = new(StringComparer.Ordinal);
    /// <summary>Command-FullName → (Simple, IsCreation, Fields).</summary>
    public Dictionary<string, CommandType> Commands { get; } = new(StringComparer.Ordinal);
}

public sealed record EventType(string Simple, bool Persisted);
public sealed record CommandType(string Simple, bool IsCreation, List<FieldInfo> Fields);

public sealed class AggregateRaw
{
    public string Name = "", Namespace = "", Full = "";
    public List<FieldInfo> State = new();
    public List<string> HandlesCommandsFull = new();  // FullNames der entschiedenen Commands
    /// <summary>Ablehnungen (ITransientEvent) je Command — der Sad-Path, den GeneratedCommandRouting bewusst auslässt.</summary>
    public List<(string CmdFull, string EvtFull)> Rejects = new();

    /// <summary>Guard-Ausdruck je Zweig, Schlüssel <c>cmdFull|EvtSimpleName</c> — das „Warum" aus der Decider-Syntax.</summary>
    public Dictionary<string, string> Guards = new();
}

public sealed class ProcessRaw
{
    public string Name = "", Namespace = "";
    public string TriggerFull = "";
    public List<RuleRaw> Rules = new();
}

public sealed class RuleRaw
{
    public List<string> WhenFull = new();   // Bedingungs-Events (FullNames)
    public string Join = "single";          // single | and | count
    public string? SammelFull;
    public bool FanOut;
    public string SendsFull = "";
    public string? CompensatesFull;
}

public sealed class ProjectionRaw
{
    public string Name = "", Full = "", SubscriberId = "";
    public List<string> ConsumesFull = new();
}

public sealed class QueryRaw
{
    public string Name = "", Full = "";
    public List<FieldInfo> Fields = new();
}

public sealed class ReaderRaw
{
    public string Name = "", Full = "", ProjectionName = "";
    public List<string> QueryNames = new();
}

public sealed class PipelineRaw
{
    public string Name = "", Full = "", PipelineId = "";
    public List<(string InputFull, bool IsTrigger, List<string> EmitsFull)> Handles = new();
}

/// <summary>
/// Extrahiert das gesamte Domänen-Modell semantisch (über Marker-Interfaces, nicht textuell) — inklusive der
/// Sagas aus dem Prozess-DSL (der Kern-Neuwert gegenüber dem alten Extractor).
/// </summary>
public sealed class DomainExtractor
{
    private readonly List<Compilation> _comps;
    private readonly List<INamedTypeSymbol> _types;

    private readonly INamedTypeSymbol? _iState, _iCommand, _iCreation, _iEvent, _iTransient,
        _iSubscriber, _iReader, _iQuery, _iPipelineHandler, _iPipelineTrigger, _iProzessDef;

    private static readonly HashSet<string> RuleVerbs = new(StringComparer.Ordinal)
        { "Auf", "Und", "UndAlle", "Sende", "SendeJe", "RückgängigDurch", "RückgängigDurchJe" };

    public DomainExtractor(IEnumerable<Compilation> comps)
    {
        _comps = comps.ToList();
        _types = Sym.AllConcreteTypes(_comps);

        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iState = Get("Abstractions.IState");
        _iCommand = Get("Abstractions.ICommand");
        _iCreation = Get("Abstractions.ICreationCommand");
        _iEvent = Get("Abstractions.IEvent");
        _iTransient = Get("Abstractions.ITransientEvent");
        _iSubscriber = Get("Abstractions.ISubscriber");
        _iReader = Get("Abstractions.IReader`1");
        _iQuery = Get("Abstractions.IQuery");
        _iPipelineHandler = Get("Abstractions.IPipelineHandler");
        _iPipelineTrigger = Get("Abstractions.IPipelineTrigger");
        _iProzessDef = Get("Abstractions.IProzessDefinition");
    }

    private SemanticModel Model(SyntaxTree tree) =>
        _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    public DomainModel Extract()
    {
        var m = new DomainModel();

        CatalogEventsAndCommands(m);
        ExtractRegisteredProcesses(m);

        foreach (var t in _types)
        {
            if (Sym.Implements(t, _iState) && HasDecider(t)) m.Aggregates.Add(ReadAggregate(t));
            if (Sym.Implements(t, _iProzessDef)) m.Processes.Add(ReadProcess(t));
            if (Sym.Implements(t, _iSubscriber)) { var p = TryReadProjection(t); if (p != null) m.Projections.Add(p); }
            if (Sym.Implements(t, _iQuery)) m.Queries.Add(ReadQuery(t));
            if (Sym.Implements(t, _iPipelineHandler)) m.Pipelines.Add(ReadPipeline(t));
            var reader = TryReadReader(t);
            if (reader != null) m.Readers.Add(reader);
        }

        return m;
    }

    // ── Kataloge: alle Events + Commands mit Klassifikation ──────────────────

    private void CatalogEventsAndCommands(DomainModel m)
    {
        foreach (var t in _types)
        {
            if (Sym.Implements(t, _iEvent))
            {
                var persisted = !Sym.Implements(t, _iTransient);
                m.Events[t.Fq()] = new EventType(t.Name, persisted);
            }
            if (Sym.Implements(t, _iCommand))
            {
                var isCreation = Sym.Implements(t, _iCreation);
                m.Commands[t.Fq()] = new CommandType(t.Name, isCreation, CtorFields(t));
            }
        }
    }

    // ── Aggregate ────────────────────────────────────────────────────────────

    private static bool HasDecider(INamedTypeSymbol t) =>
        t.GetTypeMembers("Decider").Any() && t.GetTypeMembers("Applier").Any();

    private AggregateRaw ReadAggregate(INamedTypeSymbol t)
    {
        var agg = new AggregateRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq(), Full = t.Fq() };

        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.IsStatic || p.IsIndexer || p.DeclaredAccessibility != Accessibility.Public) continue;
            if (p.Name is "Id" or "Version") continue;
            agg.State.Add(new FieldInfo { Name = p.Name, Type = ShortType(p.Type) });
        }

        var decider = t.GetTypeMembers("Decider").First();
        foreach (var method in decider.GetMembers("Decide").OfType<IMethodSymbol>())
        {
            if (method.Parameters.Length < 1 || method.Parameters[0].Type is not INamedTypeSymbol cmd || !Sym.Implements(cmd, _iCommand))
                continue;
            agg.HandlesCommandsFull.Add(cmd.Fq());
            foreach (var reject in UniverseEvents(method.ReturnType, onlyTransient: true))
                agg.Rejects.Add((cmd.Fq(), reject));
            foreach (var (evtSimple, guard) in ExtractGuards(method))
                agg.Guards[cmd.Fq() + "|" + evtSimple] = guard;
        }
        return agg;
    }

    /// <summary>
    /// Hebt je <c>yield return new Event(…)</c> den Ausdruck des umschließenden <c>if</c> als Guard —
    /// das „Warum" dieses Zweigs, statisch aus der Syntax. Der ungeschützte Sonst-Zweig (Erfolg) hat keinen.
    /// </summary>
    private static IEnumerable<(string EvtSimple, string Guard)> ExtractGuards(IMethodSymbol method)
    {
        if (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax syntax)
            yield break;
        var body = (SyntaxNode?)syntax.Body ?? syntax.ExpressionBody;
        if (body == null) yield break;

        foreach (var y in body.DescendantNodes().OfType<YieldStatementSyntax>())
        {
            if (y.Expression is not ObjectCreationExpressionSyntax oc) continue;
            var ifs = y.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
            if (ifs == null) continue; // ungeschützter Sonst-Zweig → kein Guard
            var evtSimple = oc.Type.ToString().Split('.').Last();
            yield return (evtSimple, ifs.Condition.ToString().Replace("this.", ""));
        }
    }

    // ── Prozesse / Sagas: der DSL-Walk ───────────────────────────────────────

    private ProcessRaw ReadProcess(INamedTypeSymbol t)
    {
        var proc = new ProcessRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq() };

        var propSyntax = t.GetMembers("Regeln").OfType<IPropertySymbol>()
            .SelectMany(p => p.DeclaringSyntaxReferences)
            .Select(r => r.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault();
        if (propSyntax == null) return proc;

        var model = Model(propSyntax.SyntaxTree);

        // Auslöser: das Typ-Argument von Prozess<T>.
        var prozessGeneric = propSyntax.DescendantNodes().OfType<GenericNameSyntax>()
            .FirstOrDefault(g => g.Identifier.Text == "Prozess");
        if (prozessGeneric != null)
            proc.TriggerFull = ResolveArg(prozessGeneric, model) ?? "";

        // Jede Anweisung im Definiere-Lambda ist eine Regel (Transition). Das Definiere-Lambda `p => { … }`
        // ist eine SIMPLE-Lambda (kein Klammer-Lambda) — wir erkennen es am Block-Body (die inneren
        // Sende-Lambdas haben Expression-Bodies).
        var lambdaBody = propSyntax.DescendantNodes().OfType<LambdaExpressionSyntax>()
            .Select(l => l.Body).OfType<BlockSyntax>().FirstOrDefault();
        var statements = lambdaBody?.Statements.OfType<ExpressionStatementSyntax>()
            ?? Enumerable.Empty<ExpressionStatementSyntax>();

        foreach (var stmt in statements)
        {
            var rule = ReadRule(stmt, model);
            if (rule != null) proc.Rules.Add(rule);
        }
        return proc;
    }

    private RuleRaw? ReadRule(ExpressionStatementSyntax stmt, SemanticModel model)
    {
        // Geordnete Verb-Aufrufe der Fluent-Kette (Auf → Und… → Sende → RückgängigDurch).
        var verbs = stmt.DescendantNodes().OfType<GenericNameSyntax>()
            .Where(g => RuleVerbs.Contains(g.Identifier.Text))
            .OrderBy(g => g.SpanStart)
            .ToList();
        if (verbs.Count == 0) return null;

        var rule = new RuleRaw();
        foreach (var v in verbs)
        {
            var arg = ResolveArg(v, model);
            if (arg == null) continue;
            switch (v.Identifier.Text)
            {
                case "Auf":
                case "Und":
                    rule.WhenFull.Add(arg);
                    break;
                case "UndAlle":
                    rule.Join = "count";
                    rule.SammelFull = arg;
                    break;
                case "Sende":
                    rule.SendsFull = arg;
                    break;
                case "SendeJe":
                    rule.SendsFull = arg;
                    rule.FanOut = true;
                    break;
                case "RückgängigDurch":
                case "RückgängigDurchJe":
                    rule.CompensatesFull = arg;
                    break;
            }
        }
        if (rule.Join != "count")
            rule.Join = rule.WhenFull.Count > 1 ? "and" : "single";
        return string.IsNullOrEmpty(rule.SendsFull) ? null : rule;
    }

    private static string? ResolveArg(GenericNameSyntax g, SemanticModel model)
    {
        var arg = g.TypeArgumentList.Arguments.FirstOrDefault();
        if (arg == null) return null;
        return model.GetTypeInfo(arg).Type is INamedTypeSymbol s ? s.Fq() : null;
    }

    private void ExtractRegisteredProcesses(DomainModel m)
    {
        var reg = _comps.Select(c => c.GetTypeByMetadataName("Domain.Prozess.GeneratedProzessRegeln"))
            .FirstOrDefault(x => x != null);
        var syntax = reg?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax == null) return;

        // Schlüssel sind String-Literale in [ "Name" ] = new X().Regeln
        foreach (var asg in syntax.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (asg.Left is ImplicitElementAccessSyntax access &&
                access.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax lit &&
                lit.Token.Value is string name)
                m.RegisteredProcesses.Add(name);
        }
    }

    // ── Projektionen / Queries / Reader / Pipelines ──────────────────────────

    private ProjectionRaw? TryReadProjection(INamedTypeSymbol t)
    {
        var handles = t.GetMembers("Handle").OfType<IMethodSymbol>()
            .Where(mm => mm.Parameters.Length >= 1 && Sym.Implements(mm.Parameters[0].Type, _iEvent))
            .Select(mm => mm.Parameters[0].Type.Fq())
            .Distinct().ToList();
        if (handles.Count == 0) return null;

        var subId = t.GetMembers("SubscriberId").OfType<IPropertySymbol>()
            .SelectMany(p => p.DeclaringSyntaxReferences).Select(r => r.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .Select(p => p.Initializer?.Value?.ToString() ?? p.ExpressionBody?.Expression.ToString())
            .FirstOrDefault()?.Trim('"') ?? t.Name;

        return new ProjectionRaw { Name = t.Name, Full = t.Fq(), SubscriberId = subId, ConsumesFull = handles };
    }

    private QueryRaw ReadQuery(INamedTypeSymbol t) =>
        new() { Name = t.Name, Full = t.Fq(), Fields = CtorFields(t) };

    private ReaderRaw? TryReadReader(INamedTypeSymbol t)
    {
        var readerIface = t.AllInterfaces.FirstOrDefault(i => _iReader != null && i.OriginalDefinition.Fq() == _iReader.Fq());
        if (readerIface == null || readerIface.TypeArguments.Length != 1) return null;

        var queries = t.GetMembers("Handle").OfType<IMethodSymbol>()
            .Where(mm => mm.Parameters.Length >= 1 && Sym.Implements(mm.Parameters[0].Type, _iQuery))
            .Select(mm => mm.Parameters[0].Type.Name)
            .Distinct().ToList();

        return new ReaderRaw { Name = t.Name, Full = t.Fq(), ProjectionName = readerIface.TypeArguments[0].Name, QueryNames = queries };
    }

    private PipelineRaw ReadPipeline(INamedTypeSymbol t)
    {
        var pipe = new PipelineRaw { Name = t.Name, Full = t.Fq() };
        pipe.PipelineId = t.GetMembers("PipelineId").OfType<IPropertySymbol>()
            .SelectMany(p => p.DeclaringSyntaxReferences).Select(r => r.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .Select(p => p.Initializer?.Value?.ToString() ?? p.ExpressionBody?.Expression.ToString())
            .FirstOrDefault()?.Trim('"') ?? t.Name;

        foreach (var method in t.GetMembers("Handle").OfType<IMethodSymbol>())
        {
            if (method.Parameters.Length < 1 || method.Parameters[0].Type is not INamedTypeSymbol input) continue;
            var isTrigger = Sym.Implements(input, _iPipelineTrigger);
            var emits = EmittedCommands(method).ToList();
            pipe.Handles.Add((input.Fq(), isTrigger, emits));
        }
        return pipe;
    }

    private IEnumerable<string> EmittedCommands(IMethodSymbol method)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) yield break;
        var node = syntaxRef.GetSyntax();
        var model = Model(node.SyntaxTree);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var creation in node.DescendantNodes())
        {
            ITypeSymbol? created = creation switch
            {
                ObjectCreationExpressionSyntax oc => model.GetTypeInfo(oc).Type,
                ImplicitObjectCreationExpressionSyntax ic => model.GetTypeInfo(ic).ConvertedType,
                _ => null
            };
            if (created is INamedTypeSymbol nt && Sym.Implements(nt, _iCommand) && seen.Add(nt.Fq()))
                yield return nt.Fq();
        }
    }

    // ── Helfer ───────────────────────────────────────────────────────────────

    private static List<FieldInfo> CtorFields(INamedTypeSymbol t)
    {
        var ctor = t.InstanceConstructors.Where(c => c.Parameters.Length > 0)
            .OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
        return ctor?.Parameters.Select(p => new FieldInfo { Name = p.Name, Type = ShortType(p.Type) }).ToList()
            ?? new List<FieldInfo>();
    }

    private static string ShortType(ITypeSymbol t) =>
        t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    /// <summary>Event-Typen aus einem Decide-Rückgabetyp <c>IEnumerable&lt;OneOf&lt;…&gt;&gt;</c> / <c>IEnumerable&lt;E&gt;</c>.</summary>
    private IEnumerable<string> UniverseEvents(ITypeSymbol returnType, bool onlyTransient)
    {
        if (returnType is not INamedTypeSymbol enumerable || enumerable.TypeArguments.Length != 1) yield break;
        if (enumerable.TypeArguments[0] is not INamedTypeSymbol element) yield break;

        var candidates = element.TypeArguments.Length > 0
            ? element.TypeArguments.ToList()
            : new List<ITypeSymbol> { element };

        foreach (var ta in candidates)
        {
            if (ta is not INamedTypeSymbol et || !Sym.Implements(et, _iEvent)) continue;
            var transient = Sym.Implements(et, _iTransient);
            if (onlyTransient == transient) yield return et.Fq();
        }
    }
}
