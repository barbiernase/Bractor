using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Infrastructure.SourceGeneration
{
    /// <summary>
    /// Auflage A6 (EM-1 <b>erzwungen</b>): der Analyzer macht „genau EIN Emit-Weg" zu einer Build-Regel.
    /// Nach dem Treiber-Fold (P5) gibt es nur noch zwei legitime Stellen, die ein Command senden — als <c>Frage</c> des Boten
    /// (<c>bote.FrageAsync&lt;CommandResult&gt;</c>, Pflicht-Frist) oder roh per <c>cluster.RequestAsync&lt;CommandResult&gt;</c>:
    ///  • <c>Infrastructure.Emit.CommandEmitter</c> — das EINE Emit-Primitiv (Funktionen, Uhr, gRPC-Eingang emittierter Commands).
    ///  • <c>Infrastructure.Aggregate.ActorSystem.ProtoActorAggregateDispatcher</c> — der Client-/OCC-Pfad.
    /// Jeder ANDERE rohe Command-Send umgeht die deterministische CommandId (W1) + den bounded Token (W2)
    /// und bringt einen zweiten Emit-Weg zurück → <b>CQRS020</b> (Build-Fehler).
    ///
    /// Zusätzlich <b>CQRS021</b>: ein Command-Send mit <c>CancellationToken.None</c> (oder <c>default</c>) ist
    /// eine UNBOUNDED Command-Kante (W2) — die (A)-Hang-Klasse (unendlicher stiller Retry bei nicht-konvergentem
    /// Cluster). Gilt AUCH innerhalb der erlaubten Typen (ein Regressions-Riegel: der Emitter/Dispatcher müssen
    /// bounded bleiben).
    ///
    /// Präzise, wenige Fehlalarme: syntaktischer Vorfilter (<c>RequestAsync&lt;T&gt;</c>, <c>FrageAsync&lt;T&gt;</c>) + semantische
    /// Bestätigung, dass <c>T</c> exakt <c>Abstractions.CommandResult</c> ist. CQRS021 gilt nur für den rohen Send: eine Frage hat ihre
    /// Frist ohnehin (Pflicht-Parameter).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CommandEmitAnalyzer : DiagnosticAnalyzer
    {
        public const string RawSendId = "CQRS020";
        public const string UnboundedTokenId = "CQRS021";

        private const string Kategorie = "CQRS.EmitEinsWeg";

        private static readonly DiagnosticDescriptor RawSend = new(
            RawSendId,
            "Roher Command-Send außerhalb des Emit-Primitivs",
            "'{0}' sendet ein Command direkt (RequestAsync/FrageAsync<CommandResult>) — es gibt genau EINEN Emit-Weg " +
            "(CommandEmitter.EmitAsync); der Client-/OCC-Pfad ist ProtoActorAggregateDispatcher. EM-1 verletzt.",
            Kategorie,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "EM-1: ein roher cluster.RequestAsync<CommandResult>-Send umgeht die deterministische " +
                         "CommandId (W1) + den bounded Token (W2) und bringt einen zweiten Emit-Weg zurück.");

        private static readonly DiagnosticDescriptor UnboundedToken = new(
            UnboundedTokenId,
            "Unbounded Command-Kante (CancellationToken.None)",
            "Command-Send mit CancellationToken.None/default — jede await-Command-Kante MUSS bounded sein (W2), " +
            "sonst hängt der Send bei nicht-konvergentem Cluster unendlich still (die (A)-Hang-Klasse).",
            Kategorie,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "W2: kein CancellationToken.None auf einer Command-Kante.");

        // Allow-Liste per voll qualifiziertem Typnamen (die zwei legitimen Command-Sender nach dem Treiber-Fold).
        private static readonly ImmutableHashSet<string> Erlaubt = ImmutableHashSet.Create(
            "Infrastructure.Emit.CommandEmitter",
            "Infrastructure.Aggregate.ActorSystem.ProtoActorAggregateDispatcher");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RawSend, UnboundedToken);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;

            // Syntaktischer Vorfilter: ....RequestAsync<T>( ... ) bzw. ....FrageAsync<T>( ... ) mit genau EINEM Typ-Argument.
            if (invocation.Expression is not MemberAccessExpressionSyntax ma) return;
            if (ma.Name is not GenericNameSyntax gen) return;
            var roh = gen.Identifier.ValueText == "RequestAsync";
            if (!roh && gen.Identifier.ValueText != "FrageAsync") return;
            if (gen.TypeArgumentList.Arguments.Count != 1) return;

            // Semantische Bestätigung: T == Abstractions.CommandResult (präzise, kein Namens-Zufall).
            if (ctx.SemanticModel.GetSymbolInfo(gen.TypeArgumentList.Arguments[0], ctx.CancellationToken).Symbol
                is not INamedTypeSymbol typeSym) return;
            if (typeSym.Name != "CommandResult") return;
            if (typeSym.ContainingNamespace?.ToDisplayString() != "Abstractions") return;

            // CQRS020: außerhalb der Allow-Liste?
            var typeName = ctx.ContainingSymbol?.ContainingType?.ToDisplayString();
            if (typeName is null || !Erlaubt.Contains(typeName))
                ctx.ReportDiagnostic(Diagnostic.Create(RawSend, invocation.GetLocation(), typeName ?? "(unbekannt)"));

            // CQRS021: None/default-Token auf der rohen Kante? (auch innerhalb der erlaubten Typen prüfen)
            var args = invocation.ArgumentList.Arguments;
            if (roh && args.Count > 0)
            {
                var last = args[args.Count - 1].Expression;
                if (IstNoneToken(last, ctx.SemanticModel, ctx.CancellationToken))
                    ctx.ReportDiagnostic(Diagnostic.Create(UnboundedToken, last.GetLocation()));
            }
        }

        private static bool IstNoneToken(ExpressionSyntax expr, SemanticModel model, CancellationToken ct)
        {
            // CancellationToken.None
            if (expr is MemberAccessExpressionSyntax m && m.Name.Identifier.ValueText == "None")
            {
                var t = model.GetTypeInfo(m.Expression, ct).Type;
                if (IstCancellationToken(t)) return true;
            }
            // default  (default-Literal, in CancellationToken-Position)
            if (expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                if (IstCancellationToken(model.GetTypeInfo(expr, ct).ConvertedType)) return true;
            }
            // default(CancellationToken)
            if (expr is DefaultExpressionSyntax de)
            {
                if (IstCancellationToken(model.GetTypeInfo(de.Type, ct).Type)) return true;
            }
            return false;
        }

        private static bool IstCancellationToken(ITypeSymbol? t)
            => t?.Name == "CancellationToken" && t.ContainingNamespace?.ToDisplayString() == "System.Threading";
    }
}
