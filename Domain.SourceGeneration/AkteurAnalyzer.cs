using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS058 — Akteur.</b> Ein Akteur ist ein Record mit <c>IAkteur</c>. <c>IDarf&lt;T&gt;</c> ist EIN Wort für alles, was er hineingibt: ein
/// konkreter Command (an ein Aggregat oder ohne Ziel), eine Query, ein Hinweis. <c>IHoert&lt;T&gt;</c> ist EIN Wort für alles, was zu ihm kommt: ein
/// Event oder ein Hinweis — ein Fakt, auf das er reagiert oder das er zur Kenntnis nimmt; nie ein Command (Schritt 6: es gibt keine Arbeit). Beide
/// stehen nur an einem Akteur; alles andere ist ein Build-Fehler (docs/konzept-akteure.md).
/// <para><b>CQRS063/064 — Client</b> (<c>docs/konzept-akteure.md</c> §4): ein Client ist ein Record mit <c>IClient</c>, der nur nennt, wen er
/// verkörpert (<c>IVerkoerpert&lt;A&gt;</c>), und selbst nichts deklariert (063); verkörpern kann er nur einen konkreten Akteur (064). Was über seine
/// Leitung geht, sind die <c>IDarf</c> und <c>IHoert</c> seiner Akteure — es gibt kein zweites Wort dafür.</para>
/// <para><b>CQRS125/126 — App</b> (docs/10 E18): eine App ist ein Record <c>X(…) : IApp&lt;TClient&gt;</c>, der sonst nichts nennt und
/// nichts deklariert; jeder Parameter ist eine QUELLE — eine Quellart mit mindestens einem <c>[Uebersetzt&lt;U&gt;]</c> (125). Jedes U ist eine
/// Übersetzung der Akteur-Seite, deren Quelle eine Meldung der Quellart ist und deren Ziel ein Command, den ein Akteur des Clients darf (126).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AkteurAnalyzer : DiagnosticAnalyzer
{
    public const string BefugnisId = "CQRS058", ClientFormId = "CQRS063", ClientBefugtId = "CQRS064", AppFormId = "CQRS125", AppQuelleId = "CQRS126";

    /// <summary>CQRS125 — Form einer App: ein Record mit IApp&lt;TClient&gt;; jeder Parameter eine Quellart mit [Uebersetzt&lt;U&gt;]; sonst nichts.</summary>
    private static readonly DiagnosticDescriptor AppForm = new(
        AppFormId, "Ungültige App",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS126 — die Übersetzung einer Quelle passt: aus einer Meldung der Quellart in einen Command, den ein Akteur des Clients darf.</summary>
    private static readonly DiagnosticDescriptor AppQuelle = new(
        AppQuelleId, "Quelle einer App passt nicht",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Befugnis = new(
        BefugnisId, "Ungültiger Akteur",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS063 — Form eines Clients: ein Record mit IClient; nennt nur IVerkoerpert; deklariert selbst nichts.</summary>
    private static readonly DiagnosticDescriptor ClientForm = new(
        ClientFormId, "Ungültiger Client",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS064 — verkörpern kann ein Client nur einen konkreten Akteur.</summary>
    private static readonly DiagnosticDescriptor ClientBefugt = new(
        ClientBefugtId, "Client verkörpert keinen Akteur",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Befugnis, ClientForm, ClientBefugt, AppForm, AppQuelle);

    private sealed class Typen
    {
        public INamedTypeSymbol Darf = null!, Hoert = null!, Akteur = null!;
        public INamedTypeSymbol? Client, Verkoerpert, Command, AggregatCommand, Query, Event, Hinweis;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            INamedTypeSymbol? T(string n) => c.GetTypeByMetadataName("Abstractions." + n);
            if (T("IDarf`1") is not { } darf || T("IHoert`1") is not { } hoert || T("IAkteur") is not { } akteur) return;
            var t = new Typen
            {
                Darf = darf, Hoert = hoert, Akteur = akteur, Client = T("IClient"), Verkoerpert = T("IVerkoerpert`1"), Command = T("ICommand"),
                AggregatCommand = T("IAggregatCommand"), Query = T("IQuery"), Event = T("IEvent"), Hinweis = T("ITransientEvent"),
            };
            start.RegisterSymbolAction(ctx => Pruefe(ctx, t), SymbolKind.NamedType);
            if (t.Client != null) start.RegisterSymbolAction(ctx => PruefeClient(ctx, t), SymbolKind.NamedType);
            if (T("IApp`1") is { } iApp) start.RegisterSymbolAction(ctx => PruefeApp(ctx, iApp, c), SymbolKind.NamedType);
        });
    }

    private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
        (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

    private static bool Konkret(ITypeSymbol t) => t.TypeKind != TypeKind.Interface && !t.IsAbstract;

    private static void Pruefe(SymbolAnalysisContext ctx, Typen x)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        if (typ.ContainingNamespace?.ToDisplayString() == "Abstractions") return;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        void Melde(string text) => ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name, text));
        // Ein Akteur ist ein Record (Domänen-Experte), nie ein Interface (ein Dienst, ein Vertrag).
        if (typ.TypeKind == TypeKind.Interface && typ.AllInterfaces.Contains(x.Akteur, SymbolEqualityComparer.Default))
            Melde("ein Interface ist kein Akteur — ein Akteur ist ein Record mit IAkteur");
        bool Ist(INamedTypeSymbol i, INamedTypeSymbol g) => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, g);
        var darf = typ.Interfaces.Where(i => Ist(i, x.Darf)).ToList();
        var hoert = typ.Interfaces.Where(i => Ist(i, x.Hoert)).ToList();
        if (darf.Count + hoert.Count == 0) return;
        if (!typ.AllInterfaces.Contains(x.Akteur, SymbolEqualityComparer.Default))
            Melde("IDarf<…> und IHoert<…> stehen nur an einem Akteur — ergänze IAkteur in der Basisliste");

        foreach (var t in darf.Select(d => d.TypeArguments[0]))
            if (!(Konkret(t) && (Hat(t, x.Command) || Hat(t, x.Query) || Hat(t, x.Hinweis))))
                Melde($"IDarf<{t.Name}> — dürfen kann man nur, was man hineingibt: einen konkreten Command, eine Query oder einen Hinweis");
        foreach (var t in hoert.Select(h => h.TypeArguments[0]))
        {
            if (Hat(t, x.Command))
                Melde($"IHoert<{t.Name}> — einen Command hört kein Akteur (sein Empfänger ist ein Aggregat bzw. der Kopf eines Graphen); hören kann man "
                    + "ein Fakt: ein Event oder einen Hinweis, auf das der Client reagiert");
            else if (!(Konkret(t) && (Hat(t, x.Event) || Hat(t, x.Hinweis))))
                Melde($"IHoert<{t.Name}> — hören kann man ein Event oder einen Hinweis");
        }
    }

    private static void PruefeClient(SymbolAnalysisContext ctx, Typen x)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        if (typ.ContainingNamespace?.ToDisplayString() == "Abstractions" || !Hat(typ, x.Client)) return;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        void Melde(DiagnosticDescriptor d, Location? o, string text) => ctx.ReportDiagnostic(Diagnostic.Create(d, o ?? ort, typ.Name, text));
        bool Ist(INamedTypeSymbol i, INamedTypeSymbol? g) => g != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, g);

        if (!typ.IsRecord || typ.TypeKind != TypeKind.Class)
        {
            Melde(ClientForm, ort, "ein Client ist ein Record (record X : IClient, IVerkoerpert<A>) — er nennt nur, wen er verkörpert");
            return;
        }
        // 063: nennt nur IClient und IVerkoerpert — nichts Fremdes, auch kein IDarf/IHoert (die stehen an seinen Akteuren).
        foreach (var b in typ.Interfaces.Where(b => !SymbolEqualityComparer.Default.Equals(b, x.Client) && !Ist(b, x.Verkoerpert)
                     && b.ToDisplayString() != "System.IEquatable<" + typ.ToDisplayString() + ">"))
            Melde(ClientForm, ort, $"nennt {b.Name} — ein Client nennt nur, wen er verkörpert (IVerkoerpert<A>); was über seine Leitung geht, sind "
                + "die IDarf und IHoert seiner Akteure");
        // 063: selbst deklariert er nichts.
        foreach (var m in typ.GetMembers().Where(m => !m.IsImplicitlyDeclared
                     && m is not IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet }))
            Melde(ClientForm, m.Locations.FirstOrDefault(), $"'{m.Name}' — ein Client deklariert nichts selbst; was er hört und hineingibt, steht an seinen Akteuren");
        // 064: IVerkoerpert: ein konkreter Akteur (Record, kein Interface).
        foreach (var t in typ.Interfaces.Where(b => Ist(b, x.Verkoerpert)).Select(b => b.TypeArguments[0]))
            if (!(Hat(t, x.Akteur) && Konkret(t)))
                Melde(ClientBefugt, ort, $"IVerkoerpert<{t.Name}> — verkörpern kann man nur einen konkreten Akteur (ein Record mit IAkteur)");
    }

    private static void PruefeApp(SymbolAnalysisContext ctx, INamedTypeSymbol iApp, Compilation c)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        if (typ.ContainingNamespace?.ToDisplayString() == "Abstractions"
            || !typ.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iApp))) return;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        void Melde(DiagnosticDescriptor d, Location? o, string text) => ctx.ReportDiagnostic(Diagnostic.Create(d, o ?? ort, typ.Name, text));
        if (!typ.IsRecord || typ.TypeKind != TypeKind.Class)
        {
            Melde(AppForm, ort, "eine App ist ein Record (record X(…) : IApp<Client>) — ihre Parameter sind ihre Quellen");
            return;
        }
        if (typ.Interfaces.Count(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iApp)) > 1)
            Melde(AppForm, ort, "setzt mehrere Clients um — eine App setzt genau einen Client um (IApp<Client>)");
        foreach (var b in typ.Interfaces.Where(b => !SymbolEqualityComparer.Default.Equals(b.OriginalDefinition, iApp)
                     && b.ToDisplayString() != "System.IEquatable<" + typ.ToDisplayString() + ">"))
            Melde(AppForm, ort, $"nennt {b.Name} — eine App nennt nur, welchen Client sie umsetzt (IApp<Client>)");
        foreach (var m in typ.GetMembers().Where(m => !m.IsImplicitlyDeclared
                     && m is not IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet }
                     && !(m is IPropertySymbol pr && typ.InstanceConstructors.Any(k => k.Parameters.Any(pa => pa.Name == pr.Name)))))
            Melde(AppForm, m.Locations.FirstOrDefault(), $"'{m.Name}' — eine App deklariert nichts selbst; ihre Parameter sind ihre Quellen");
        var v = Infrastructure.SourceGeneration.Clientvertragslesart.LiesApp(typ, c);
        var darf = new HashSet<ISymbol>(Infrastructure.SourceGeneration.Clientvertragslesart.Lies(v.Client!, c).Sendet, SymbolEqualityComparer.Default);
        foreach (var q in v.Quellen)
        {
            var qort = q.Parameter.Locations.FirstOrDefault() ?? ort;
            if (q.Quellart is not INamedTypeSymbol art || !Infrastructure.SourceGeneration.Funktionsform.IstQuellart(art))
            {
                Melde(AppForm, qort, $"Parameter {q.Name}: {q.Quellart.Name} ist keine Quellart (IQuellart) — ein Parameter einer App ist eine Quelle");
                continue;
            }
            if (q.Uebersetzungen.Count == 0)
            {
                Melde(AppForm, qort, $"Quelle {q.Name}: keine Übersetzung — [Uebersetzt<{{Meldung}}_{{Command}}_n>] nennt, was aus ihrer Meldung wird");
                continue;
            }
            var meldet = Infrastructure.SourceGeneration.Funktionsform.Lies(art).Meldet.ToList();
            foreach (var (u, quelle, ziel, a) in q.Uebersetzungen)
            {
                var uort = a.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken).GetLocation() ?? qort;
                if (quelle == null || ziel == null)
                    Melde(AppQuelle, uort, $"Quelle {q.Name}: {u.Name} ist keine Übersetzung (ITranslate<Meldung, Command>)");
                else if (!meldet.Any(m => SymbolEqualityComparer.Default.Equals(m, quelle)))
                    Melde(AppQuelle, uort, $"Quelle {q.Name}: {u.Name} übersetzt {quelle.Name} — {art.Name} meldet "
                        + string.Join(", ", meldet.Select(m => m.Name).DefaultIfEmpty("nichts")));
                else if (!darf.Contains(ziel))
                    Melde(AppQuelle, uort, $"Quelle {q.Name}: {u.Name} macht {ziel.Name} — das darf kein Akteur von {v.Client!.Name} (IDarf<{ziel.Name}>)");
            }
        }
    }
}
