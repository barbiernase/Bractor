using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Domain.SourceGeneration
{
    /// <summary>
    /// Wurzel-2, Schritt 3: zwei COMPILE-ZEIT-Diagnostiken, die stille Verdrahtungs-Bugs der Prozess-Schicht
    /// in Build-Meldungen verwandeln. Syntax-basiert → gehört in Domain.SourceGeneration (hier liegt der
    /// Prozess-Quelltext; ein Infrastructure-Generator sähe nur Symbole, keine Regeln-Syntax).
    ///
    /// CQRS001 — Doppelter Prozess-Auslöser: zwei <c>IProzessDefinition</c> mit demselben
    ///   <c>Prozess&lt;TAuslöser&gt;</c>. Zur Laufzeit überschreibt <c>auslöserZuProzess[typ] = name</c>
    ///   (ProzessManagerWiring) den ersten still → einer der Prozesse startet nie.
    ///
    /// CQRS002 — Dangling Command: ein gesendeter Command-Typ, den KEIN Aggregat-<c>IDecider&lt;TState&gt;.Decide(Cmd)</c>
    ///   behandelt → der Send verpufft zur Laufzeit still. Der Command-Typ kommt aus dem TYP-ARGUMENT der
    ///   Emissions-Methode (<c>Sende&lt;TCmd&gt;</c>/<c>SendeJe&lt;TCmd&gt;</c>/<c>RückgängigDurch&lt;TCmd&gt;</c>) —
    ///   NICHT aus dem <c>new</c> im Lambda (das läge hinter einer Factory verborgen). Der Typ fließt durch die
    ///   generische Signatur, exakt wie <c>OneOf&lt;…&gt;</c> auf <c>Decide</c>. Ist <c>TCmd</c> auf die Basis
    ///   <c>ICommand</c> gelöscht (Lambda gibt den Basistyp zurück), ist er nicht bestimmbar → übersprungen
    ///   (Anwender-Vertrag, wie eine nackte <c>IEnumerable&lt;IEvent&gt;</c>-Decide-Signatur).
    ///
    /// CQRS003 — Expliziter Command-Typ: jede Emissions-Methode (Sende/SendeJe/RückgängigDurch/RückgängigDurchJe)
    ///   MUSS ihren Command-Typ explizit als Typ-Argument tragen (<c>.Sende&lt;X&gt;(…)</c>). Ein Inferenz-Aufruf
    ///   (<c>.Sende(e =&gt; …)</c>) leitet den Typ aus dem Lambda-BODY ab — das ist body-abhängig und bei einer
    ///   Factory nicht bestimmbar → Fehler. Erst mit explizitem Typ steht CQRS002 body-frei auf sicherem Grund.
    ///
    /// „Anwender-Vertrag" (OneOf-/TCmd-Präzision, Azyklizität) wird NICHT erzwungen — nur mechanische Verdrahtung.
    /// </summary>
    [Generator]
    public class ProzessRegelDiagnosticGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor DoppelterAusloeser = new(
            id: "CQRS001",
            title: "Doppelter Prozess-Auslöser",
            messageFormat: "Prozess '{0}' teilt seinen Auslöser-Typ '{1}' mit '{2}' — der zweite überschreibt den ersten still (auslöserZuProzess last-wins); dasselbe Event kann nur EINEN Prozess starten",
            category: "CqrsProzess",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor DanglingCommand = new(
            id: "CQRS002",
            title: "Command ohne Decider-Handler",
            messageFormat: "Command '{0}' wird von Prozess '{1}' gesendet, aber von keinem Aggregat-Decider (IDecider<TState>.Decide) behandelt — der Send verpufft zur Laufzeit still",
            category: "CqrsProzess",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor ExpliziterTyp = new(
            id: "CQRS003",
            title: "Command-Typ muss explizit angegeben werden",
            messageFormat: "'{0}' ohne expliziten Command-Typ — schreibe {0}<Command>(…). Der Command-Typ muss aus der Signatur feststehen (body-frei), nicht aus dem Lambda inferiert werden",
            category: "CqrsProzess",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // Die typ-tragenden Emissions-Methoden der Fluent-DSL (alle generisch: <TCmd>).
        private static readonly HashSet<string> EmissionsMethoden =
            new() { "Sende", "SendeJe", "RückgängigDurch", "RückgängigDurchJe" };

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var comp = context.Compilation;
            var iDef = comp.GetTypeByMetadataName("Abstractions.IProzessDefinition");
            var iDecider = comp.GetTypeByMetadataName("Abstractions.IDecider`1");
            var iCommand = comp.GetTypeByMetadataName("Abstractions.ICommand");
            if (iDef == null || iDecider == null || iCommand == null)
                return;

            var allTypes = new List<INamedTypeSymbol>();
            CollectNested(comp.GlobalNamespace, allTypes);

            // 1. Command-Typen, die ein Aggregat-Decider WIRKLICH behandelt (Decide-Parametertyp).
            var behandelt = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var t in allTypes)
            {
                var deciderIface = t.AllInterfaces.FirstOrDefault(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDecider));
                if (deciderIface == null)
                    continue;
                foreach (var m in t.GetMembers("Decide").OfType<IMethodSymbol>())
                    if (m.Parameters.Length >= 1 &&
                        m.Parameters[0].Type is INamedTypeSymbol cmd &&
                        cmd.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default))
                        behandelt.Add(cmd);
            }

            // 2. Prozess-Definitionen: Auslöser + gesendete Commands aus der Regeln-Syntax.
            var defs = allTypes.Where(t =>
                t.TypeKind == TypeKind.Class && !t.IsAbstract &&
                t.AllInterfaces.Contains(iDef, SymbolEqualityComparer.Default));

            var proProzess = new Dictionary<INamedTypeSymbol, List<(string Name, Location Loc)>>(SymbolEqualityComparer.Default);

            foreach (var def in defs)
            {
                var regeln = def.GetMembers("Regeln").OfType<IPropertySymbol>().FirstOrDefault();
                var syntaxRef = regeln?.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef == null)
                    continue;

                var node = syntaxRef.GetSyntax();
                var model = comp.GetSemanticModel(node.SyntaxTree);
                var defLoc = def.Locations.FirstOrDefault() ?? Location.None;
                var ausloeserGefunden = false;

                foreach (var inv in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (inv.Expression is not MemberAccessExpressionSyntax ma)
                        continue;
                    var methodenName = ma.Name.Identifier.Text;

                    // 2a. Auslöser-Typ: die Prozess<X>.Definiere(...)-Invocation → ContainingType Prozess<X> → X.
                    if (!ausloeserGefunden && methodenName == "Definiere" &&
                        model.GetSymbolInfo(inv).Symbol is IMethodSymbol dm &&
                        dm.ContainingType is INamedTypeSymbol prozessTyp &&
                        prozessTyp.TypeArguments.Length == 1 &&
                        prozessTyp.TypeArguments[0] is INamedTypeSymbol ausloeser)
                    {
                        if (!proProzess.TryGetValue(ausloeser, out var list))
                            proProzess[ausloeser] = list = new List<(string, Location)>();
                        list.Add((def.Name, defLoc));
                        ausloeserGefunden = true;
                        continue;
                    }

                    // 2b. Emissions-Methode: der Command-Typ MUSS explizit als Typ-Argument stehen — body-frei,
                    //     KEINE Inferenz aus dem Lambda. Fehlt es → CQRS003. Steht es → aus der Syntax lesen.
                    if (!EmissionsMethoden.Contains(methodenName))
                        continue;
                    if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol em ||
                        em.ContainingNamespace?.ToDisplayString() != "Abstractions")
                        continue; // nur die Prozess-DSL, keine gleichnamige Fremd-Methode

                    if (ma.Name is not GenericNameSyntax gen || gen.TypeArgumentList.Arguments.Count != 1)
                    {
                        // Inferenz-Aufruf: der Typ hinge am Lambda-Body → verboten (CQRS003).
                        context.ReportDiagnostic(Diagnostic.Create(ExpliziterTyp, inv.GetLocation(), methodenName));
                        continue;
                    }

                    // Typ AUS DEM EXPLIZITEN TYP-ARGUMENT (reine Typreferenz, kein Body, keine Inferenz).
                    var typArg = gen.TypeArgumentList.Arguments[0];
                    if (model.GetTypeInfo(typArg).Type is not INamedTypeSymbol tcmd ||
                        !tcmd.AllInterfaces.Contains(iCommand, SymbolEqualityComparer.Default))
                        continue;
                    if (!behandelt.Contains(tcmd))
                        context.ReportDiagnostic(Diagnostic.Create(DanglingCommand, typArg.GetLocation(), tcmd.Name, def.Name));
                }
            }

            // 3. Doppelter Auslöser: derselbe Typ von mehr als einem Prozess.
            foreach (var kv in proProzess.Where(k => k.Value.Count > 1))
            {
                foreach (var (name, loc) in kv.Value)
                {
                    var andere = string.Join(", ", kv.Value.Select(v => v.Name).Where(n => n != name));
                    context.ReportDiagnostic(Diagnostic.Create(DoppelterAusloeser, loc, name, kv.Key.Name, andere));
                }
            }
        }

        private static void CollectNested(INamespaceSymbol ns, List<INamedTypeSymbol> acc)
        {
            foreach (var t in ns.GetTypeMembers())
                AddRec(t, acc);
            foreach (var child in ns.GetNamespaceMembers())
                CollectNested(child, acc);
        }

        private static void AddRec(INamedTypeSymbol t, List<INamedTypeSymbol> acc)
        {
            if (t.TypeKind == TypeKind.Class && !t.IsAbstract)
                acc.Add(t);
            foreach (var nested in t.GetTypeMembers())
                AddRec(nested, acc);
        }
    }
}
