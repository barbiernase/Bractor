using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// PIPELINE ALS FLUSS lesen (docs/konzept-editor-pipelines.md §14). Nur Code-Fakten: die Klasse implementiert IPipeline, die
// Fluss-Property ruft PipelineFluss.Definiere, und jede Anweisung im Lambda ist ein Knoten — erkannt über die SYMBOLE der DSL
// (Vertrags-Assembly + DSL-Typ + Methodenname), nie über Text oder Namenskonventionen. Gelesen wird 1:1, was der Scaffolder
// schreibt (Fixpunkt): Name, Art, Typ, Eingänge (Drähte, ∧, Je-Element, Sammeln, ∨), Zeitlimit, Liste und die Lambdas verbatim.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class FlussRaw
{
    public string Name = "", Namespace = "", Bauer = "p";
    public string? Doku, Datei;
    public List<string> Usings = new();
    public List<FlussKnotenRaw> Knoten = new();
    /// <summary>Anweisungen, die kein Fluss-Knoten sind (werden nicht gelesen — gemeldet von der Parität).</summary>
    public List<string> Unbekannt = new();
}

public sealed class FlussKnotenRaw
{
    public string Name = "", Art = "", TypFull = "";
    public bool OhneVariable;
    public string? Zeitlimit, Liste;
    /// <summary>Nur Warte: die Typ-Argumente von <c>Warte&lt;A, B&gt;()</c> (FullName).</summary>
    public List<string> WarteAufFull = new();
    public List<FlussEingangRaw> Eingaenge = new();
}

public sealed class FlussEingangRaw
{
    public List<(string Von, string? FallFull, string Port)> Draehte = new();
    public string? Je;
    public bool Sammle;
    public string? Ausdruck;
}

public sealed partial class DomainExtractor
{
    private readonly INamedTypeSymbol? _iPipeline;

    private FlussRaw ReadFluss(INamedTypeSymbol t)
    {
        var fluss = new FlussRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq() };
        var classDecl = QuellDeklarationen(t).FirstOrDefault();
        if (classDecl != null)
        {
            fluss.Datei = classDecl.SyntaxTree.FilePath;
            fluss.Doku = Summary(classDecl);
            fluss.Usings = DateiUsings(classDecl).ToList();
        }

        // Die Fluss-Property über die Interface-IMPLEMENTIERUNG — nicht über ihren Namen am Typ.
        var iface = _iPipeline?.GetMembers(Vertrag.FlussProperty).OfType<IPropertySymbol>().FirstOrDefault();
        var impl = iface == null ? null : Sym.Implementierung(t, iface) as IPropertySymbol;
        var prop = impl?.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (prop == null) return fluss;
        var model = Model(prop.SyntaxTree);

