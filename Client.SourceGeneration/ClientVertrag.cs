// ── Client.SourceGeneration/ClientVertrag.cs ──
//
// Der Client-Vertrag einer Client-Assembly ([assembly: IstClient<IVertrag>], docs/konzept-akteure.md §4) — gelesen mit derselben Lesart
// wie am Server (Clientvertragslesart, per Link), dazu die Diagnosen CQRS093–097: der Client-Code wird gegen seinen Vertrag gehalten.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Client.SourceGeneration;

/// <summary>Was der Wiring-Generator vom Vertrag braucht (Typen als Anzeige-Namen wie die übrigen Domänen-Typen).</summary>
internal sealed class ClientVertragInfo
{
    public string Typ { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Hash { get; private set; } = "";
    public List<string> Sendet { get; private set; } = new List<string>();
    public List<string> Fragt { get; private set; } = new List<string>();
    /// <summary>Was der Client hört (<c>IHoert&lt;T&gt;</c>) — genau das wird abonniert.</summary>
    public List<string> Hoert { get; private set; } = new List<string>();
    /// <summary>Getragene Funktionen (<c>ITraegt&lt;F&gt;</c>) — ein C#-Client trägt (noch) keine (CQRS094).</summary>
    public List<string> Traegt { get; private set; } = new List<string>();

    public static readonly ClientVertragInfo Leer = new ClientVertragInfo();

    /// <summary>Der Vertrags-Typ aus <c>[assembly: IstClient&lt;T&gt;]</c>, sonst null.</summary>
    public static INamedTypeSymbol? VertragsTyp(Compilation c) =>
        c.Assembly.GetAttributes()
            .Where(a => a.AttributeClass is { IsGenericType: true } k
                        && k.OriginalDefinition.ToDisplayString() == "Abstractions.IstClientAttribute<TVertrag>")
            .Select(a => a.AttributeClass!.TypeArguments[0]).OfType<INamedTypeSymbol>().FirstOrDefault();

    public static ClientVertragInfo? Aus(Compilation c)
    {
        var typ = VertragsTyp(c);
        if (typ == null) return null;
        var v = Clientvertragslesart.Lies(typ, c);
        return new ClientVertragInfo
        {
            Typ = typ.ToDisplayString(),
            Name = v.Name,
            Hash = v.Hash,
            Sendet = v.Sendet.Select(t => t.ToDisplayString()).OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
            Fragt = v.Fragt.Select(t => t.ToDisplayString()).OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
            Hoert = v.Hoert.Select(t => t.ToDisplayString()).OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
            Traegt = v.Traegt.Select(t => t.Funktion.Name).ToList(),
        };
    }
}

internal static class ClientVertragDiagnosen
{
    private const string Kategorie = "CQRS.Client";

