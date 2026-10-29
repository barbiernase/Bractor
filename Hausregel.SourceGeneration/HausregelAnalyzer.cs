using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hausregel.SourceGeneration
{
    /// <summary>
    /// Die Hausregel für Actors als Build-Prüfung (R18, Plan Pub/Sub-Neustrukturierung §1.7, Phase 10): ein Weg je Absicht im Actor.
    /// Geprüft werden alle Typen, die <c>Proto.IActor</c> implementieren, und alle Typen, die ein solcher Actor besitzt — als Feld, als
    /// Parameter seines primären Konstruktors, als Typ-Argument eines Feldes, transitiv, soweit sie im selben Projekt stehen. Darin verboten
    /// (<b>CQRS112</b>, Fehler):
    /// <list type="bullet">
    /// <item><b>eigene Fäden</b> — <c>Task.Run</c>, <c>TaskFactory.StartNew</c>, <c>Parallel</c>, <c>ThreadPool</c>, <c>new Thread</c>:
    ///   Parallelität kommt nur von mehr Actors, lange I/O läuft neben der Mailbox (<c>ReenterAfter</c>, im Rahmen <c>Neben</c>);</item>
    /// <item><b><c>lock</c></b> — ein Actor hat nur einen Faden zugleich, ein Schloss verdeckt einen zweiten;</item>
    /// <item><b>Feuern und Vergessen</b> — <c>_ = …Async(…)</c> oder ein nicht abgewarteter Task als Anweisung: die Arbeit überlebt den
    ///   Turn ohne Rückkehr in den Actor, ihr Fehler geht verloren, Stopp und Passivierung sehen sie nicht;</item>
    /// <item><b><c>ContinueWith</c></b> — die Fortsetzung läuft auf einem fremden Faden, am Actor vorbei;</item>
    /// <item><b>Blockieren</b> — <c>.Result</c>, <c>.Wait()</c>, <c>GetAwaiter().GetResult()</c>; erlaubt ist nur das Ergebnis eines
    ///   erwiesen fertigen Tasks: <c>t is Task&lt;T&gt; { IsCompletedSuccessfully: true } a ? a.Result : …</c>;</item>
    /// <item><b>rohe Cluster-API</b> — Methoden von <c>Proto.Cluster.Cluster</c> (auch Erweiterungen) und jedes <c>RequestAsync</c> von Proto: Cluster-Nachrichten
    ///   gehen nur über den Boten (<c>Melde</c>, <c>FrageAsync</c> mit Pflicht-Frist).</item>
    /// </list>
    /// Eine Ausnahme gibt es nur mit <c>[HausregelAusnahme("Begründung")]</c> am Member oder am Typ; eine leere Begründung ist selbst ein
    /// Fehler (<b>CQRS113</b>).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class HausregelAnalyzer : DiagnosticAnalyzer
    {
        public const string VerstossId = "CQRS112";
        public const string OhneBegruendungId = "CQRS113";

        private const string Kategorie = "CQRS.Hausregel";
        private const string AusnahmeName = "Infrastructure.Lebenszyklus.HausregelAusnahmeAttribute";

        private static readonly DiagnosticDescriptor Verstoss = new(
            VerstossId,
            "Hausregel für Actors verletzt",
            "{0} in '{1}' — {2} (Hausregel R18, Plan §1.7; Ausnahme nur mit [HausregelAusnahme(\"Begründung\")])",
            Kategorie,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Ein Weg je Absicht im Actor: keine eigenen Fäden, kein lock, kein Feuern und Vergessen, kein ContinueWith, kein " +
                         "Blockieren, keine rohe Cluster-API — in Actors und in den Typen, die sie besitzen.");

        private static readonly DiagnosticDescriptor OhneBegruendung = new(
            OhneBegruendungId,
            "Ausnahme von der Hausregel ohne Begründung",
            "[HausregelAusnahme] an '{0}' braucht eine Begründung",
            Kategorie,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Verstoss, OhneBegruendung);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var actor = start.Compilation.GetTypeByMetadataName("Proto.IActor");
                if (actor is null) return;
                var ausnahme = start.Compilation.GetTypeByMetadataName(AusnahmeName);
                var geprueft = GepruefteTypen(start.Compilation, actor);
                if (geprueft.Count == 0) return;
                var pruefung = new Pruefung(geprueft, ausnahme);
                start.RegisterSyntaxNodeAction(pruefung.Aufruf, SyntaxKind.InvocationExpression);
                start.RegisterSyntaxNodeAction(pruefung.Neu, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
                start.RegisterSyntaxNodeAction(pruefung.Schloss, SyntaxKind.LockStatement);
                start.RegisterSyntaxNodeAction(pruefung.Verworfen, SyntaxKind.SimpleAssignmentExpression);
                start.RegisterSyntaxNodeAction(pruefung.Anweisung, SyntaxKind.ExpressionStatement);
                start.RegisterSyntaxNodeAction(pruefung.Ergebnis, SyntaxKind.SimpleMemberAccessExpression);
                if (ausnahme is not null) start.RegisterSymbolAction(s => pruefung.Begruendung(s, ausnahme),
                    SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property);
            });
        }

        // ─── Welche Typen gelten: die Actors und, transitiv, was sie besitzen ───

        private static HashSet<INamedTypeSymbol> GepruefteTypen(Compilation c, INamedTypeSymbol actor)
        {
            var quelle = AlleTypen(c.Assembly.GlobalNamespace).Where(t => t.Locations.Any(l => l.IsInSource)).ToList();
            var geprueft = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var offen = new Stack<INamedTypeSymbol>();
            foreach (var t in quelle)
                if (t.TypeKind == TypeKind.Class && t.AllInterfaces.Contains(actor, SymbolEqualityComparer.Default)) offen.Push(t);

            while (offen.Count > 0)
            {
                var t = offen.Pop();
                if (!geprueft.Add(t)) continue;
                if (t.BaseType is { } basis && Eigen(basis, c)) offen.Push(basis.OriginalDefinition);
                foreach (var besessen in Besessen(t))
                    foreach (var e in Eigene(besessen, c)) offen.Push(e);
            }
            return geprueft;
        }

        /// <summary>Was ein Typ besitzt: die Typen seiner Instanz-Felder (auch hinter Auto-Properties) und seines primären Konstruktors.</summary>
        private static IEnumerable<ITypeSymbol> Besessen(INamedTypeSymbol t)
        {
            foreach (var m in t.GetMembers())
            {
                if (m is IFieldSymbol { IsStatic: false, IsConst: false } f) yield return f.Type;
                if (m is IMethodSymbol { MethodKind: MethodKind.Constructor } k && IstPrimaer(k))
                    foreach (var p in k.Parameters) yield return p.Type;
            }
        }

        private static bool IstPrimaer(IMethodSymbol k) =>
            k.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is TypeDeclarationSyntax);

        /// <summary>Die Typen dieses Projekts in einem Feld-Typ: er selbst, seine Typ-Argumente, der Element-Typ eines Arrays.</summary>
        private static IEnumerable<INamedTypeSymbol> Eigene(ITypeSymbol typ, Compilation c)
        {
            switch (typ)
            {
                case IArrayTypeSymbol a:
                    foreach (var e in Eigene(a.ElementType, c)) yield return e;
                    break;
                case INamedTypeSymbol n:
                    if (Eigen(n, c) && n.TypeKind is TypeKind.Class or TypeKind.Struct) yield return n.OriginalDefinition;
                    foreach (var arg in n.TypeArguments)
                        foreach (var e in Eigene(arg, c)) yield return e;
                    break;
            }
        }

        private static bool Eigen(INamedTypeSymbol t, Compilation c) =>
            SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, c.Assembly) && t.Locations.Any(l => l.IsInSource);

        private static IEnumerable<INamedTypeSymbol> AlleTypen(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers())
                foreach (var x in MitGeschachtelten(t)) yield return x;
            foreach (var inner in ns.GetNamespaceMembers())
                foreach (var t in AlleTypen(inner)) yield return t;
        }

        private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
        {
            yield return t;
            foreach (var n in t.GetTypeMembers())
                foreach (var x in MitGeschachtelten(n)) yield return x;
        }

        // ─── Die Verbots-Muster ───

        private sealed class Pruefung(HashSet<INamedTypeSymbol> geprueft, INamedTypeSymbol? ausnahme)
        {
            public void Aufruf(SyntaxNodeAnalysisContext ctx)
            {
                var aufruf = (InvocationExpressionSyntax)ctx.Node;
                if (ctx.SemanticModel.GetSymbolInfo(aufruf, ctx.CancellationToken).Symbol is not IMethodSymbol m) return;
                var typ = m.ContainingType;
                var name = m.Name;

                if (Ist(typ, "System.Threading.Tasks.Task") && name == "Run"
                    || Ist(typ, "System.Threading.Tasks.TaskFactory") && name == "StartNew"
                    || Ist(typ, "System.Threading.Tasks.Parallel")
                    || Ist(typ, "System.Threading.ThreadPool"))
                    Melde(ctx, aufruf, $"{typ.Name}.{name}", "Parallelität kommt nur von mehr Actors, lange I/O läuft neben der Mailbox (Neben/ReenterAfter)");
                else if (name == "ContinueWith" && IstTask(typ))
                    Melde(ctx, aufruf, "ContinueWith", "die Fortsetzung läuft am Actor vorbei; Neben/ReenterAfter kehrt in den Actor zurück");
                else if (name == "Wait" && IstTask(typ))
                    Melde(ctx, aufruf, ".Wait()", "Blockieren hält die Mailbox an; abwarten mit await oder neben der Mailbox");
                else if (name == "GetResult" && typ.Name is "TaskAwaiter" or "ValueTaskAwaiter")
                    Melde(ctx, aufruf, "GetAwaiter().GetResult()", "Blockieren hält die Mailbox an; abwarten mit await oder neben der Mailbox");
                else if (Ist(typ, "Proto.Cluster.Cluster") || Ist(m.ReceiverType as INamedTypeSymbol, "Proto.Cluster.Cluster")
                         || name == "RequestAsync" && Namensraum(typ).StartsWith("Proto"))
                    Melde(ctx, aufruf, $"{typ.Name}.{name}", "Cluster-Nachrichten nur über den Boten (Melde, FrageAsync mit Pflicht-Frist)");
            }

            public void Neu(SyntaxNodeAnalysisContext ctx)
            {
                if (ctx.SemanticModel.GetTypeInfo(ctx.Node, ctx.CancellationToken).Type is INamedTypeSymbol t && Ist(t, "System.Threading.Thread"))
                    Melde(ctx, ctx.Node, "new Thread", "Parallelität kommt nur von mehr Actors");
            }

            public void Schloss(SyntaxNodeAnalysisContext ctx) =>
                Melde(ctx, ((LockStatementSyntax)ctx.Node).LockKeyword, "lock", "ein Actor hat einen Faden zugleich; ein Schloss verdeckt einen zweiten");

            public void Verworfen(SyntaxNodeAnalysisContext ctx)
            {
                var zuweisung = (AssignmentExpressionSyntax)ctx.Node;
                if (ctx.SemanticModel.GetOperation(zuweisung.Left, ctx.CancellationToken) is not Microsoft.CodeAnalysis.Operations.IDiscardOperation) return;
                if (!IstTask(ctx.SemanticModel.GetTypeInfo(zuweisung.Right, ctx.CancellationToken).Type)) return;
                Melde(ctx, zuweisung, "_ = …Async", "Feuern und Vergessen: Fehler gehen verloren, Stopp und Passivierung sehen die Arbeit nicht (Neben/ReenterAfter)");
            }

            public void Anweisung(SyntaxNodeAnalysisContext ctx)
            {
                var anweisung = (ExpressionStatementSyntax)ctx.Node;
                if (anweisung.Expression is not InvocationExpressionSyntax) return;
                if (!IstTask(ctx.SemanticModel.GetTypeInfo(anweisung.Expression, ctx.CancellationToken).Type)) return;
                Melde(ctx, anweisung.Expression, "nicht abgewarteter Task", "Feuern und Vergessen: Fehler gehen verloren, Stopp und Passivierung sehen die Arbeit nicht (Neben/ReenterAfter)");
            }

            public void Ergebnis(SyntaxNodeAnalysisContext ctx)
            {
                var zugriff = (MemberAccessExpressionSyntax)ctx.Node;
                if (zugriff.Name.Identifier.ValueText != "Result") return;
                if (ctx.SemanticModel.GetSymbolInfo(zugriff, ctx.CancellationToken).Symbol is not IPropertySymbol p || !IstTask(p.ContainingType)) return;
                if (ErwiesenFertig(zugriff.Expression, ctx.SemanticModel, ctx)) return;
                Melde(ctx, zugriff, ".Result", "Blockieren hält die Mailbox an; erlaubt nur nach „t is Task<T> { IsCompletedSuccessfully: true } a“");
            }

            /// <summary>
            /// Ist der Empfänger eine Variable aus einem Muster, das den Task als fertig ausweist (<c>{ IsCompletedSuccessfully: true }</c>
            /// oder <c>{ IsCompleted: true }</c>)? Dann blockiert <c>.Result</c> nicht.
            /// </summary>
            private static bool ErwiesenFertig(ExpressionSyntax empfaenger, SemanticModel modell, SyntaxNodeAnalysisContext ctx)
            {
                if (modell.GetSymbolInfo(empfaenger, ctx.CancellationToken).Symbol is not ILocalSymbol lokal) return false;
                foreach (var r in lokal.DeclaringSyntaxReferences)
                {
                    if (r.GetSyntax(ctx.CancellationToken) is not SingleVariableDesignationSyntax { Parent: RecursivePatternSyntax muster }) continue;
                    var teile = muster.PropertyPatternClause?.Subpatterns ?? default;
                    if (teile.Any(t => t.NameColon?.Name.Identifier.ValueText is "IsCompletedSuccessfully" or "IsCompleted"
                                       && t.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax l }
                                       && l.IsKind(SyntaxKind.TrueLiteralExpression)))
                        return true;
                }
                return false;
            }

            /// <summary>Eine <c>[HausregelAusnahme]</c> ohne Begründung (leer oder nur Leerraum) ist selbst ein Fehler.</summary>
            public void Begruendung(SymbolAnalysisContext ctx, INamedTypeSymbol attribut)
            {
                foreach (var a in ctx.Symbol.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribut)) continue;
                    var text = a.ConstructorArguments.Length == 1 ? a.ConstructorArguments[0].Value as string : null;
                    if (!string.IsNullOrWhiteSpace(text)) continue;
                    var ort = a.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken).GetLocation() ?? ctx.Symbol.Locations.FirstOrDefault();
                    ctx.ReportDiagnostic(Diagnostic.Create(OhneBegruendung, ort, ctx.Symbol.Name));
                }
            }

            private void Melde(SyntaxNodeAnalysisContext ctx, SyntaxNodeOrToken ort, string muster, string warum)
            {
                if (ctx.ContainingSymbol is not { } symbol) return;
                var typ = symbol as INamedTypeSymbol ?? symbol.ContainingType;
                if (typ is null || !Gilt(typ)) return;
                if (Ausgenommen(symbol)) return;
                ctx.ReportDiagnostic(Diagnostic.Create(Verstoss, ort.GetLocation(), muster, typ.Name, warum));
            }

            /// <summary>Gilt die Regel im Typ? Ein geschachtelter Typ zählt nur, wenn er selbst geprüft wird (Actor oder besessen).</summary>
            private bool Gilt(INamedTypeSymbol typ) => geprueft.Contains(typ.OriginalDefinition);

            /// <summary>Trägt der Member, eine lokale Funktion um ihn oder sein Typ (auch ein äußerer) die Ausnahme?</summary>
            private bool Ausgenommen(ISymbol symbol)
            {
                if (ausnahme is null) return false;
                for (var s = symbol; s is not null; s = s.ContainingSymbol)
                {
                    if (s is INamespaceSymbol) break;
                    var traeger = s is IMethodSymbol { AssociatedSymbol: { } prop } ? new[] { s, prop } : new[] { s };
                    if (traeger.Any(t => t.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, ausnahme)))) return true;
                }
                return false;
            }
        }

        private static bool Ist(INamedTypeSymbol? t, string voll) => t is not null && t.OriginalDefinition.ToDisplayString() == voll;

        private static bool IstTask(ITypeSymbol? t) =>
            t is INamedTypeSymbol n && Namensraum(n) == "System.Threading.Tasks" && n.Name is "Task" or "ValueTask";

        private static string Namensraum(INamedTypeSymbol t) => t.ContainingNamespace?.ToDisplayString() ?? "";
    }
}
