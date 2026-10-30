using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Domain.SourceGeneration;

/// <summary>
/// <b>CQRS058 — Akteur-Befugnis.</b> <c>IDarf&lt;T&gt;</c> ist EIN Wort für alles, was ein Akteur in das System hineingibt: Command, Query,
/// Quell-Nachricht, Hinweis und die Ausgaben einer Funktion draußen (<c>IDarf&lt;IFunktion-Typ&gt;</c>). Alles andere (ein Event aus dem Log, eine
/// Antwort, ein Wertobjekt …) kann man nicht „dürfen" — das Hören wird aus dem Graphen abgeleitet. Und <c>IDarf</c> gehört an einen
/// <c>IAkteur</c>: an einem anderen Typ würde es niemand lesen. Beides ist ein Build-Fehler (docs/konzept-akteure.md).
/// <para><b>CQRS063/064 — Client-Vertrag</b> (<c>docs/konzept-akteure.md</c> §4, docs/konzept-akteure.md §5): <c>IClientVertrag</c> ist ein
/// Interface, das nur zusammenstellt — <c>IVerkoerpert&lt;A&gt;</c>, <c>IHoert&lt;T&gt;</c> und andere Client-Verträge — und selbst nichts
/// deklariert (063); wen er verkörpert, ist ein konkreter Akteur, was er hört, ein Event oder Hinweis (064). Was über seine Leitung hineingeht,
/// sind die <c>IDarf</c> seiner Akteure — es gibt kein zweites Wort dafür, also auch nichts zu prüfen.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AkteurAnalyzer : DiagnosticAnalyzer
{
    public const string BefugnisId = "CQRS058", ClientFormId = "CQRS063", ClientBefugtId = "CQRS064";

    private static readonly DiagnosticDescriptor Befugnis = new(
        BefugnisId, "Ungültige Akteur-Befugnis",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS063 — Form eines Client-Vertrags: Interface; erbt nur IVerkoerpert/IHoert/Client-Verträge; deklariert selbst nichts.</summary>
    private static readonly DiagnosticDescriptor ClientForm = new(
        ClientFormId, "Ungültiger Client-Vertrag",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS064 — der Rand eines Clients: verkörpern kann er nur einen konkreten Akteur, hören nur Events und Hinweise.</summary>
    private static readonly DiagnosticDescriptor ClientBefugt = new(
        ClientBefugtId, "Client verkörpert keinen Akteur oder hört etwas, das man nicht hören kann",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Befugnis, ClientForm, ClientBefugt);

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
            var hinein = new[] { T("Abstractions.ICommand"), T("Abstractions.IQuery"), T("Abstractions.ITransientEvent"), T("Abstractions.IQuellNachricht") }
                .Where(x => x != null).Cast<INamedTypeSymbol>().ToArray();
            var iFunktion = T("Abstractions.IFunktion");
            start.RegisterSymbolAction(ctx => Pruefe(ctx, iDarf, iAkteur, hinein, iFunktion), SymbolKind.NamedType);
            var cl = new ClientTypen(T("Abstractions.IClientVertrag"), T("Abstractions.IVerkoerpert`1"), T("Abstractions.IHoert`1"),
                T("Abstractions.IEvent"), T("Abstractions.ITransientEvent"), iAkteur);
            if (cl.Client != null)
                start.RegisterSymbolAction(ctx => PruefeClient(ctx, cl), SymbolKind.NamedType);
        });
    }

    private static void Pruefe(SymbolAnalysisContext ctx, INamedTypeSymbol iDarf, INamedTypeSymbol iAkteur, INamedTypeSymbol[] hinein,
        INamedTypeSymbol? iFunktion)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        // Ein Akteur ist ein Record (Domänen-Experte), nie ein Interface (ein Dienst, ein Vertrag).
        if (typ.TypeKind == TypeKind.Interface && typ.ContainingNamespace?.ToDisplayString() != "Abstractions"
            && typ.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
            ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                "ein Interface ist kein Akteur — Akteure sind Records (IMensch/IMaschine/IKi)"));
        var darf = typ.Interfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).ToList();
        if (darf.Count == 0) return;

        if (!typ.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
            ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                "IDarf<…> steht nur an einem Akteur — ergänze IAkteur in der Basisliste"));

        foreach (var d in darf)
        {
            var t = d.TypeArguments[0];
            // Eine Funktion draußen (Interface mit IFunktion): der Akteur darf sie RECHNEN — ihre Ausgaben gibt er hinein.
            if (iFunktion != null && t.TypeKind == TypeKind.Interface && t.AllInterfaces.Contains(iFunktion, SymbolEqualityComparer.Default))
                continue;
            var erlaubt = hinein.Any(h => SymbolEqualityComparer.Default.Equals(t, h)
                || t.AllInterfaces.Contains(h, SymbolEqualityComparer.Default));
            if (!erlaubt || t.TypeKind == TypeKind.Interface || t.IsAbstract)
                ctx.ReportDiagnostic(Diagnostic.Create(Befugnis, ort, typ.Name,
                    $"IDarf<{t.Name}> — dürfen kann man nur, was man hineingibt: einen konkreten Command, eine Query, "
                    + "eine Quell-Nachricht, einen Hinweis oder die Ausgaben einer Funktion draußen (IDarf<IFunktion-Typ>: er darf sie rechnen) — was "
                    + "ein Akteur hört, wird aus dem Graphen abgeleitet"));
        }
    }

    // ── Client-Vertrag (CQRS063/064, docs/konzept-akteure.md §4) ─────────────────────────────────────────────

    private sealed class ClientTypen
    {
        public readonly INamedTypeSymbol? Client, Verkoerpert, Hoert, Event, Hinweis, Akteur;
        public ClientTypen(INamedTypeSymbol? client, INamedTypeSymbol? verkoerpert, INamedTypeSymbol? hoert, INamedTypeSymbol? evt,
            INamedTypeSymbol? hinweis, INamedTypeSymbol? akteur)
        {
            Client = client; Verkoerpert = verkoerpert; Hoert = hoert; Event = evt; Hinweis = hinweis; Akteur = akteur;
        }
    }

    private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
        (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

    private static bool IstClient(INamedTypeSymbol t, ClientTypen cl) =>
        cl.Client != null && t.AllInterfaces.Contains(cl.Client, SymbolEqualityComparer.Default);

    private static void PruefeClient(SymbolAnalysisContext ctx, ClientTypen cl)
    {
        var typ = (INamedTypeSymbol)ctx.Symbol;
        if (typ.ContainingNamespace?.ToDisplayString() == "Abstractions" || !IstClient(typ, cl)) return;
        var ort = typ.Locations.FirstOrDefault() ?? Location.None;
        void Melde(DiagnosticDescriptor d, Location? o, string text) => ctx.ReportDiagnostic(Diagnostic.Create(d, o ?? ort, typ.Name, text));
        bool Ist(INamedTypeSymbol i, INamedTypeSymbol? g) => g != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, g);

        if (typ.TypeKind != TypeKind.Interface)
        {
            Melde(ClientForm, ort, "ein Client-Vertrag ist ein Interface — implementiert wird er draußen (vom Client, gegen die generierte Basis)");
            return;
        }

        // 063: erben nur IVerkoerpert/IHoert, IClientVertrag bzw. andere Client-Verträge — nichts Fremdes.
        foreach (var b in typ.Interfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(b, cl.Client) || Ist(b, cl.Verkoerpert) || Ist(b, cl.Hoert) || IstClient(b, cl)) continue;
            Melde(ClientForm, ort, $"erbt {b.Name} — ein Client-Vertrag nennt nur, wen er verkörpert (IVerkoerpert<A>) und was er hört (IHoert<T>); "
                + "was über seine Leitung hineingeht, sind die IDarf seiner Akteure");
        }
        // 063: selbst deklariert er nichts — Kenntnis ist IHoert<T>, eine Ausgabe gehört in eine Funktion, die ein verkörperter Akteur rechnet.
        foreach (var m in typ.GetMembers().Where(m => !m.IsImplicitlyDeclared))
            Melde(ClientForm, m.Locations.FirstOrDefault(), $"'{m.Name}' — ein Client-Vertrag deklariert nichts selbst: Kenntnis ist IHoert<T>, was er "
                + "auf ein Event ausgibt, ist eine Funktion, die ein verkörperter Akteur rechnen darf (IDarf<F>)");

        // 064: IVerkoerpert: ein konkreter Akteur (Record, kein Interface). IHoert: ein konkretes Event oder ein Hinweis (Kenntnis braucht keinen Akteur).
        foreach (var b in typ.Interfaces.Where(b => Ist(b, cl.Verkoerpert) || Ist(b, cl.Hoert)))
        {
            var t = b.TypeArguments[0];
            if (Ist(b, cl.Verkoerpert))
            {
                if (!(Hat(t, cl.Akteur) && t.TypeKind != TypeKind.Interface && !t.IsAbstract))
                    Melde(ClientBefugt, ort, $"IVerkoerpert<{t.Name}> — verkörpern kann man nur einen konkreten Akteur (ein Record mit IAkteur)");
                continue;
            }
            if (!((Hat(t, cl.Event) || Hat(t, cl.Hinweis)) && t.TypeKind != TypeKind.Interface && !t.IsAbstract))
                Melde(ClientBefugt, ort, $"IHoert<{t.Name}> — hören kann man ein konkretes Event aus dem Log oder einen Hinweis");
        }
    }
}
