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
/// <para><b>CQRS063/064 — Client-Vertrag</b> (<c>docs/konzept-akteure.md</c> §4, Plan Pub/Sub-Neustrukturierung §2.7): <c>IClientVertrag</c> ist ein
/// Interface, das nur zusammenstellt — <c>ITraegt&lt;F&gt;</c>, <c>ISendet&lt;T&gt;</c>, <c>IFragt&lt;T&gt;</c>, <c>IHoert&lt;T&gt;</c> und andere
/// Client-Verträge — und selbst nichts deklariert (063); was er trägt, sendet oder fragt, darf ein Akteur, was er hört, ist ein Event oder
/// Hinweis (064).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AkteurAnalyzer : DiagnosticAnalyzer
{
    public const string BefugnisId = "CQRS058", ClientFormId = "CQRS063", ClientBefugtId = "CQRS064";

    private static readonly DiagnosticDescriptor Befugnis = new(
        BefugnisId, "Ungültige Akteur-Befugnis",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS063 — Form eines Client-Vertrags: Interface; erbt nur ITraegt/ISendet/IFragt/IHoert/Client-Verträge; deklariert selbst nichts.</summary>
    private static readonly DiagnosticDescriptor ClientForm = new(
        ClientFormId, "Ungültiger Client-Vertrag",
        "'{0}': {1}",
        "CQRS.Akteur", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CQRS064 — Rand ⊆ Befugnis: was ein Client trägt/sendet/fragt, darf ein Akteur (<c>IDarf</c>); hören kann er nur Events und Hinweise.</summary>
    private static readonly DiagnosticDescriptor ClientBefugt = new(
        ClientBefugtId, "Client trägt/sendet/fragt, was kein Akteur darf",
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
            var hinein = new[] { T("Abstractions.ICommand"), T("Abstractions.IQuery"), T("Abstractions.IPipelineTrigger"), T("Abstractions.ITransientEvent"), T("Abstractions.IQuellNachricht") }
                .Where(x => x != null).Cast<INamedTypeSymbol>().ToArray();
            var iFunktion = T("Abstractions.IFunktion");
            start.RegisterSymbolAction(ctx => Pruefe(ctx, iDarf, iAkteur, hinein, iFunktion), SymbolKind.NamedType);
            var cl = new ClientTypen(T("Abstractions.IClientVertrag"), T("Abstractions.ISendet`1"), T("Abstractions.IFragt`1"), T("Abstractions.IHoert`1"),
                T("Abstractions.ITraegt`1"), T("Abstractions.IQuery"), T("Abstractions.IEvent"), T("Abstractions.ITransientEvent"), iFunktion, hinein);
            // IDarf-Halter je Typ (alle Akteure der Compilation inkl. Referenzen, einmal je Compilation): CQRS064.
            var halter = new System.Lazy<Dictionary<ITypeSymbol, List<string>>>(() => SammleHalter(c, iDarf, iAkteur));
            if (cl.Client != null)
                start.RegisterSymbolAction(ctx => PruefeClient(ctx, cl, halter.Value), SymbolKind.NamedType);
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
        public readonly INamedTypeSymbol? Client, Sendet, Fragt, Hoert, Traegt, Query, Event, Hinweis, Funktion;
        public readonly INamedTypeSymbol[] Hinein;
        public ClientTypen(INamedTypeSymbol? client, INamedTypeSymbol? sendet, INamedTypeSymbol? fragt, INamedTypeSymbol? hoert,
            INamedTypeSymbol? traegt, INamedTypeSymbol? query, INamedTypeSymbol? evt, INamedTypeSymbol? hinweis, INamedTypeSymbol? funktion,
            INamedTypeSymbol[] hinein)
        {
            Client = client; Sendet = sendet; Fragt = fragt; Hoert = hoert; Traegt = traegt; Query = query; Event = evt; Hinweis = hinweis;
            Funktion = funktion; Hinein = hinein;
        }
    }

    private static bool Hat(ITypeSymbol t, INamedTypeSymbol? i) => i != null &&
        (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i)));

    private static bool IstClient(INamedTypeSymbol t, ClientTypen cl) =>
        cl.Client != null && t.AllInterfaces.Contains(cl.Client, SymbolEqualityComparer.Default);

    /// <summary>Typ → die Akteure mit <c>IDarf&lt;Typ&gt;</c> (über die ganze Compilation inkl. Referenzen).</summary>
    private static Dictionary<ITypeSymbol, List<string>> SammleHalter(Compilation c, INamedTypeSymbol iDarf, INamedTypeSymbol iAkteur)
    {
        var d = new Dictionary<ITypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        void Lauf(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers())
                if (t.TypeKind is TypeKind.Class or TypeKind.Struct && t.AllInterfaces.Contains(iAkteur, SymbolEqualityComparer.Default))
                    foreach (var i in t.AllInterfaces.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)))
                    {
                        if (!d.TryGetValue(i.TypeArguments[0], out var l)) d[i.TypeArguments[0]] = l = new List<string>();
                        if (!l.Contains(t.Name)) l.Add(t.Name);
                    }
            foreach (var sub in ns.GetNamespaceMembers()) Lauf(sub);
        }
        Lauf(c.GlobalNamespace);
        return d;
    }

    private static void PruefeClient(SymbolAnalysisContext ctx, ClientTypen cl, Dictionary<ITypeSymbol, List<string>> halter)
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

        // 063: erben nur ITraegt/ISendet/IFragt/IHoert, IClientVertrag bzw. andere Client-Verträge — nichts Fremdes.
        foreach (var b in typ.Interfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(b, cl.Client) || Ist(b, cl.Sendet) || Ist(b, cl.Fragt) || Ist(b, cl.Hoert) || Ist(b, cl.Traegt)
                || IstClient(b, cl)) continue;
            Melde(ClientForm, ort, $"erbt {b.Name} — ein Client-Vertrag stellt nur zusammen: ITraegt<F>, ISendet<T>, IFragt<T>, IHoert<T>");
        }
        // 063: selbst deklariert er nichts — Kenntnis ist IHoert<T>, eine Ausgabe gehört in eine getragene Funktion.
        foreach (var m in typ.GetMembers().Where(m => !m.IsImplicitlyDeclared))
            Melde(ClientForm, m.Locations.FirstOrDefault(), $"'{m.Name}' — ein Client-Vertrag deklariert nichts selbst: Kenntnis ist IHoert<T>, was er "
                + "auf ein Event ausgibt, ist eine Funktion, die er trägt (ITraegt<F>)");

        // 064: Rand ⊆ Befugnis. ISendet: Command/Quell-Nachricht/Hinweis; IFragt: Query; ITraegt: Funktion als Schnittstelle — und ein Akteur hat
        //      IDarf<T>. IHoert: ein konkretes Event oder ein Hinweis (Kenntnis braucht keinen Akteur).
        foreach (var b in typ.Interfaces.Where(b => Ist(b, cl.Sendet) || Ist(b, cl.Fragt) || Ist(b, cl.Traegt) || Ist(b, cl.Hoert)))
        {
            var t = b.TypeArguments[0];
            if (Ist(b, cl.Hoert))
            {
                if (!((Hat(t, cl.Event) || Hat(t, cl.Hinweis)) && t.TypeKind != TypeKind.Interface && !t.IsAbstract))
                    Melde(ClientBefugt, ort, $"IHoert<{t.Name}> — hören kann man ein konkretes Event aus dem Log oder einen Hinweis");
                continue;
            }
            var wort = Ist(b, cl.Fragt) ? "IFragt" : Ist(b, cl.Traegt) ? "ITraegt" : "ISendet";
            var art = wort switch
            {
                "IFragt" => cl.Query != null && Hat(t, cl.Query) && t.TypeKind != TypeKind.Interface && !t.IsAbstract,
                "ITraegt" => cl.Funktion != null && Hat(t, cl.Funktion) && t.TypeKind == TypeKind.Interface,
                _ => cl.Hinein.Any(h => !SymbolEqualityComparer.Default.Equals(h, cl.Query) && Hat(t, h)) && t.TypeKind != TypeKind.Interface && !t.IsAbstract,
            };
            if (!art)
            {
                Melde(ClientBefugt, ort, wort switch
                {
                    "IFragt" => $"IFragt<{t.Name}> — fragen kann man nur eine konkrete Query",
                    "ITraegt" => $"ITraegt<{t.Name}> — tragen kann man eine Funktion, die als Schnittstelle deklariert ist (läuft draußen)",
                    _ => $"ISendet<{t.Name}> — senden kann man einen konkreten Command, eine Quell-Nachricht oder einen Hinweis (Queries: IFragt<T>)",
                });
                continue;
            }
            if (!halter.ContainsKey(t))
                Melde(ClientBefugt, ort, $"{wort}<{t.Name}> — kein Akteur darf {t.Name} (IDarf<{t.Name}>); der Client-Vertrag schneidet aus der "
                    + "Befugnis eines Akteurs, er erweitert sie nicht");
        }
    }
}
