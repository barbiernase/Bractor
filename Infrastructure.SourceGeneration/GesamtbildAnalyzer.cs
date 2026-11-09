using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Infrastructure.SourceGeneration;

/// <summary>
/// Regeln, die das GESAMTBILD aller Domänen-Projekte brauchen — Erzeuger und Konsumenten liegen in verschiedenen Projekten (ein Command ohne Ziel
/// in einem Katalog, die Übersetzung, die ihn nimmt, in <c>Domain</c>; eine Query in <c>Domain</c>, ihr Reader in <c>Domain.Projections</c>).
/// Darum laufen sie in der Laufzeit-Compilation, die alle Domänen-Projekte referenziert, und lesen nur Signaturen aus den Metadaten
/// (Decide, Handles, Funktionen, Akteure, Clients) — Rümpfe werden nicht gebraucht (R1: der Ausgang steht in der Signatur).
/// <list type="bullet">
/// <item>CQRS084 (GR-QUERY, R4) — eine Query hat genau ein Ziel: einen Reader oder ein Aggregat (<c>Frage</c> im Decider, docs/03 §3.2).</item>
/// <item>CQRS085 (GR-MODUL-AUSGANG-OFFEN, UNUSED-EVENT; Warnung) — ein Event, eine Ablehnung oder eine Response, die niemand erzeugt.</item>
/// <item>CQRS108 (GR-ANTWORT; Fehler) — eine Funktion hört ein Event, das keiner ihrer Commands entscheiden kann: eine Funktion liest nie das Log, ein
///   Event erreicht sie nur als Antwort auf einen eigenen Command (docs/04 §4.4) — dieses käme nie.</item>
/// <item>CQRS086 (GR-MODUL-EINGANG-OFFEN; Warnung) — ein Command ohne Decider, eine Query ohne Reader und ohne Aggregat.</item>
/// <item>CQRS116 (GR-EMPFAENGER; Fehler) — ein Command ohne Ziel, der seinen Erzeuger verlässt (von einem Akteur, aus einem Knoten, den kein Knoten
///   desselben Graphen nimmt), hat genau einen Empfänger: den Kopf genau eines Graphen. Kein Empfänger oder mehrere sind ein Fehler. Ein Graph
///   in einem Katalog ist eine Blaupause (docs/10 §10.9): er läuft erst als Knoten eines Graphen der Anwendung — seine Köpfe
///   empfangen nichts, seine Ausgänge verlassen nichts, geprüft wird dort, wo er steht.</item>
/// <item>CQRS117 (GR-GRAPH-KNOTEN; Warnung) — eine Funktion, die in keinem Graphen ein Knoten ist, läuft nie (ein Graph läuft als Instanz).</item>
/// <item>CQRS120 (GR-UEBERSETZUNG; Fehler) — ein Command eines Katalogs verlässt einen Graphen der Anwendung: jeder Fall wird übersetzt
///   (<c>Uebersetze&lt;TEin, TAus&gt;</c>) oder von einem Knoten genommen, so kennt die Domäne keine Funktionsnachricht (A3). Ein Graph, der als Schachtel
///   in einem anderen steht, gibt seine Ausgänge dem äußeren — dort wird geprüft.</item>
/// <item>CQRS123 (GR-UEBERSETZUNG-ANTWORT; Fehler) — eine Antwort-Übersetzung nimmt ein Event, das kein <c>Decide</c> eines Commands ausgeben kann,
///   den eine Übersetzung erzeugt: diese Antwort käme nie.</item>
/// <item>CQRS124 (GR-FAEHIGKEIT; Fehler) — eine Funktion in einem Graphen der Anwendung nimmt eine Lese-Fähigkeit, die kein Store der Anwendung umsetzt
///   (etwa eine, die ein Katalog deklariert): sie bekäme zur Laufzeit nichts.</item>
/// </list>
/// Die Befunde haben keinen Quell-Ort in dieser Compilation (die Typen liegen in den Domänen-Projekten); jede Meldung beginnt deshalb mit dem
/// einfachen Namen des Typs in Anführungszeichen — daran hängt die Werkbank sie an die Karte.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GesamtbildAnalyzer : DiagnosticAnalyzer
{
    public const string QueryId = "CQRS084", AusgangOffenId = "CQRS085", EingangOffenId = "CQRS086",
        AntwortOhneCommandId = "CQRS108", EmpfaengerId = "CQRS116", OhneGraphId = "CQRS117", VerlaesstId = Uebersetzungsform.Verlassen,
        UebersetzteAntwortId = Uebersetzungsform.Antwort, FaehigkeitOhneStoreId = "CQRS124";

    private static DiagnosticDescriptor D(string id, string titel, string text, DiagnosticSeverity s) =>
        new(id, titel, text, "CQRS.Gesamtbild", s, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Query = D(QueryId, "Query mit mehreren Zielen",
        "'{0}': die Query beantworten {1} Ziele ({2}) — genau eines, ein Reader oder ein Aggregat (R4, GR-QUERY)", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor AusgangOffen = D(AusgangOffenId, "Ausgang ohne Erzeuger",
        "'{0}' ({1}) erzeugt niemand — {2} (GR-MODUL-AUSGANG-OFFEN)", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor EingangOffen = D(EingangOffenId, "Eingang ohne Konsument",
        "'{0}' ({1}) nimmt niemand an — {2} (GR-MODUL-EINGANG-OFFEN)", DiagnosticSeverity.Warning);

    private static readonly DiagnosticDescriptor AntwortOhneCommand = D(AntwortOhneCommandId, "Antwort ohne eigenen Command",
        "Die Funktion '{0}' hört '{1}', aber keiner ihrer Commands kann es entscheiden ({2}) — eine Funktion liest nie das Log, ein Event erreicht sie nur "
        + "als Antwort auf einen eigenen Command; was sie aus einer Entscheidung braucht, sagt ihr das Aggregat als Folge (GR-ANTWORT)", DiagnosticSeverity.Error);

    private static readonly DiagnosticDescriptor UebersetzteAntwort = D(UebersetzteAntwortId, "Antwort, die nie kommt",
        "'{0}': die Antwort-Übersetzung nimmt {1}, aber kein Decide eines Commands, den eine Übersetzung erzeugt, gibt es aus ({2}) — eine "
        + "Antwort-Übersetzung nimmt ein Event, mit dem ein Aggregat den Command einer Übersetzung beantwortet (GR-UEBERSETZUNG-ANTWORT)", DiagnosticSeverity.Error);

    private static readonly DiagnosticDescriptor FaehigkeitOhneStore = D(FaehigkeitOhneStoreId, "Fähigkeit ohne Store",
        "'{0}': der Knoten '{1}' liest über {2}, aber kein Store der Anwendung setzt die Fähigkeit um — deklariert ein Katalog sie, setzt ein Store im "
        + "Speicher der Anwendung sie um (er liest dafür aus seinem eigenen Read-Model) (GR-FAEHIGKEIT)", DiagnosticSeverity.Error);

    private static readonly DiagnosticDescriptor Empfaenger = D(EmpfaengerId, "Command ohne genau einen Empfänger",
        "'{0}' (Command ohne Ziel, von {1}): {2} — ein Command ohne Ziel geht an einen Knoten desselben Graphen, an die Übersetzungen der Domäne, die "
        + "seinen Typ nehmen, oder an den Kopf genau eines Graphen (GR-EMPFAENGER)", DiagnosticSeverity.Error);

    private static readonly DiagnosticDescriptor OhneGraph = D(OhneGraphId, "Funktion in keinem Graphen",
        "'{0}' ist in keinem Graphen ein Knoten — sie läuft nie (ein Graph läuft als Instanz, docs/04 §4.4) (GR-GRAPH-KNOTEN)", DiagnosticSeverity.Warning);

    private static readonly DiagnosticDescriptor Verlaesst = D(VerlaesstId, "Katalog-Command verlässt den Graphen",
        "'{0}': {1} aus dem Katalog {2} verlässt den Graphen (von {3}), und keine Übersetzung nimmt ihn — übersetze ihn in der Domäne "
        + "(record … : IUebersetzung<{1}, …>) oder lass ihn den Kopf eines Graphen nehmen (GR-UEBERSETZUNG)", DiagnosticSeverity.Error);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Query, AusgangOffen, EingangOffen, AntwortOhneCommand, UebersetzteAntwort, FaehigkeitOhneStore, Empfaenger, OhneGraph, Verlaesst);

    /// <summary>Die Rollen-Marker, deren Implementierung eine Assembly zur Domäne macht (wie die Projektlage des Extractors).</summary>
    private static readonly string[] DomaenenRollen =
    {
        "IState", "IDecider`1", "IApplier`1", "ICommand", "IEvent", "IQuery", "IQueryResponse", "IReadModel", "IWriteStore", "IReadStore",
        "IProjektion", "IReader", "IFunktion", "IGraph", "IWertobjekt",
        // Die Übersetzungen der Anwendung (<Wurzel>.Uebersetzungen, ein Projekt nur mit ihnen).
        "IUebersetzung`2",
        // Der Rand (Domain.Akteure): wer was hineingibt und hört, und welcher Client wen verkörpert.
        "IAkteur", "IClient",
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Pruefe);
    }

    private static void Pruefe(CompilationAnalysisContext ctx)
    {
        var c = ctx.Compilation;
        if (!Laufzeitprojekt.Ist(c)) return;   // das Gesamtbild sieht nur die Komposition (alle Rollen-Projekte, docs/07)
        INamedTypeSymbol? G(string n) => c.GetTypeByMetadataName("Abstractions." + n);
        var (iEvent, iTransient, iCommand, iQuery, iResponse) = (G("IEvent"), G("ITransientEvent"), G("ICommand"), G("IQuery"), G("IQueryResponse"));
        var (iDecider, iFunktion, iAkteur, iDarf) = (G("IDecider`1"), G("IFunktion"), G("IAkteur"), G("IDarf`1"));
        var iAggregatCommand = G("IAggregatCommand");
        var iClient = G("IClient");
        if (iEvent == null || iCommand == null || iDecider == null) return;
        var rollen = DomaenenRollen.Select(G).Where(x => x != null).Select(x => x!).ToList();

        // Domänen-Assemblies = referenzierte Assemblies (nicht der Vertrag), die einen Rollen-Marker implementieren.
        var typen = new List<INamedTypeSymbol>();
        foreach (var a in c.SourceModule.ReferencedAssemblySymbols.Where(a => a.Name != "Abstractions"))
        {
            var alle = Alle(a.GlobalNamespace).ToList();
            if (alle.Any(t => rollen.Any(r => Ist(t, r)))) typen.AddRange(alle);
        }
        if (typen.Count == 0) return;

        bool IstT(ITypeSymbol t, INamedTypeSymbol? i) => i != null && Ist(t, i);
        var clientVertraege = typen.Where(t => Clientvertragslesart.IstClient(t, iClient)).Select(t => Clientvertragslesart.Lies(t, c)).ToList();
        bool Generisch(INamedTypeSymbol t, INamedTypeSymbol? g) => g != null && t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, g));
        IEnumerable<IMethodSymbol> Methoden(INamedTypeSymbol t, string? name = null) => t.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && m.Parameters.Length > 0 && (name == null || m.Name == name));
        var erzeugt = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var entschieden = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var genommen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);   // Commands ohne Ziel, die eine Funktion nimmt
        var entscheidet = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);   // Command → was sein Decide ausgeben kann
        var zieleJeQuery = new Dictionary<ITypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        void Ziel(ITypeSymbol query, string ziel)
        {
            if (!zieleJeQuery.TryGetValue(query, out var l)) zieleJeQuery[query] = l = new List<string>();
            l.Add(ziel);
        }

        foreach (var t in typen.Where(t => !t.IsAbstract || t.TypeKind == TypeKind.Interface))
        {
            if (t.TypeKind == TypeKind.Class && Generisch(t, iDecider))
            {
                foreach (var m in Methoden(t, Abstractions.Aggregatvertrag.Decide))
                {
                    entschieden.Add(m.Parameters[0].Type);
                    if (!entscheidet.TryGetValue(m.Parameters[0].Type, out var aus))
                        entscheidet[m.Parameters[0].Type] = aus = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                    aus.UnionWith(Faelle(m.ReturnType));
                    erzeugt.UnionWith(Faelle(m.ReturnType));
                }
                // Die Fragen an das Aggregat (docs/03 §3.2, dieselbe Lesart wie Generator und Extractor): Ziel der Query, Erzeuger der Antworten.
                if (Projektionsform.StateVonDecider(t) is { } state)
                    foreach (var f in Projektionsform.LiesAggregatfragen(t, state).Fragen)
                    {
                        Ziel(f.Query, "Aggregat " + state.Name);
                        erzeugt.UnionWith(f.Antworten);
                    }
            }
            // Ein Reader (Projektionsform, eine Lesart mit Generator und Extractor): die Antworten jeder Frage.
            if (Projektionsform.IstReader(t))
                foreach (var f in Projektionsform.LiesReader(t).Fragen)
                {
                    Ziel(f.Query, "Reader " + t.Name);
                    erzeugt.UnionWith(f.Antworten);
                }
            // Eine Funktion (Klasse oder Schnittstelle): die Fälle jedes Auf, und welche Commands ohne Ziel sie nimmt.
            if ((t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Interface) && Funktionsform.IstFunktion(t))
                foreach (var a in Funktionsform.Lies(t).Aufs)
                {
                    erzeugt.UnionWith(a.Ausgaben);
                    if (a.Command) genommen.UnionWith(a.Eingaenge);
                }
            // Eine Übersetzung der Domäne nimmt TEin und erzeugt TAus.
            if (Uebersetzungsform.IstUebersetzung(t))
            {
                var u = Uebersetzungsform.Lies(t);
                genommen.Add(u.Ein);
                erzeugt.Add(u.Aus);
            }
            // Ein Akteur: was er hineingibt, erzeugt er.
            if (IstT(t, iAkteur))
                erzeugt.UnionWith(t.AllInterfaces.Where(i => iDarf != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).Select(i => i.TypeArguments[0]));
        }
        foreach (var v in clientVertraege) erzeugt.UnionWith(v.Sendet);

        // GR-QUERY und offene Eingänge.
        foreach (var t in typen.Where(t => !t.IsAbstract && t.TypeKind != TypeKind.Interface))
        {
            if (IstT(t, iQuery))
            {
                var r = zieleJeQuery.TryGetValue(t, out var l) ? l : new List<string>();
                if (r.Count > 1) ctx.ReportDiagnostic(Diagnostic.Create(Query, Location.None, t.Name, r.Count, string.Join(", ", r)));
                if (r.Count == 0) ctx.ReportDiagnostic(Diagnostic.Create(EingangOffen, Location.None, t.Name, "Query", "kein Reader und kein Aggregat beantwortet sie"));
            }
            else if (IstT(t, iAggregatCommand) && !entschieden.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(EingangOffen, Location.None, t.Name, "Command", "kein Aggregat entscheidet ihn (Decide)"));
            else if (IstT(t, iCommand) && !IstT(t, iAggregatCommand) && !t.IsGenericType && !genommen.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(EingangOffen, Location.None, t.Name, "Command ohne Ziel", "kein Knoten eines Graphen nimmt ihn"));
        }

        // Offene Ausgänge: Events/Ablehnungen ohne Erzeuger, Responses ohne Reader-Handle.
        var alsFeld = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var r in typen.Where(t => IstT(t, iResponse)))
            foreach (var p in r.GetMembers().OfType<IPropertySymbol>())
                foreach (var x in MitArgumenten(p.Type)) alsFeld.Add(x);
        foreach (var t in typen.Where(t => !t.IsAbstract && t.TypeKind != TypeKind.Interface))
        {
            if (IstT(t, iEvent) && !erzeugt.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(AusgangOffen, Location.None, t.Name, IstT(t, iTransient) ? "Ablehnung/Hinweis" : "Event",
                    "kein Decide, keine Funktion und kein Akteur nennt es in seiner Signatur"));
            else if (IstT(t, iResponse) && !erzeugt.Contains(t) && !alsFeld.Contains(t))
                ctx.ReportDiagnostic(Diagnostic.Create(AusgangOffen, Location.None, t.Name, "Response", "keine Frage eines Readers oder Aggregats nennt sie in ihrer Signatur"));
        }

        // GR-ANTWORT: ein Event-Eingang einer Funktion ist eine Antwort — einer ihrer Commands (aus irgendeinem Auf) muss es entscheiden können.
        foreach (var f in typen.Where(t => (t.TypeKind == TypeKind.Interface || (t.TypeKind == TypeKind.Class && !t.IsAbstract)) && Funktionsform.IstFunktion(t)))
        {
            var form = Funktionsform.Lies(f);
            if (form.Fehler.Count > 0) continue;   // CQRS100 meldet der Generator
            var befehle = form.Aufs.SelectMany(a => a.Ausgaben).Where(x => IstT(x, iAggregatCommand)).Distinct<ITypeSymbol>(SymbolEqualityComparer.Default).ToList();
            foreach (var a in form.Aufs.Where(form.IstAntwort))
                if (!befehle.Any(b => entscheidet.TryGetValue(b, out var aus) && aus.Contains(a.Eingang)))
                    ctx.ReportDiagnostic(Diagnostic.Create(AntwortOhneCommand, a.Methode.Locations.FirstOrDefault() ?? Location.None, f.Name, a.Eingang.Name,
                        befehle.Count == 0 ? "sie gibt keinen Command aus" : "ihre Commands: " + string.Join(", ", befehle.Select(b => b.Name))));
        }

        // Die Übersetzungen der Domänen (docs/04 §4.4): sie nehmen nach Typ, was ein Graph ausgibt, was ein Client gibt, was ein Aggregat
        //   auf den Command einer Übersetzung antwortet.
        var uebersetzungen = typen.Where(Uebersetzungsform.IstUebersetzung).Select(Uebersetzungsform.Lies).Where(u => u.Fehler.Count == 0).ToList();
        string V(ITypeSymbol t) => Funktionsform.Voll(t);
        bool Uebersetzt(ITypeSymbol t) => uebersetzungen.Any(u => V(u.Ein) == V(t));

        // GR-UEBERSETZUNG-ANTWORT: eine Antwort-Übersetzung nimmt ein Event, das das Decide eines Commands ausgeben kann, den eine Übersetzung erzeugt.
        var uebersetzt = uebersetzungen.Select(u => u.Aus).Where(x => IstT(x, iAggregatCommand)).Distinct<ITypeSymbol>(SymbolEqualityComparer.Default).ToList();
        foreach (var u in uebersetzungen.Where(u => u.IstAntwort))
            if (!uebersetzt.Any(b => entscheidet.TryGetValue(b, out var aus) && aus.Contains(u.Ein)))
                ctx.ReportDiagnostic(Diagnostic.Create(UebersetzteAntwort, Location.None, u.Typ.Name, u.Ein.Name,
                    uebersetzt.Count == 0 ? "keine Übersetzung erzeugt einen Command an ein Aggregat" : "die Commands der Übersetzungen: " + string.Join(", ", uebersetzt.Select(b => b.Name))));

        // Was läuft: ein Graph der Anwendung — und ein Graph aus einem Katalog, sobald die Domäne ihn anbindet: eine Übersetzung nimmt einen seiner
        //   Ausgänge oder gibt an seinen Kopf, oder ein Akteur gibt an seinen Kopf (welche Instanzen laufen, sagt erst die Umgebung).
        var graphen = typen.Where(Graphform.IstGraph).Select(Graphform.Lies).Where(g => g.Gueltig).ToList();
        var akteurDarf = new HashSet<string>(typen.Where(t => IstT(t, iAkteur)).SelectMany(a => a.AllInterfaces
            .Where(i => iDarf != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)).Select(i => V(i.TypeArguments[0]))));
        var laufen = graphen.Where(g => !Abstractions.Projektstruktur.IstKatalog(g.Typ.ContainingAssembly?.Name ?? "")
            || g.Ausgaenge.Any(a => Uebersetzt(a.Typ))
            || g.Koepfe.Any(k => akteurDarf.Contains(V(k.Typ)) || uebersetzungen.Any(u => V(u.Aus) == V(k.Typ)))).ToList();

        // GR-FAEHIGKEIT: jede Lese-Fähigkeit eines Knotens in einem laufenden Graphen setzt ein Store der Anwendung um (mehrere meldet CQRS053).
        var iReadStore = G("IReadStore");
        var stores = typen.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract && iReadStore != null && Ist(t, iReadStore)).ToList();
        foreach (var g in laufen)
            foreach (var k in g.Knoten)
                foreach (var f in k.Funktion.Aufs.SelectMany(a => a.Faehigkeiten).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
                    if (!stores.Any(s => s.AllInterfaces.Any(i => V(i) == V(f))))
                        ctx.ReportDiagnostic(Diagnostic.Create(FaehigkeitOhneStore, Location.None, g.Typ.Name, k.Name, f.Name));

        // GR-EMPFAENGER (Entscheidung 2) und GR-UEBERSETZUNG (A3): ein Command ohne Ziel, der seinen Erzeuger verlässt, nimmt eine Übersetzung der
        //   Domäne (nach seinem Typ) oder der Kopf genau eines Graphen. Ohne Empfänger: kommt er aus einem Katalog, fehlt seine Übersetzung (CQRS120).
        string N(ITypeSymbol t) => Funktionsform.Name(t);
        var koepfe = new Dictionary<string, List<string>>();
        void Empfaengt(ITypeSymbol typ, string wer) => (koepfe.TryGetValue(N(typ), out var l) ? l : koepfe[N(typ)] = new List<string>()).Add(wer);
        foreach (var g in laufen)
            foreach (var (typ, _) in g.Koepfe) Empfaengt(typ, g.Typ.Name);
        var verlassen = new List<(ITypeSymbol Typ, string Von, bool AusGraph)>();
        foreach (var g in laufen)
            foreach (var (von, typ) in g.Ausgaenge.Where(a => Nachrichtenform.IstOhneZiel(a.Typ)))
                verlassen.Add((typ, $"{g.Typ.Name}.{von.Name}", true));
        foreach (var u in uebersetzungen.Where(u => Nachrichtenform.IstOhneZiel(u.Aus)))
            verlassen.Add((u.Aus, "Übersetzung " + u.Typ.Name, false));
        foreach (var a in typen.Where(t => IstT(t, iAkteur)))
            foreach (var d in a.AllInterfaces.Where(i => iDarf != null && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDarf)))
                if (Nachrichtenform.IstOhneZiel(d.TypeArguments[0])) verlassen.Add((d.TypeArguments[0], "Akteur " + a.Name, false));
        foreach (var (typ, von, ausGraph) in verlassen.GroupBy(v => N(v.Typ) + "|" + v.Von).Select(x => x.First()))
        {
            if (Uebersetzt(typ)) continue;
            var hier = koepfe.TryGetValue(N(typ), out var l) ? l.Distinct().ToList() : new List<string>();
            if (hier.Count == 1) continue;
            if (hier.Count == 0 && ausGraph && Abstractions.Projektstruktur.IstKatalog(typ.ContainingAssembly?.Name ?? ""))
                ctx.ReportDiagnostic(Diagnostic.Create(Verlaesst, Location.None, von.Split('.')[0], typ.Name, typ.ContainingAssembly!.Name, von));
            else
                ctx.ReportDiagnostic(Diagnostic.Create(Empfaenger, Location.None, typ.Name, von,
                    hier.Count == 0 ? "keine Übersetzung nimmt ihn und kein Graph am Kopf" : "mehrere Graphen nehmen ihn am Kopf (" + string.Join(", ", hier) + ")"));
        }

        // GR-GRAPH-KNOTEN: eine Funktion ohne Graph läuft nie.
        var knoten = new HashSet<string>(graphen.SelectMany(g => g.Knoten).Select(k => N(k.Typ)));
        foreach (var f in typen.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract && Funktionsform.IstFunktion(t) && !knoten.Contains(N(t))))
            ctx.ReportDiagnostic(Diagnostic.Create(OhneGraph, Location.None, f.Name));
    }

    private static bool Ist(ITypeSymbol t, INamedTypeSymbol i) =>
        SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, i) || t.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, i));

    /// <summary>Die Fälle einer Ausgabe-Signatur: <c>OneOf&lt;A, B&gt;</c> → A, B; Task/IEnumerable abgeschält; void → keine.</summary>
    private static IEnumerable<ITypeSymbol> Faelle(ITypeSymbol t)
    {
        while (t is INamedTypeSymbol { TypeArguments.Length: 1 } h
               && (h.Name == "Task" || h.Name == "ValueTask" || h.Name == "IEnumerable" || h.Name == "IAsyncEnumerable"))
            t = h.TypeArguments[0];
        if (t is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } n) t = n.TypeArguments[0];
        if (t is INamedTypeSymbol { Name: "OneOf" } o && o.ContainingNamespace?.ToDisplayString() == "Abstractions") return o.TypeArguments;
        if (t.SpecialType == SpecialType.System_Void || t is INamedTypeSymbol { Name: "Task", TypeArguments.Length: 0 }) return Enumerable.Empty<ITypeSymbol>();
        return new[] { t };
    }

    private static IEnumerable<ITypeSymbol> MitArgumenten(ITypeSymbol t)
    {
        yield return t;
        if (t is IArrayTypeSymbol a) foreach (var x in MitArgumenten(a.ElementType)) yield return x;
        if (t is INamedTypeSymbol n) foreach (var arg in n.TypeArguments) foreach (var x in MitArgumenten(arg)) yield return x;
    }

    private static IEnumerable<INamedTypeSymbol> Alle(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in Alle(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }
}
