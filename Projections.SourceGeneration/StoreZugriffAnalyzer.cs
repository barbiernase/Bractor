using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Projections.SourceGeneration;

/// <summary>
/// <b>Ein Store schreibt nur über seinen Puffer.</b> Ein Co-Commit-Store (Klasse mit Store-Fähigkeiten, deren Basis die
/// Marke trägt — <c>ICoCommitTracker</c>) puffert Effekte; die Basis committet sie mit der Marke in EINER Transaktion
/// und prüft je Dokument, wem es gehört. Der übliche Marten-Weg <c>LightweightSession() … SaveChangesAsync()</c> ginge
/// still daran vorbei: kein gemeinsames Commit (Absturz ⇒ doppelter Effekt beim Replay), keine Schreib-Regel (Lost
/// Update). Deshalb:
///  • <b>CQRS066</b> — von den Datenzugriffs-Assemblies nur die Positivliste aus <see cref="StoreZugriff"/> (Lese-Einstieg
///    <c>QuerySession()</c>, darauf <c>LoadAsync</c>/<c>LoadManyAsync</c>/<c>Query</c>, LINQ-Materialisierung). Auch ein
///    Zugangs-Objekt (Document-Store, Session, Verbindung) an fremden Code weiterzureichen ist CQRS066 — dort prüft
///    niemand, was damit geschieht.
///  • <b>CQRS067</b> — Rollen je Funktion: eine Schreib-Fähigkeit liest keine committeten Daten (Lesen-Ändern-Schreiben
///    gehört in die Puffer-Methoden, die im Stapel lesen); eine Lese-Fähigkeit ruft keine Methode der Co-Commit-Basis.
/// Rollen über Marker und Symbole, nie über Namen. Nicht abgedeckt (bewusst): eigener Code AUSSERHALB des Stores, der
/// sich selbst einen Document-Store besorgt — das passiert nicht aus Versehen.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StoreZugriffAnalyzer : DiagnosticAnalyzer
{
    public const string ZugriffId = "CQRS066", RolleId = "CQRS067";

    private static readonly DiagnosticDescriptor Zugriff = new(
        ZugriffId, "Store schreibt am Puffer vorbei",
        "'{0}' {1} — ein Store schreibt nur über die Puffer-Methoden seiner Co-Commit-Basis; von der Datenzugriffs-Bibliothek " +
        "ist nur der Lese-Einstieg erlaubt (QuerySession → LoadAsync/LoadManyAsync/Query)",
        "CQRS.Store", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Rolle = new(
        RolleId, "Lese- und Schreib-Funktion vermischt",
        "'{0}' implementiert die {1}-Fähigkeit '{2}' und {3}",
        "CQRS.Store", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Zugriff, Rolle);

    private enum Art { Keine, Schreiben, Lesen }

    /// <summary>Was über einen Typ zu wissen ist: ist er ein Co-Commit-Store, seine Co-Commit-Basen, die Rolle je Methode.</summary>
    private sealed class StoreInfo
    {
        public readonly HashSet<INamedTypeSymbol> Basen = new(SymbolEqualityComparer.Default);
        public readonly Dictionary<IMethodSymbol, (Art Art, INamedTypeSymbol Faehigkeit)> Rollen = new(SymbolEqualityComparer.Default);
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var iCoCommit = c.GetTypeByMetadataName("Abstractions.ICoCommitTracker");
            var iWrite = c.GetTypeByMetadataName("Abstractions.IWriteStore");
            var iRead = c.GetTypeByMetadataName("Abstractions.IReadStore");
            var iStore = c.GetTypeByMetadataName("Abstractions.IStore");
            if (iCoCommit == null || iWrite == null || iRead == null) return;   // kein Store-Vertrag in dieser Compilation

            var k = new Kontext(iCoCommit, iWrite, iRead, iStore,
                c.GetTypeByMetadataName(StoreZugriff.DocumentStore),
                c.GetTypeByMetadataName(StoreZugriff.QuerySession),
                c.GetTypeByMetadataName("System.Linq.IQueryable`1"),
                c.GetTypeByMetadataName("System.Collections.IEnumerable"));
            var cache = new ConcurrentDictionary<INamedTypeSymbol, StoreInfo?>(SymbolEqualityComparer.Default);

            start.RegisterOperationAction(ctx =>
            {
                var typ = ctx.ContainingSymbol.ContainingType;
                if (typ == null) return;
                var info = cache.GetOrAdd(typ, t => k.Info(t));
                if (info == null) return;
                Pruefe(ctx, k, typ, info);
            },
            OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.FieldReference, OperationKind.MethodReference,
            OperationKind.EventReference, OperationKind.ObjectCreation, OperationKind.Argument, OperationKind.SimpleAssignment);
        });
    }

    private static void Pruefe(OperationAnalysisContext ctx, Kontext k, INamedTypeSymbol typ, StoreInfo info)
    {
        var op = ctx.Operation;
        switch (op)
        {
            case IArgumentOperation arg:
                if (arg.ArgumentKind == ArgumentKind.Explicit && arg.Value.Type is { } vt && k.IstZugang(vt)
                    && arg.Parameter?.ContainingSymbol is IMethodSymbol ziel
                    && !k.Erlaubt(ziel) && !IstEigen(ziel, typ, info))
                    Melde(ctx, Zugriff, typ.Name, $"gibt '{vt.Name}' an '{ziel.ContainingType?.Name}.{ziel.Name}' weiter");
                return;
            case ISimpleAssignmentOperation zuw:
                if (zuw.Value.Type is { } wt && k.IstZugang(wt) && Mitglied(zuw.Target) is { } m
                    && !SymbolEqualityComparer.Default.Equals(m.ContainingType, typ))
                    Melde(ctx, Zugriff, typ.Name, $"legt '{wt.Name}' in '{m.ContainingType?.Name}.{m.Name}' ab");
                return;
        }

        var sym = Mitglied(op);
        if (sym == null) return;

        if (k.IstDatenzugriff(sym.ContainingType) && !k.Erlaubt(sym))
            Melde(ctx, Zugriff, typ.Name, $"benutzt '{sym.ContainingType?.Name}.{sym.Name}'");

        // CQRS067 — nur für direkte Aufrufe in der Methode, die die Fähigkeit implementiert.
        if (op is not IInvocationOperation inv || Methode(ctx.ContainingSymbol) is not { } methode
            || !info.Rollen.TryGetValue(methode, out var rolle)) return;
        var ziel2 = inv.TargetMethod.OriginalDefinition;
        if (rolle.Art == Art.Schreiben && k.IstLeseEinstieg(ziel2))
            Melde(ctx, Rolle, methode.Name, "Schreib", rolle.Faehigkeit.Name,
                "liest committete Daten — Lesen-Ändern-Schreiben gehört in die Puffer-Methoden der Basis (sie lesen im Stapel)");
        else if (rolle.Art == Art.Lesen && info.Basen.Contains(ziel2.ContainingType))
            Melde(ctx, Rolle, methode.Name, "Lese", rolle.Faehigkeit.Name,
                $"ruft '{ziel2.Name}' der Co-Commit-Basis — eine Lese-Funktion schreibt nicht");
    }

    private static bool IstEigen(IMethodSymbol ziel, INamedTypeSymbol typ, StoreInfo info) =>
        SymbolEqualityComparer.Default.Equals(ziel.ContainingType, typ)                                       // eigene Helfer (selbst geprüft)
        || (ziel.MethodKind == MethodKind.Constructor && Basen(typ).Any(b => SymbolEqualityComparer.Default.Equals(b, ziel.ContainingType)));

    private static ISymbol? Mitglied(IOperation op) => op switch
    {
        IInvocationOperation i => i.TargetMethod,
        IPropertyReferenceOperation p => p.Property,
        IFieldReferenceOperation f => f.Field,
        IMethodReferenceOperation m => m.Method,
        IEventReferenceOperation e => e.Event,
        IObjectCreationOperation o => o.Constructor,
        _ => null
    };

    /// <summary>Das Mitglied, zu dem ein Lambda/eine lokale Funktion gehört.</summary>
    private static IMethodSymbol? Methode(ISymbol s)
    {
        while (s is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction } m) s = m.ContainingSymbol;
        return s as IMethodSymbol;
    }

    private static IEnumerable<INamedTypeSymbol> Basen(INamedTypeSymbol t)
    {
        for (var b = t.BaseType; b != null; b = b.BaseType) yield return b;
    }

    private static void Melde(OperationAnalysisContext ctx, DiagnosticDescriptor d, params object[] args) =>
        ctx.ReportDiagnostic(Diagnostic.Create(d, ctx.Operation.Syntax.GetLocation(), args));

    private sealed class Kontext
    {
        private readonly INamedTypeSymbol _iCoCommit, _iWrite, _iRead;
        private readonly INamedTypeSymbol? _iStore, _docStore, _querySession, _iQueryable, _iEnumerable;
        private readonly HashSet<string> _assemblies = new(StoreZugriff.DatenzugriffAssemblies);
        private readonly HashSet<string> _session = new(StoreZugriff.SessionErlaubt);
        private readonly HashSet<string> _abfrage = new(StoreZugriff.AbfrageErlaubt);

        public Kontext(INamedTypeSymbol coCommit, INamedTypeSymbol write, INamedTypeSymbol read, INamedTypeSymbol? store,
                       INamedTypeSymbol? docStore, INamedTypeSymbol? querySession, INamedTypeSymbol? queryable, INamedTypeSymbol? enumerable)
        {
            _iCoCommit = coCommit; _iWrite = write; _iRead = read; _iStore = store;
            _docStore = docStore; _querySession = querySession; _iQueryable = queryable; _iEnumerable = enumerable;
        }

        private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
            (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i)
             || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

        /// <summary>Co-Commit-Store: eine BASIS trägt die Marke (ICoCommitTracker), der Typ selbst implementiert Fähigkeiten.
        /// Die Basis selbst hat keine Fähigkeiten und fällt heraus — sie ist der eine Ort, der schreiben muss.</summary>
        public StoreInfo? Info(INamedTypeSymbol t)
        {
            if (t.TypeKind != TypeKind.Class) return null;
            var info = new StoreInfo();
            foreach (var b in Basen(t))
                if (Hat(b, _iCoCommit)) info.Basen.Add(b.OriginalDefinition);
            if (info.Basen.Count == 0) return null;

            var faehigkeiten = t.AllInterfaces.Where(i => !Hat(i, _iStore) && !SymbolEqualityComparer.Default.Equals(i, _iWrite)
                                                          && !SymbolEqualityComparer.Default.Equals(i, _iRead)
                                                          && (Hat(i, _iWrite) || Hat(i, _iRead))).ToList();
            if (faehigkeiten.Count == 0) return null;

            foreach (var f in faehigkeiten)
            {
                var art = Hat(f, _iWrite) ? Art.Schreiben : Art.Lesen;
                foreach (var m in f.GetMembers().OfType<IMethodSymbol>())
                    if (t.FindImplementationForInterfaceMember(m) is IMethodSymbol impl && !info.Rollen.ContainsKey(impl))
                        info.Rollen[impl] = (art, f);
            }
            return info;
        }

        public bool IstDatenzugriff(INamedTypeSymbol? t) => t != null &&
            (_assemblies.Contains(t.ContainingAssembly?.Name ?? "") || t.ToDisplayString() == StoreZugriff.ServiceProvider);

        /// <summary>Ein Zugangs-Objekt (Document-Store, Session, Verbindung, Service-Locator) — keine Werte (Enums wie
        /// IsolationLevel), keine Abfragen/Ergebnisse, die nur Daten tragen.</summary>
        public bool IstZugang(ITypeSymbol t) => t is INamedTypeSymbol { IsReferenceType: true } n && IstDatenzugriff(n) && !Hat(n, _iEnumerable);

        public bool IstLeseEinstieg(IMethodSymbol m) =>
            m.Name == StoreZugriff.LeseEinstieg && SymbolEqualityComparer.Default.Equals(m.ContainingType?.OriginalDefinition, _docStore);

        public bool Erlaubt(ISymbol s)
        {
            if (s is IMethodSymbol m0 && IstLeseEinstieg(m0.OriginalDefinition)) return true;
            if (SymbolEqualityComparer.Default.Equals(s.ContainingType?.OriginalDefinition, _querySession) && _session.Contains(s.Name)) return true;
            if (!_abfrage.Contains(s.Name)) return false;
            if (s is IMethodSymbol m)
            {
                var stat = m.ReducedFrom ?? m;
                if (stat.IsExtensionMethod && stat.Parameters.Length > 0 && Hat(stat.Parameters[0].Type, _iQueryable)) return true;
            }
            return s.ContainingType != null && Hat(s.ContainingType, _iQueryable);   // z. B. IMartenQueryable<T>.Include
        }
    }
}
