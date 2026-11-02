using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS058 — Akteur.</b> Ein Akteur ist ein Record mit <c>IAkteur</c>. <c>IDarf&lt;T&gt;</c> ist EIN Wort für alles, was er hineingibt: ein
/// konkreter Command (an ein Aggregat oder ohne Ziel), eine Query, ein Hinweis. <c>IHoert&lt;T&gt;</c> ist EIN Wort für alles, was zu ihm kommt: ein
/// Command ohne Ziel (Arbeit), ein Event oder ein Hinweis (Kenntnis) — nie ein Command an ein Aggregat. Beide stehen nur an einem Akteur; alles
/// andere ist ein Build-Fehler (docs/konzept-akteure.md).
/// <para><b>CQRS063/064 — Client</b> (<c>docs/konzept-akteure.md</c> §4): ein Client ist ein Record mit <c>IClient</c>, der nur nennt, wen er
/// verkörpert (<c>IVerkoerpert&lt;A&gt;</c>), und selbst nichts deklariert (063); verkörpern kann er nur einen konkreten Akteur (064). Was über seine
/// Leitung geht, sind die <c>IDarf</c> und <c>IHoert</c> seiner Akteure — es gibt kein zweites Wort dafür.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AkteurAnalyzer : DiagnosticAnalyzer
{
    public const string BefugnisId = "CQRS058", ClientFormId = "CQRS063", ClientBefugtId = "CQRS064";

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
        ImmutableArray.Create(Befugnis, ClientForm, ClientBefugt);

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
            if (Hat(t, x.AggregatCommand))
                Melde($"IHoert<{t.Name}> — einen Command an ein Aggregat hört kein Akteur (sein Empfänger ist das Aggregat); Arbeit ist ein Command ohne Ziel");
            else if (!(Konkret(t) && (Hat(t, x.Command) || Hat(t, x.Event) || Hat(t, x.Hinweis))))
                Melde($"IHoert<{t.Name}> — hören kann man einen Command ohne Ziel (Arbeit), ein Event oder einen Hinweis (Kenntnis)");
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
        foreach (var m in typ.GetMembers().Where(m => !m.IsImplicitlyDeclared && m is not IMethodSymbol { MethodKind: MethodKind.Constructor }))
            Melde(ClientForm, m.Locations.FirstOrDefault(), $"'{m.Name}' — ein Client deklariert nichts selbst; was er hört und hineingibt, steht an seinen Akteuren");
        // 064: IVerkoerpert: ein konkreter Akteur (Record, kein Interface).
        foreach (var t in typ.Interfaces.Where(b => Ist(b, x.Verkoerpert)).Select(b => b.TypeArguments[0]))
            if (!(Hat(t, x.Akteur) && Konkret(t)))
                Melde(ClientBefugt, ort, $"IVerkoerpert<{t.Name}> — verkörpern kann man nur einen konkreten Akteur (ein Record mit IAkteur)");
    }
}
