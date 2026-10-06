using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS058 — Akteur-Befugnis.</b> <c>IDarf&lt;T&gt;</c> ist EIN Wort für alles, was ein Akteur in das System
/// hineingibt: Command, Query, Trigger, Transient-Event. Alles andere (ein Event aus dem Log, eine Response, ein
/// Wertobjekt …) kann man nicht „dürfen" — das Hören wird aus dem Graphen abgeleitet. Und <c>IDarf</c> gehört an einen
/// <c>IAkteur</c>: an einem anderen Typ würde es niemand lesen. Beides ist ein Build-Fehler (docs/konzept-akteure.md).
/// <para><b>CQRS061/062 — Akteur-Vertrag</b> (§9): <c>IAkteurVertrag&lt;A&gt;</c> ist ein Interface, höchstens eines je Akteur, nur
/// <c>Auf(TEvent)</c> (ein Event aus dem Log oder ein Transient-Event, je Event höchstens einmal); der Rückgabetyp ist der
/// Ausgabe-Vertrag und nennt nur konkrete Commands (wie CQRS050 bei Handles).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AkteurAnalyzer : DiagnosticAnalyzer
{
    public const string BefugnisId = "CQRS058", AuftragId = "CQRS060", VertragId = "CQRS061", VertragsAusgabeId = "CQRS062";

    private static readonly DiagnosticDescriptor Befugnis = new(
        BefugnisId, "Ungültige Akteur-Befugnis",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>
    /// CQRS060 — Im Auftrag eines Akteurs über seinen Dienst (<c>IAkteurDienst&lt;A&gt;</c>, z. B. der Classifier-Dienst des
    /// Klassifizierers): der Dienst kommt als Parameter an den Pipeline-Handle (so
    /// steht in der Signatur, WER entscheidet), höchstens einer je Handle, und der Handle gibt nur Commands aus, die dieser
    /// Akteur darf. Im Konstruktor/Feld eines Konsumenten wäre die Verbindung unsichtbar (wie ein Store, CQRS054).
    /// </summary>
    private static readonly DiagnosticDescriptor Auftrag = new(
        AuftragId, "Im Auftrag eines Akteurs",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS061 — Form eines Akteur-Vertrags: Interface, einer je Akteur, nur <c>Auf(TEvent)</c>, je Event einmal.</summary>
    private static readonly DiagnosticDescriptor Vertrag = new(
        VertragId, "Ungültiger Akteur-Vertrag",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS062 — Ausgabe-Vertrag einer Reaktion: <c>void</c>, ein konkreter Command, <c>OneOf</c> davon oder ein Strom davon.</summary>
    private static readonly DiagnosticDescriptor VertragsAusgabe = new(
        VertragsAusgabeId, "Ausgabe-Vertrag einer Reaktion",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Befugnis, Auftrag, Vertrag, VertragsAusgabe);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            INamedTypeSymbol? T(string n) => c.GetTypeByMetadataName(n);
            var iDarf = T("Abstractions.IDarf`1");
            var iAkteur = T("Abstractions.IAkteur");
            if (iDarf == null || iAkteur == null) return;
            var hinein = new[] { T("Abstractions.ICommand"), T("Abstractions.IQuery"), T("Abstractions.IPipelineTrigger"), T("Abstractions.ITransientEvent") }
                .Where(x => x != null).Cast<INamedTypeSymbol>().ToArray();
            start.RegisterSymbolAction(ctx => Pruefe(ctx, iDarf, iAkteur, hinein), SymbolKind.NamedType);
            var k = new Konsumenten(T("Abstractions.ISubscriber"), T("Abstractions.IReader`1"), T("Abstractions.IPipelineHandler"),
                T("Abstractions.PipelineContext"), T("Abstractions.ICommand"));
            var v = new VertragsTypen(T("Abstractions.IAkteurVertrag`1"), T("Abstractions.IEvent"), T("Abstractions.ITransientEvent"),
                T("Abstractions.ICommand"));
            // Akteur → seine Verträge (aus der eigenen Assembly, einmal je Compilation): „höchstens einer je Akteur" und die
            // Verkörperungs-Prüfung am Dienst brauchen den Blick über den einzelnen Typ hinaus.
            var vertraege = new System.Lazy<Dictionary<ITypeSymbol, List<INamedTypeSymbol>>>(() => SammleVertraege(c, v));
            if (v.Vertrag != null)
                start.RegisterSymbolAction(ctx => PruefeVertrag(ctx, v, vertraege.Value), SymbolKind.NamedType);
            var iDienst = T("Abstractions.IAkteurDienst`1");
            if (iDienst == null) return;
            start.RegisterSymbolAction(ctx => PruefeKonsument(ctx, iDienst, k), SymbolKind.NamedType);
            start.RegisterSymbolAction(ctx => PruefeHandle(ctx, iDarf, iDienst, k, v, vertraege), SymbolKind.Method);
        });
    }

    private static void Pruefe(SymbolAnalysisContext ctx, INamedTypeSymbol iDarf, INamedTypeSymbol iAkteur, INamedTypeSymbol[] hinein)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        // Ein Akteur ist ein Record (Domänen-Experte), nie ein Dienst-Vertrag: der Dienst gehört einem Akteur (IAkteurDienst<A>).
        if (typ.TypeKind == TypeKind.Interface && typ.ContainingNamespace?.ToDisplayString() != "Abstractions"
            && typ.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
            ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                "ein Dienst ist kein Akteur — Akteure sind Records (IMensch/IMaschine/IKi); lass den Dienst einem Akteur gehören: IAkteurDienst<TAkteur>"));
        var darf = typ.Interfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).ToList();
        if (darf.Count == 0) return;

        if (!typ.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
            ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                "IDarf<…> steht nur an einem Akteur — ergänze IAkteur in der Basisliste"));

        foreach (var d in darf)
        {
            var t = d.TypeArguments[0];
            var erlaubt = hinein.Any(h => SymbolEqualityComparer.Default.Equals(t, h)
                || t.AllInterfaces.Contains(h, SymbolEqualityComparer.Default));
            if (!erlaubt || t.TypeKind == TypeKind.Interface || t.IsAbstract)
                ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                    $"IDarf<{t.Name}> — dürfen kann man nur, was man hineingibt: einen konkreten Command, eine Query, "
                    + "einen Trigger oder ein Transient-Event (was ein Akteur hört, wird aus dem Graphen abgeleitet)"));
        }
    }

    private sealed class Konsumenten
    {
        public readonly INamedTypeSymbol? Subscriber, Reader, Pipeline, PipeCtx, Command;
        public Konsumenten(INamedTypeSymbol? subscriber, INamedTypeSymbol? reader, INamedTypeSymbol? pipeline,
            INamedTypeSymbol? pipeCtx, INamedTypeSymbol? command)
        { Subscriber = subscriber; Reader = reader; Pipeline = pipeline; PipeCtx = pipeCtx; Command = command; }
    }

    private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
        (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

    /// <summary>Der Akteur eines Akteur-Dienstes (<c>IAkteurDienst&lt;A&gt;</c> → A), sonst null.</summary>
    private static ITypeSymbol? AkteurVon(ITypeSymbol t, INamedTypeSymbol iDienst) =>
        t.TypeKind != TypeKind.Interface ? null
            : t.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDienst))?.TypeArguments.FirstOrDefault();

    private static bool IstAkteurDienst(ITypeSymbol t, INamedTypeSymbol iDienst) => AkteurVon(t, iDienst) != null;

    // Akteur-Dienst im Konstruktor/Feld einer Klasse: die Verbindung „wer entscheidet" stünde nicht in der Signatur.
    private static void PruefeKonsument(SymbolAnalysisContext ctx, INamedTypeSymbol iDienst, Konsumenten k)
    {
        var t = (INamedTypeSymbol)ctx.Symbol;
        // In JEDER Klasse (nicht nur Konsumenten): sonst wäre offen, wer den Dienst benutzt (docs/konzept-akteure.md §7.2).
        if (t.TypeKind != TypeKind.Class) return;
        foreach (var ctor in t.InstanceConstructors.Where(x => !x.IsImplicitlyDeclared))
            foreach (var p in ctor.Parameters.Where(p => IstAkteurDienst(p.Type, iDienst)))
                ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, p.Locations.FirstOrDefault(), t.Name,
                    $"der Akteur-Dienst {p.Type.Name} steht im Konstruktor — er gehört als Parameter an den Handle, der in seinem Auftrag entscheidet"));
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsImplicitlyDeclared && IstAkteurDienst(f.Type, iDienst)))
            ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, f.Locations.FirstOrDefault(), t.Name,
                $"das Feld '{f.Name}' hält den Akteur-Dienst {f.Type.Name} — er gehört als Parameter an den Handle"));
    }

    // Handle mit Akteur-Parameter: höchstens einer, und nur Commands, die dieser Akteur darf — und keine zweite Verkörperung
    // derselben Entscheidung (der Akteur reagiert im Vertrag schon draußen auf dasselbe Event mit demselben Command).
    private static void PruefeHandle(SymbolAnalysisContext ctx, INamedTypeSymbol iDarf, INamedTypeSymbol iDienst, Konsumenten k,
        VertragsTypen v, System.Lazy<Dictionary<ITypeSymbol, List<INamedTypeSymbol>>> vertraege)
    {
        var m = (IMethodSymbol)ctx.Symbol;
        if (m.MethodKind != MethodKind.Ordinary || m.ContainingType is not { } typ || !Hat(typ, k.Pipeline)) return;
        if (m.Parameters.Length < 2 || !SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, k.PipeCtx)) return;
        var akteure = m.Parameters.Skip(2).Where(p => IstAkteurDienst(p.Type, iDienst)).ToList();
        if (akteure.Count == 0) return;
        var ort = m.Locations.FirstOrDefault() ?? Location.None;
        if (akteure.Count > 1)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, ort, $"{typ.Name}.{m.Name}",
                $"entscheidet im Auftrag mehrerer Akteure ({string.Join(", ", akteure.Select(a => a.Type.Name))}) — höchstens einer je Handle"));
            return;
        }
        var akteur = AkteurVon(akteure[0].Type, iDienst)!;
        var darf = akteur.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf))
            .Select(i => i.TypeArguments[0]).ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        // Befugt = IDarf ∪ Ausgaben seines Vertrags (§9): was er draußen als Reaktion gibt, darf er auch drinnen.
        var vertragsAusgaben = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);
        if (vertraege.Value.TryGetValue(akteur, out var vs))
            foreach (var vt in vs)
                foreach (var auf in AufMethoden(vt))
                {
                    var aus = new HashSet<ITypeSymbol>(VertragsAusgaben(auf.ReturnType, out _).Item1, SymbolEqualityComparer.Default);
                    vertragsAusgaben[auf.Parameters[0].Type] = aus;
                    darf = darf.Union(aus);
                }
        foreach (var cmd in Ausgaben(m.ReturnType).Where(t => Hat(t, k.Command) && !darf.Contains(t)))
            ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, ort, $"{typ.Name}.{m.Name}",
                $"gibt {cmd.Name} im Auftrag von {akteur.Name} aus, aber {akteur.Name} darf das nicht — ergänze IDarf<{cmd.Name}> am Akteur oder nimm den Akteur-Parameter weg"));
        if (vertragsAusgaben.TryGetValue(m.Parameters[0].Type, out var draussen))
            foreach (var cmd in Ausgaben(m.ReturnType).Where(draussen.Contains))
                ctx.ReportDiagnostic(Diagnostic.Create(Auftrag, ort, $"{typ.Name}.{m.Name}",
                    $"{akteur.Name} trifft dieselbe Entscheidung zweimal: er reagiert in seinem Vertrag schon auf {m.Parameters[0].Type.Name} mit {cmd.Name} "
                    + "— eine Verkörperung je Entscheidung (drinnen über den Dienst ODER draußen über den Vertrag)"));
    }

    // ── Akteur-Vertrag (CQRS061/062) ────────────────────────────────────────────────────────────────────────────────

    private sealed class VertragsTypen
    {
        public readonly INamedTypeSymbol? Vertrag, Event, Transient, Command;
        public VertragsTypen(INamedTypeSymbol? vertrag, INamedTypeSymbol? evt, INamedTypeSymbol? transient, INamedTypeSymbol? command)
        { Vertrag = vertrag; Event = evt; Transient = transient; Command = command; }
    }

    /// <summary>Die Akteure, deren Vertrag <paramref name="t"/> ist (<c>IAkteurVertrag&lt;A&gt;</c> → A).</summary>
    private static List<ITypeSymbol> VertragsAkteure(INamedTypeSymbol t, VertragsTypen v) =>
        v.Vertrag == null ? new List<ITypeSymbol>()
            : t.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, v.Vertrag))
                .Select(i => i.TypeArguments[0]).Distinct<ITypeSymbol>(SymbolEqualityComparer.Default).ToList();

    private static IEnumerable<IMethodSymbol> AufMethoden(INamedTypeSymbol vertrag) =>
        vertrag.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.Name == Abstractions.Akteurvertrag.Auf && m.Parameters.Length == 1);

    private static Dictionary<ITypeSymbol, List<INamedTypeSymbol>> SammleVertraege(Compilation c, VertragsTypen v)
    {
        var d = new Dictionary<ITypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        if (v.Vertrag == null) return d;
        void Lauf(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers())
                if (t.TypeKind == TypeKind.Interface && t.ContainingNamespace?.ToDisplayString() != "Abstractions")
                    foreach (var a in VertragsAkteure(t, v))
                    {
                        if (!d.TryGetValue(a, out var l)) d[a] = l = new List<INamedTypeSymbol>();
                        l.Add(t);
                    }
            foreach (var sub in ns.GetNamespaceMembers()) Lauf(sub);
        }
        Lauf(c.Assembly.GlobalNamespace);
        return d;
    }

    private static void PruefeVertrag(SymbolAnalysisContext ctx, VertragsTypen v, Dictionary<ITypeSymbol, List<INamedTypeSymbol>> vertraege)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        if (typ.ContainingNamespace?.ToDisplayString() == "Abstractions") return;
        var akteure = VertragsAkteure(typ, v);
        if (akteure.Count == 0) return;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        void Melde(DiagnosticDescriptor d, Location? o, string text) => ctx.ReportDiagnostic(Diagnostic.Create(d, o ?? ort, typ.Name, text));

        if (typ.TypeKind != TypeKind.Interface)
        {
            Melde(Vertrag, ort, "ein Akteur-Vertrag ist ein Interface — implementiert wird er nur draußen (vom Client, gegen die generierte Basis)");
            return;
        }
        if (akteure.Count > 1)
            Melde(Vertrag, ort, $"ein Vertrag gehört genau einem Akteur, nicht {string.Join(", ", akteure.Select(a => a.Name))}");
        foreach (var a in akteure)
            if (vertraege.TryGetValue(a, out var alle) && alle.Count > 1)
                Melde(Vertrag, ort, $"{a.Name} hat mehrere Verträge ({string.Join(", ", alle.Select(x => x.Name).OrderBy(x => x))}) — höchstens einer je Akteur");

        var gesehen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var m in typ.GetMembers())
        {
            var mOrt = m.Locations.FirstOrDefault();
            if (m is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove }) continue;
            if (m is not IMethodSymbol meth || meth.MethodKind != MethodKind.Ordinary)
            {
                Melde(Vertrag, mOrt, $"'{m.Name}' — ein Vertrag enthält nur Reaktionen: {Abstractions.Akteurvertrag.Auf}(TEvent e)");
                continue;
            }
            if (meth.Name != Abstractions.Akteurvertrag.Auf)
            {
                Melde(Vertrag, mOrt, $"'{meth.Name}' — Reaktionen heißen {Abstractions.Akteurvertrag.Auf}(TEvent e)");
                continue;
            }
            if (meth.Parameters.Length != 1 || meth.IsGenericMethod)
            {
                Melde(Vertrag, mOrt, $"{meth.Name} nimmt genau ein Event: {Abstractions.Akteurvertrag.Auf}(TEvent e)");
                continue;
            }
            var e = meth.Parameters[0].Type;
            var istEvent = (Hat(e, v.Event) || Hat(e, v.Transient)) && e.TypeKind != TypeKind.Interface && !e.IsAbstract;
            if (!istEvent)
                Melde(Vertrag, mOrt, $"{meth.Name}({e.Name}) — reagieren kann man nur auf ein konkretes Event aus dem Log oder ein Transient-Event");
            else if (!gesehen.Add(e))
                Melde(Vertrag, mOrt, $"zwei Reaktionen auf {e.Name} — je Event höchstens ein {Abstractions.Akteurvertrag.Auf}");

            var (aus, fehler) = VertragsAusgaben(meth.ReturnType, out _);
            if (fehler != null)
                ctx.ReportDiagnostic(Diagnostic.Create(VertragsAusgabe, mOrt ?? ort, $"{typ.Name}.{meth.Name}({e.Name})", fehler));
            else
                foreach (var t in aus.Where(t => !Hat(t, v.Command) || t.TypeKind == TypeKind.Interface || t.IsAbstract))
                    ctx.ReportDiagnostic(Diagnostic.Create(VertragsAusgabe, mOrt ?? ort, $"{typ.Name}.{meth.Name}({e.Name})",
                        $"gibt {t.ToDisplayString()} aus — eine Reaktion gibt nur konkrete Commands hinein (oder OneOf davon), nie ICommand/object"));
        }
    }

    /// <summary>
    /// Ausgabe-Vertrag einer Reaktion: <c>void</c> → keine; <c>T</c>/<c>OneOf&lt;…&gt;</c> → eine; <c>IEnumerable</c>/<c>IAsyncEnumerable</c>
    /// davon → Strom. Zweiter Wert: Fehlertext, wenn die Form nicht stimmt (Task, verschachtelt …).
    /// </summary>
    private static (List<ITypeSymbol>, string?) VertragsAusgaben(ITypeSymbol rueckgabe, out bool strom)
    {
        strom = false;
        var liste = new List<ITypeSymbol>();
        if (rueckgabe.SpecialType == SpecialType.System_Void) return (liste, null);
        var el = rueckgabe;
        if (el is INamedTypeSymbol { TypeArguments.Length: 1 } en
            && en.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IEnumerable<T>" or "System.Collections.Generic.IAsyncEnumerable<T>")
        {
            strom = true;
            el = en.TypeArguments[0];
        }
        if (el is INamedTypeSymbol { Name: "OneOf" } oneOf && oneOf.ContainingNamespace?.ToDisplayString() == "Abstractions")
            liste.AddRange(oneOf.TypeArguments);
        else if (el is INamedTypeSymbol { IsGenericType: true } g)
            return (liste, $"{g.ToDisplayString()} ist kein Ausgabe-Vertrag — erlaubt: void, ein Command, OneOf<…> oder IAsyncEnumerable<OneOf<…>> (ohne Task)");
        else
            liste.Add(el);
        return (liste, null);
    }

    /// <summary>Die ausgegebenen Typen aus <c>(Async)Enumerable&lt;OneOf&lt;…&gt;&gt;</c> bzw. <c>(Async)Enumerable&lt;T&gt;</c>.</summary>
    private static IEnumerable<ITypeSymbol> Ausgaben(ITypeSymbol rueckgabe)
    {
        if (rueckgabe is not INamedTypeSymbol { TypeArguments.Length: 1 } en || en.TypeArguments[0] is not INamedTypeSymbol el) yield break;
        if (el.Name == "OneOf") foreach (var a in el.TypeArguments) yield return a;
        else yield return el;
    }
}