        foreach (var inv in prop.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol ms || ms.Name != Vertrag.FlussDefiniere
                || ms.ContainingType?.Fq() != Vertrag.PipelineFlussTyp) continue;
            if (inv.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LambdaExpressionSyntax lambda) break;
            fluss.Bauer = lambda switch
            {
                SimpleLambdaExpressionSyntax s => s.Parameter.Identifier.Text,
                ParenthesizedLambdaExpressionSyntax pl when pl.ParameterList.Parameters.Count == 1 => pl.ParameterList.Parameters[0].Identifier.Text,
                _ => "p",
            };
            var anweisungen = lambda.Body is BlockSyntax block ? block.Statements.ToList() : new List<StatementSyntax>();
            var namen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var st in anweisungen)
            {
                string? name = null;
                ExpressionSyntax? ausdruck = null;
                if (st is LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } ld && ld.Declaration.Variables[0].Initializer is { } init)
                { name = ld.Declaration.Variables[0].Identifier.Text; ausdruck = init.Value; }
                else if (st is ExpressionStatementSyntax es) ausdruck = es.Expression;

                var knoten = ausdruck == null ? null : LiesKnoten(ausdruck, model);
                if (knoten == null) { fluss.Unbekannt.Add(st.ToString()); continue; }
                if (name == null)
                {
                    knoten.OhneVariable = true;
                    var basis = Klein(Einfach(knoten.TypFull));
                    name = basis;
                    for (var i = 2; namen.Contains(name); i++) name = basis + i;
                }
                knoten.Name = name;
                namen.Add(name);
                fluss.Knoten.Add(knoten);
            }
            break;
        }
        return fluss;
    }

    /// <summary>Ein Knoten-Ausdruck: die äußeren <c>.Zeitlimit(…)</c>/<c>.Oder(…)</c> abschälen, dann der Kern-Aufruf.</summary>
    private FlussKnotenRaw? LiesKnoten(ExpressionSyntax expr, SemanticModel model)
    {
        var oder = new List<FlussEingangRaw>();
        string? zeitlimit = null;
        while (expr is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma &&
               model.GetSymbolInfo(inv).Symbol is IMethodSymbol ms &&
               (Vertrag.IstFlussVerb(ms, Vertrag.FlussZeitlimit) || Vertrag.IstFlussVerb(ms, Vertrag.FlussOder)))
        {
            var args = inv.ArgumentList.Arguments;
            if (ms.Name == Vertrag.FlussZeitlimit) zeitlimit = args.FirstOrDefault()?.Expression.ToString();
            else if (args.Count == 2 && LiesEmpfänger(args[0].Expression, model) is { } e)
            {
                e.Ausdruck = Text(args[1].Expression);
                oder.Insert(0, e);
            }
            else return null;
            expr = ma.Expression;
        }

        if (expr is not InvocationExpressionSyntax kern || model.GetSymbolInfo(kern).Symbol is not IMethodSymbol k) return null;
        var typ = k.TypeArguments.FirstOrDefault()?.Fq() ?? "";
        var knoten = new FlussKnotenRaw { Zeitlimit = zeitlimit, TypFull = typ };

        if (Vertrag.IstFlussVerb(k, Vertrag.FlussQuelle)) knoten.Art = DomainEditor.FlussArt.Quelle;
        else if (Vertrag.IstFlussVerb(k, Vertrag.FlussAuf)) knoten.Art = DomainEditor.FlussArt.Auf;
        else if (Vertrag.IstFlussVerb(k, Vertrag.FlussJe) && kern.Expression is MemberAccessExpressionSyntax jm)
        {
            knoten.Art = DomainEditor.FlussArt.Je;
            knoten.Liste = Text(kern.ArgumentList.Arguments.FirstOrDefault()?.Expression);
            if (LiesEmpfänger(jm.Expression, model) is not { } q) return null;
            knoten.Eingaenge.Add(q);
        }
        else if (Vertrag.IstFlussVerb(k, Vertrag.FlussWarte) && kern.Expression is MemberAccessExpressionSyntax wm)
        {
            knoten.Art = DomainEditor.FlussArt.Warte;
            knoten.WarteAufFull = k.TypeArguments.Select(t => t.Fq()).ToList();
            if (LiesEmpfänger(wm.Expression, model) is not { } s) return null;
            knoten.Eingaenge.Add(s);
        }
        else if ((Vertrag.IstFlussVerb(k, Vertrag.FlussRufe) || Vertrag.IstFlussVerb(k, Vertrag.FlussSende))
                 && kern.Expression is MemberAccessExpressionSyntax rm)
        {
            knoten.Art = k.Name == Vertrag.FlussRufe ? DomainEditor.FlussArt.Funktion : DomainEditor.FlussArt.Command;
            if (LiesEmpfänger(rm.Expression, model) is not { } e) return null;
            e.Ausdruck = Text(kern.ArgumentList.Arguments.FirstOrDefault()?.Expression);
            knoten.Eingaenge.Add(e);
        }
        else return null;

        knoten.Eingaenge.AddRange(oder);
        return knoten;
    }

    /// <summary>Wovon ein Aufruf ausgeht: Knoten-Variable (Quelle / Je-Element), <c>x.Bei&lt;F&gt;()</c>, Fehler-Port, <c>p.Alle(…)</c>, <c>je.Sammle(…)</c>.</summary>
    private FlussEingangRaw? LiesEmpfänger(ExpressionSyntax e, SemanticModel model)
    {
        if (e is IdentifierNameSyntax id && model.GetSymbolInfo(id).Symbol is ILocalSymbol lokal)
        {
            var typ = lokal.Type.OriginalDefinition.MetadataName;
            if (typ == Vertrag.JeKnotenTyp) return new FlussEingangRaw { Je = id.Identifier.Text };
            if (typ == Vertrag.QuellKnotenTyp) return Ein((id.Identifier.Text, null, DomainEditor.FlussPort.Fall));
            return null;
        }
        if (e is not InvocationExpressionSyntax inv || model.GetSymbolInfo(inv).Symbol is not IMethodSymbol ms) return null;
        if (Vertrag.IstFlussVerb(ms, Vertrag.FlussAlle))
        {
            var r = new FlussEingangRaw();
            foreach (var a in inv.ArgumentList.Arguments)
                if (LiesDraht(a.Expression, model) is { } d) r.Draehte.Add(d); else return null;
            return r;
        }
        if (Vertrag.IstFlussVerb(ms, Vertrag.FlussSammle) && inv.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax je })
        {
            var r = new FlussEingangRaw { Je = je.Identifier.Text, Sammle = true };
            foreach (var a in inv.ArgumentList.Arguments)
                if (LiesDraht(a.Expression, model) is { } d) r.Draehte.Add(d); else return null;
            return r;
        }
        return LiesDraht(e, model) is { } einzel ? Ein(einzel) : null;
    }

    /// <summary>Ein Draht: <c>knoten.Bei&lt;Fall&gt;()</c>, <c>knoten.BeiZeitlimit()</c>, <c>knoten.BeiAbgelehnt()</c>, <c>quelle.Strom()</c> oder eine Quelle selbst.</summary>
    private static (string Von, string? FallFull, string Port)? LiesDraht(ExpressionSyntax e, SemanticModel model)
    {
        if (e is IdentifierNameSyntax id && model.GetSymbolInfo(id).Symbol is ILocalSymbol l
            && l.Type.OriginalDefinition.MetadataName == Vertrag.QuellKnotenTyp)
            return (id.Identifier.Text, null, DomainEditor.FlussPort.Fall);
        if (e is not InvocationExpressionSyntax inv || inv.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax von }
            || model.GetSymbolInfo(inv).Symbol is not IMethodSymbol ms) return null;
        if (Vertrag.IstFlussVerb(ms, Vertrag.FlussBei) && ms.TypeArguments.FirstOrDefault() is { } fall)
            return (von.Identifier.Text, fall.Fq(), DomainEditor.FlussPort.Fall);
        if (Vertrag.IstFlussVerb(ms, Vertrag.FlussBeiZeitlimit)) return (von.Identifier.Text, null, DomainEditor.FlussPort.Zeitlimit);
        if (Vertrag.IstFlussVerb(ms, Vertrag.FlussStrom)) return (von.Identifier.Text, null, DomainEditor.FlussPort.Strom);
        if (Vertrag.IstFlussVerb(ms, Vertrag.FlussBeiAbgelehnt)) return (von.Identifier.Text, null, DomainEditor.FlussPort.Abgelehnt);
        return null;
    }

    private static FlussEingangRaw Ein((string, string?, string) d) => new() { Draehte = { d } };
    private static string? Text(ExpressionSyntax? e) => e == null ? null : Dedent(e.ToString().Replace("\r\n", "\n"));
    private static string Einfach(string full) => full.Contains('.') ? full[(full.LastIndexOf('.') + 1)..] : full;
    private static string Klein(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