    public static readonly DiagnosticDescriptor OhneVertrag = new DiagnosticDescriptor(
        "CQRS093", "Client ohne Vertrag",
        "Die Client-Assembly '{0}' nennt ihren Vertrag nicht — [assembly: IstClient<IMeinClient>] ergänzen (IMeinClient : IClientVertrag); "
        + "ohne Vertrag wüsste niemand, was sie sendet, fragt und hört (docs/konzept-akteure.md §4)",
        Kategorie, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TeileImCSharpClient = new DiagnosticDescriptor(
        "CQRS094", "C#-Client trägt Funktionen",
        "Der Client-Vertrag '{0}' trägt Funktionen ({1}) — draußen trägt sie heute nur das Python-SDK (generierte Basen); ein C#-Client nennt "
        + "ISendet/IFragt/IHoert",
        Kategorie, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandOhneSendet = new DiagnosticDescriptor(
        "CQRS095", "Command nicht im Client-Vertrag",
        "Der Client erzeugt '{0}', sein Vertrag '{1}' nennt ISendet<{0}> nicht — der Server würde den Command ablehnen; in den Vertrag "
        + "aufnehmen (ein verkörperter Akteur muss ihn dürfen) oder im Client einen anderen Weg wählen",
        Kategorie, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor QueryOhneFragt = new DiagnosticDescriptor(
        "CQRS096", "Query nicht im Client-Vertrag",
        "Der Client stellt '{0}', sein Vertrag '{1}' nennt IFragt<{0}> nicht — der Server würde die Query ablehnen",
        Kategorie, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EventOhneKenntnis = new DiagnosticDescriptor(
        "CQRS097", "Server-Event nicht im Client-Vertrag",
        "'{2}.Handle({0})' hört ein Server-Event, das der Vertrag '{1}' nicht hört (IHoert<{0}>) — abonniert wird nur, was im "
        + "Vertrag steht; der Handler liefe nie",
        Kategorie, DiagnosticSeverity.Error, isEnabledByDefault: true);
}

/// <summary>
/// Hält den Client-Code gegen seinen Vertrag (CQRS095–097): jede erzeugte Domänen-Nachricht (Command, Query) muss der Vertrag senden bzw.
/// fragen, jeder <c>Handle(E, MessageContext)</c> auf ein Server-Event muss es kennen. Code-Fakten: Objekterzeugung und Signatur, keine Namen.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClientVertragAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        ClientVertragDiagnosen.CommandOhneSendet, ClientVertragDiagnosen.QueryOhneFragt, ClientVertragDiagnosen.EventOhneKenntnis);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var c = start.Compilation;
            var typ = ClientVertragInfo.VertragsTyp(c);
            if (typ == null) return;   // CQRS093 meldet der Wiring-Generator
            var v = Clientvertragslesart.Lies(typ, c);
            var iCommand = c.GetTypeByMetadataName("Abstractions.ICommand");
            var iQuery = c.GetTypeByMetadataName("Abstractions.IQuery");
            var iEvent = c.GetTypeByMetadataName("Abstractions.IEvent");
            var iClientEvent = c.GetTypeByMetadataName("Client.Infrastructure.Abstractions.IClientEvent");
            var ctx = c.GetTypeByMetadataName("Client.Infrastructure.Abstractions.MessageContext");
            var sendet = new HashSet<ISymbol>(v.Sendet, SymbolEqualityComparer.Default);
            var fragt = new HashSet<ISymbol>(v.Fragt, SymbolEqualityComparer.Default);
            var eingaenge = new HashSet<ISymbol>(v.Hoert, SymbolEqualityComparer.Default);

            start.RegisterOperationAction(o =>
            {
                if (((IObjectCreationOperation)o.Operation).Type is not INamedTypeSymbol t) return;
                if (Clientvertragslesart.Implementiert(t, iCommand) && !sendet.Contains(t))
                    o.ReportDiagnostic(Diagnostic.Create(ClientVertragDiagnosen.CommandOhneSendet, o.Operation.Syntax.GetLocation(), t.Name, v.Name));
                else if (Clientvertragslesart.Implementiert(t, iQuery) && !fragt.Contains(t))
                    o.ReportDiagnostic(Diagnostic.Create(ClientVertragDiagnosen.QueryOhneFragt, o.Operation.Syntax.GetLocation(), t.Name, v.Name));
            }, OperationKind.ObjectCreation);

            start.RegisterSymbolAction(s =>
            {
                var m = (IMethodSymbol)s.Symbol;
                if (m.Name != "Handle" || m.Parameters.Length != 2 || ctx == null
                    || !SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, ctx)
                    || m.Parameters[0].Type is not INamedTypeSymbol e) return;
                if (!Clientvertragslesart.Implementiert(e, iEvent) || Clientvertragslesart.Implementiert(e, iClientEvent)) return;
                if (!eingaenge.Contains(e))
                    s.ReportDiagnostic(Diagnostic.Create(ClientVertragDiagnosen.EventOhneKenntnis, m.Locations.FirstOrDefault() ?? Location.None,
                        e.Name, v.Name, m.ContainingType.Name));
            }, SymbolKind.Method);
        });
    }
}
