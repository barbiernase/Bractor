using System.Collections.Immutable;
using System.Linq;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Domain.SourceGeneration;

/// <summary>
/// Gleiche Eingabe, gleiche Rechnung (Plan Pub/Sub-Neustrukturierung §3.6) — was eine Wiederholung voraussetzt, prüft der Build:
/// <list type="bullet">
/// <item><b>CQRS102 (R11)</b> — ein Aggregat (Decider, Applier, State) und eine Projektion (Handle mit Schreib-Fähigkeit) rechnen bei gleicher Eingabe
///   gleich: keine Uhr, kein Zufall, kein Lesen fremder Daten (Dateien, Netz, Umgebungsvariablen). Sonst ergäbe ein Neuaufbau einen anderen Stand,
///   und nach einem halb geschriebenen Batch stünden Werte aus zwei Läufen nebeneinander. Zeit kommt aus dem Event bzw. der <c>Herkunft</c>.</item>
/// <item><b>CQRS103 (R12)</b> — eine Funktion, die das Framework als gleichbleibend behandelt (Klasse ohne Konstruktor-Parameter und ohne
///   Lese-Fähigkeit, <see cref="Funktionsform.Gleichbleibend"/>), sendet ihre Ausgabe OHNE sie festzuhalten — sie darf deshalb weder Uhr noch Zufall
///   noch fremde Daten nutzen. Was sich ändern kann, gehört in einen Dienst; dann hält das Framework die Ausgabe fest.</item>
/// <item><b>CQRS104 (R13)</b> — neue Ids auf der Server-Seite nur abgeleitet (<c>Herkunft.NeueId</c>), nie <c>Guid.NewGuid()</c> oder
///   <c>Id&lt;T&gt;.Neu()</c> in Aggregat, Projektion, Reader oder Funktion im Host: eine Wiederholung bekäme sonst ein anderes Ziel, also eine
///   andere Command-Id — die Inbox erkennte sie nicht. Clients würfeln (sie halten die Command-Id fest).</item>
/// </list>
/// Gelesen werden nur Code-Fakten: die Symbole, die ein Rumpf aufruft, und der Baustein, in dem er steht (Marker, Signatur).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GleichbleibendAnalyzer : DiagnosticAnalyzer
{
    public const string GleichId = "CQRS102", GleichbleibendId = "CQRS103", NeueIdId = "CQRS104";

    private static DiagnosticDescriptor D(string id, string titel, string text) =>
        new(id, titel, text, "CQRS.Wiederholung", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Gleich = D(GleichId, "Aggregat oder Projektion rechnet nicht gleich",
        "'{0}' ist {1} und nutzt {2} ({3}) — bei gleicher Eingabe muss gleich gerechnet werden (R11); Zeit kommt aus dem Event bzw. der Herkunft");
    private static readonly DiagnosticDescriptor Gleichbleibend = D(GleichbleibendId, "Gleichbleibende Funktion nutzt Uhr, Zufall oder fremde Daten",
        "Die Funktion '{0}' hat weder Dienst noch Lese-Fähigkeit und sendet ihre Ausgabe deshalb ohne sie festzuhalten — sie nutzt aber {1} ({2}). "
        + "Nimm die Herkunft (Zeit, NeueId) oder einen Dienst im Konstruktor; dann hält das Framework die Ausgabe fest (R12)");
    private static readonly DiagnosticDescriptor NeueId = D(NeueIdId, "Neue Id nicht abgeleitet",
        "'{0}' ({1}) würfelt eine neue Id mit {2} — auf der Server-Seite nur abgeleitet: Herkunft.NeueId<T>(…), sonst bekommt eine Wiederholung ein anderes Ziel (R13)");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Gleich, Gleichbleibend, NeueId);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Pruefe, OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.ObjectCreation,
            OperationKind.FieldReference);
    }

    private enum Art { Uhr, Zufall, NeueId, FremdeDaten }

    private static (Art Art, string Was)? Verboten(IOperation op)
    {
        ISymbol? s = op switch
        {
            IInvocationOperation i => i.TargetMethod,
            IPropertyReferenceOperation p => p.Property,
            IFieldReferenceOperation f => f.Field,
            IObjectCreationOperation o => o.Constructor,
            _ => null,
        };
        if (s?.ContainingType is not { } typ) return null;
        var t = Funktionsform.Name(typ);
        var was = typ.Name + "." + (s is IMethodSymbol { MethodKind: MethodKind.Constructor } ? "new" : s.Name);
        switch (t)
        {
            case "System.DateTime" when s.Name is "Now" or "UtcNow" or "Today":
            case "System.DateTimeOffset" when s.Name is "Now" or "UtcNow":
            case "System.Environment" when s.Name is "TickCount" or "TickCount64":
            case "System.Diagnostics.Stopwatch":
            case "System.TimeProvider":
                return (Art.Uhr, was);
            case "System.Random":
            case "System.Security.Cryptography.RandomNumberGenerator":
                return (Art.Zufall, was);
            case "System.Guid" when s.Name is "NewGuid" or "CreateVersion7":
            case "Abstractions.Id`1" when s.Name == "Neu":
                return (Art.NeueId, was);
            case "System.IO.File":
            case "System.IO.Directory":
            case "System.Net.Http.HttpClient":
            case "System.Environment" when s.Name is "GetEnvironmentVariable" or "GetEnvironmentVariables":
                return (Art.FremdeDaten, was);
        }
        return null;
    }

    private static void Pruefe(OperationAnalysisContext ctx)
    {
        if (Verboten(ctx.Operation) is not { } v) return;
        var typ = ctx.ContainingSymbol.ContainingType;
        if (typ == null) return;
        var oben = typ;
        while (oben.ContainingType != null) oben = oben.ContainingType;
        var ort = ctx.Operation.Syntax.GetLocation();

        var baustein = Baustein(oben);
        if (baustein == null) return;
        if (v.Art == Art.NeueId)
        {
            if (baustein.Value.Art != BausteinArt.Andere)
                ctx.ReportDiagnostic(Diagnostic.Create(NeueId, ort, oben.Name, baustein.Value.Text, v.Was));
            return;
        }
        if (baustein.Value.Art is BausteinArt.Aggregat or BausteinArt.Projektion)
            ctx.ReportDiagnostic(Diagnostic.Create(Gleich, ort, oben.Name, baustein.Value.Text, Text(v.Art), v.Was));
        else if (baustein.Value.Art == BausteinArt.GleichbleibendeFunktion)
            ctx.ReportDiagnostic(Diagnostic.Create(Gleichbleibend, ort, oben.Name, Text(v.Art), v.Was));
    }

    private enum BausteinArt { Aggregat, Projektion, Reader, Funktion, GleichbleibendeFunktion, Andere }

    /// <summary>Welcher Baustein der Server-Seite <paramref name="t"/> ist — nur aus Markern und Signaturen; null = keiner (Client, Hilfscode …).</summary>
    private static (BausteinArt Art, string Text)? Baustein(INamedTypeSymbol t)
    {
        if (Funktionsform.Hat(t, "Abstractions.IState")) return (BausteinArt.Aggregat, "ein Aggregat");
        if (Funktionsform.Hat(t, "Abstractions.ISubscriber"))
        {
            var schreibt = t.GetMembers().OfType<IMethodSymbol>().Any(m => m.Parameters.Any(p => Funktionsform.Hat(p.Type, "Abstractions.IWriteStore")));
            return schreibt ? (BausteinArt.Projektion, "eine Projektion") : (BausteinArt.Andere, "eine Reaktion");
        }
        if (t.AllInterfaces.Any(i => Funktionsform.Name(i) == "Abstractions.IReader`1")) return (BausteinArt.Reader, "ein Reader");
        if (t.TypeKind == TypeKind.Class && Funktionsform.IstBaustein(t))
            return Funktionsform.Gleichbleibend(Funktionsform.Lies(t)) ? (BausteinArt.GleichbleibendeFunktion, "eine Funktion") : (BausteinArt.Funktion, "eine Funktion");
        // Die Umsetzung einer Funktion draußen (Schnittstelle mit Auf), wenn sie im Host rechnet.
        if (t.TypeKind == TypeKind.Class && Funktionsform.Umsetzt(t).Any(Funktionsform.IstBaustein)) return (BausteinArt.Funktion, "eine Funktion");
        return null;
    }

    private static string Text(Art a) => a switch
    {
        Art.Uhr => "die Uhr",
        Art.Zufall => "Zufall",
        Art.FremdeDaten => "fremde Daten",
        _ => "eine gewürfelte Id",
    };
}
