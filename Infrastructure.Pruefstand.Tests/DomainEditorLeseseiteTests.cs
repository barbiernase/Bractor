using System.Collections.Immutable;
using DomainEditor;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Die LESESEITE des Domänen-Editors: Modell (Stores/Fähigkeiten, Projektion, Reaktion, Reader, Query/Response/ReadModel)
/// → C#. Beweist die Form UND dass die Ausgabe die Analyzer besteht, die ihr Sicherheitsnetz sind (CQRS050 Ausgabe-Vertrag,
/// CQRS051 eine Fn je Fähigkeit, CQRS054/055 kein Store im Konsumenten-Zustand / kein new Store, CQRS057 Handle-Form).
/// Dazu Board → Modell: die Editor-Defaults für NEUES (Fähigkeits-Name, Parameter, Impl-Klasse).
/// </summary>
public sealed class DomainEditorLeseseiteTests
{
    private static readonly Rahmen TestRahmen = new()
    {
        Verzeichnisse = new Dictionary<string, string> { ["Probe"] = "Probe" },
        ProjektionsSchreiber = nameof(Core.ProjectionWriter), ProjektionsSchreiberNamespace = typeof(Core.ProjectionWriter).Namespace,
    };

    private static EditorModell Modell() => new()
    {
        Rahmen = TestRahmen,
        Records =
        [
            new() { Name = "Gemacht", Kind = RecordArt.Event, Namespace = "Probe", Felder = [new() { Name = "Wert", Typ = "int" }] },
            new() { Name = "MachWeiter", Kind = RecordArt.Command, Namespace = "Probe", Felder = [new() { Name = "AggregateId", Typ = "Guid" }] },
            new() { Name = "HoleZeile", Kind = RecordArt.Query, Namespace = "Probe", Felder = [new() { Name = "Id", Typ = "Guid" }] },
            new() { Name = "ZeileAntwort", Kind = RecordArt.Antwort, Namespace = "Probe", Felder = [new() { Name = "Wert", Typ = "int" }] },
            new() { Name = "KeineZeile", Kind = RecordArt.Antwort, Namespace = "Probe" },
            new() { Name = "Zeile", Kind = RecordArt.ReadModel, Namespace = "Probe", Felder = [new() { Name = "Id", Typ = "Guid" }, new() { Name = "Wert", Typ = "int" }] },
            new() { Name = "PruefKonfig", Kind = RecordArt.Konfig, Namespace = "Probe", Felder = [new() { Name = "Tage", Typ = "int" }] },
            new() { Name = "Pruefe", Kind = RecordArt.Trigger, Namespace = "Probe", Felder = [new() { Name = "Id", Typ = "Guid" }] },
            new() { Name = "Tick", Kind = RecordArt.Selbst, Namespace = "Probe", OhneParameterliste = true },
        ],
        Lesen = new Leseseite
        {
            Stores =
            [
                new()
                {
                    Name = "IBuch", Namespace = "Probe", Doku = "Das Buch.",
                    Fns =
                    [
                        new() { Name = "ISchreibeZeile", Methode = "SchreibeAsync", Parameter = [new() { Typ = "Zeile", Name = "zeile" }] },
                        new() { Name = "IFindeZeile", Methode = "FindeAsync", Lesen = true, Rueckgabe = "Task<Zeile?>", Parameter = [new() { Typ = "Guid", Name = "id" }] },
                    ],
                    Impl = new() { Name = "Buch", Namespace = "Probe" },
                },
            ],
            Konsumenten =
            [
                new()
                {
                    Name = "Zeilenliste", Namespace = "Probe", SubscriberId = "zeilenliste",
                    Handles = [new() { Eingang = "Gemacht", Faehigkeiten = [new() { Typ = "ISchreibeZeile", Name = "schreibe" }] }],
                },
                new()
                {
                    Name = "Weitermacher", Namespace = "Probe", SubscriberId = "weitermacher",
                    Handles = [new() { Eingang = "Gemacht", Ausgaenge = ["MachWeiter"] }],
                },
            ],
            Reader =
            [
                new()
                {
                    Name = "ZeilenLeser", Namespace = "Probe", Projektion = "Zeilenliste", TrackDeps = false,
                    Handles = [new() { Eingang = "HoleZeile", Parameter = "query", Ausgaenge = ["ZeileAntwort", "KeineZeile"], Faehigkeiten = [new() { Typ = "IFindeZeile", Name = "finde" }] }],
                },
            ],
            Pipelines =
            [
                new()
                {
                    Name = "PruefPipeline", Namespace = "Probe", PipelineId = "pruef", Konfigs = ["PruefKonfig"],
                    Handles =
                    [
                        new() { Eingang = "Pruefe", Parameter = "pruefe", Ausgaenge = ["MachWeiter", "Selbst<Tick>"], Faehigkeiten = [new() { Typ = "IFindeZeile", Name = "finde" }],
                            Rumpf = "if (await finde.FindeAsync(pruefe.Id) is null) yield break;\nyield return new MachWeiter(pruefe.Id);" },
                        new() { Eingang = "Tick", Parameter = "tick", Ausgaenge = ["MachWeiter"] },
                    ],
                },
            ],
        },
    };

    private static string Inhalt(IReadOnlyList<GenerierteDatei> dateien, string pfad) =>
        dateien.Single(d => d.Pfad == pfad).Inhalt.Replace("\r\n", "\n").TrimEnd('\n');

    [Fact]
    public void Erzeugt_Schnittstellen_Impl_Konsumenten_Reader_und_Leseseiten_Records()
    {
        var dateien = Scaffolder.Generiere(Modell());
        dateien.Select(d => (d.Pfad, d.Art)).Should().BeEquivalentTo(new[]
        {
            ("Probe/Commands.cs", DateiArt.Typen), ("Probe/Events.cs", DateiArt.Typen), ("Probe/Queries.cs", DateiArt.Typen),
            ("Probe/Responses.cs", DateiArt.Typen), ("Probe/ReadModels.cs", DateiArt.Typen),
            ("Probe/IBuch.cs", DateiArt.Schnittstellen), ("Probe/Buch.cs", DateiArt.StoreImpl),
            ("Probe/Zeilenliste.cs", DateiArt.Konsument), ("Probe/Weitermacher.cs", DateiArt.Konsument), ("Probe/ZeilenLeser.cs", DateiArt.Leser),
            ("Probe/Konfigs.cs", DateiArt.Typen), ("Probe/Triggers.cs", DateiArt.Typen), ("Probe/SelbstNachrichten.cs", DateiArt.Typen),
            ("Probe/PruefPipeline.cs", DateiArt.Pipeline),
        });
    }

    [Fact]
    public void Faehigkeit_je_Funktion_und_Buendel_als_Store()
    {
        Inhalt(Scaffolder.Generiere(Modell()), "Probe/IBuch.cs").Should().Be(
            """
            using Abstractions;

            namespace Probe;

            public interface ISchreibeZeile : IWriteStore { Task SchreibeAsync(Zeile zeile); }

            public interface IFindeZeile : IReadStore { Task<Zeile?> FindeAsync(Guid id); }

            /// <summary>
            /// Das Buch.
            /// </summary>
            public interface IBuch : IStore, ISchreibeZeile, IFindeZeile { }
            """);
    }

    [Fact]
    public void Projektion_nimmt_die_Faehigkeit_als_Parameter_nicht_im_Konstruktor()
    {
        Inhalt(Scaffolder.Generiere(Modell()), "Probe/Zeilenliste.cs").Should().Be(
            """
            using Abstractions;
            using Core;

            namespace Probe;

            public partial class Zeilenliste : ISubscriber, IPullSubscriber
            {
                public string SubscriberId => "zeilenliste";

                public Task Handle(Gemacht evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISchreibeZeile schreibe)
                {
                    throw new NotImplementedException("TODO: Handle(Gemacht)");
                }
            }
            """);
    }

    [Fact]
    public void Reader_und_Reaktion_tragen_den_Ausgabe_Vertrag_in_der_Signatur()
    {
        var d = Scaffolder.Generiere(Modell());
        Inhalt(d, "Probe/ZeilenLeser.cs").Should().Contain("[ProjectionReader(TrackDeps = false)]")
            .And.Contain("public partial class ZeilenLeser : IReader<Zeilenliste>")
            .And.Contain("public Task<OneOf<ZeileAntwort, KeineZeile>> Handle(HoleZeile query, IMessageEnvelope envelope, ReadContext ctx, IFindeZeile finde)");
        Inhalt(d, "Probe/Weitermacher.cs").Should()
            .Contain("public IAsyncEnumerable<MachWeiter> Handle(Gemacht evt, IAggregateEnvelope envelope, ProjectionWriter writer)");
        Inhalt(d, "Probe/ReadModels.cs").Should().Contain("public record Zeile(Guid Id, int Wert) : IReadModel;");
    }

    [Fact]
    public void Neue_Store_Impl_erbt_die_gemeinsame_Basis_aus_dem_Rahmen()
    {
        var m = Modell() with
        {
            Rahmen = TestRahmen with
            {
                StoreBasis = new() { Name = "SpeicherBasis", Namespace = "Probe.Basis", KtorParameter = [new() { Typ = "IDocumentStore", Name = "store" }], Usings = ["Marten"] },
            },
        };
        Inhalt(Scaffolder.Generiere(m), "Probe/Buch.cs").Should().Be(
            """
            using Marten;
            using Probe.Basis;

            namespace Probe;

            public sealed partial class Buch : SpeicherBasis, IBuch
            {
                public Buch(IDocumentStore store) : base(store) { }

                public Task SchreibeAsync(Zeile zeile)
                    => throw new NotImplementedException("TODO: ISchreibeZeile.SchreibeAsync");

                public Task<Zeile?> FindeAsync(Guid id)
                    => throw new NotImplementedException("TODO: IFindeZeile.FindeAsync");
            }
            """);
    }

    [Fact]
    public void Bestehende_Impl_bekommt_nur_Methoden_neuer_Faehigkeiten()
    {
        var m = Modell();
        var st = m.Lesen!.Stores[0];
        m = m with
        {
            Lesen = m.Lesen with
            {
                Stores = [st with
                {
                    Impl = st.Impl! with { Datei = "Probe/Buch.cs", SchreibDatei = "Probe/Buch.cs", LeseDatei = "Probe/Buch.Lesen.cs" },
                    Fns = [st.Fns[0] with { Datei = "Probe/IBuch.cs" }, st.Fns[1]],
                }],
            },
        };
        var impl = Scaffolder.Generiere(m).Where(d => d.Art == DateiArt.StoreImpl).ToList();
        impl.Select(d => d.Pfad).Should().Equal("Probe/Buch.Lesen.cs");
        impl[0].Inhalt.Should().Contain("FindeAsync").And.NotContain("SchreibeAsync");
    }

    [Fact]
    public async Task Scaffolder_Ausgabe_kompiliert_und_besteht_alle_Vertrags_Analyzer()
    {
        _ = typeof(Abstractions.ICommand);
        _ = typeof(Core.ProjectionWriter);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        var trees = Scaffolder.Generiere(Modell()).Select(d => CSharpSyntaxTree.ParseText(d.Inhalt, path: d.Pfad))
            .Append(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Threading.Tasks;"))
            .ToList();
        var comp = CSharpCompilation.Create("LeseseiteProbe", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).Should().BeEmpty();

        var analyzer = typeof(Domain.SourceGeneration.FaehigkeitAnalyzer).Assembly.GetTypes()
            .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!).ToImmutableArray();
        analyzer.Select(a => a.GetType().Name).Should().Contain(["FaehigkeitAnalyzer", "AusgabeVertragAnalyzer", "HandlerFormAnalyzer"]);
        var befunde = await comp.WithAnalyzers(analyzer).GetAnalyzerDiagnosticsAsync();
        befunde.Select(d => d.ToString()).Should().BeEmpty("der Scaffolder schreibt nur, was die Vertrags-Analyzer erlauben");
    }

    // ── Board → Modell: die Editor-Defaults für NEUES ──────────────────────────────────────────

    private const string Board = """
        {
          "rahmen": { "storeImplNamespace": "Probe.Speicher" },
          "stores": [
            { "_id": "st1", "name": "IBuch", "namespace": "Probe", "datei": "Probe/IBuch.cs",
              "impl": { "name": "Buch", "namespace": "Probe.Speicher", "datei": "Probe/Buch.cs" },
              "writeFns": [
                { "_id": "wf1", "name": "SchreibeAsync", "faehigkeit": "ISchreibeZeile", "params": [ { "name": "zeile", "typ": "Zeile" } ],
                  "sig": { "rueckgabe": "Task", "datei": "Probe/IBuch.cs", "parameter": [ { "typ": "Zeile", "name": "zeile" } ] } },
                { "_id": "wf2", "name": "LoescheAsync", "params": [ { "name": "id", "typ": "Guid" } ] }
              ],
              "readFns": [ { "_id": "rf1", "name": "FindeAsync", "params": [ { "name": "id", "typ": "Guid" } ], "rueckgabe": "Zeile?" } ] },
            { "_id": "st2", "name": "IArchiv", "namespace": "Probe",
              "writeFns": [ { "_id": "wf3", "name": "LoescheAsync", "params": [] } ], "readFns": [] }
          ],
          "projektionen": [
            { "_id": "pj1", "name": "Zeilenliste", "namespace": "Probe", "subscriberId": "zeilenliste", "pull": true,
              "code": { "datei": "Probe/Zeilenliste.cs", "zusatz": "public string SubscriberId => \"zeilenliste\";" },
              "handles": [ { "event": "Gemacht", "fns": [ "wf1", "wf2" ],
                "sig": { "parameter": "e", "kontext": [ "env", "w" ], "rueckgabe": "Task", "modifikatoren": "public async",
                         "faehigkeitParameter": [ { "typ": "ISchreibeZeile", "name": "schreib" } ], "rumpfCode": "await Task.CompletedTask;", "datei": "Probe/Zeilenliste.cs" } } ] }
          ],
          "reader": [ { "_id": "rd1", "name": "ZeilenLeser", "namespace": "Probe", "projektion": "Zeilenliste",
                        "handles": [ { "query": "HoleZeile", "fns": [ "rf1" ], "responses": [ "ZeileAntwort" ] } ] },
                      { "_id": "rd2", "name": "Waise", "namespace": "Probe", "handles": [] } ],
          "readModels": [ { "name": "Zeile", "namespace": "Probe", "typart": "public record", "felder": [ { "name": "Id", "typ": "Guid" } ] } ]
        }
        """;

    [Fact]
    public void Board_neue_Fn_bekommt_abgeleitete_Faehigkeit_und_der_Handle_den_Parameter()
    {
        var lesen = BoardLeseseite.AusBoard(Board).Lesen!;
        var buch = lesen.Stores.Single(s => s.Name == "IBuch");
        buch.Fns.Select(f => (f.Name, f.Methode, f.Lesen, f.Rueckgabe)).Should().Equal(
            ("ISchreibeZeile", "SchreibeAsync", false, "Task"), ("ILoesche", "LoescheAsync", false, "Task"), ("IFinde", "FindeAsync", true, "Task<Zeile?>"));
        // Kollision (ILoesche schon vergeben) ⇒ Store-Name ohne I-Präfix angehängt.
        lesen.Stores.Single(s => s.Name == "IArchiv").Fns.Single().Name.Should().Be("ILoescheArchiv");

        var h = lesen.Konsumenten.Single().Handles.Single();
        h.Faehigkeiten.Select(f => (f.Typ, f.Name)).Should().Equal(("ISchreibeZeile", "schreib"), ("ILoesche", "loesche"));
        // Aus dem Code gelesen ⇒ unverändert (nur die Fähigkeits-Parameter folgen den Kanten).
        h.Parameter.Should().Be("e");
        h.Kontext.Should().Equal("env", "w");
        h.Modifikatoren.Should().Be("public async");
        h.Rumpf.Should().Be("await Task.CompletedTask;");
    }

    [Fact]
    public void Board_neuer_Store_bekommt_Impl_im_Namespace_der_Impls_bestehender_ohne_Impl_nichts()
    {
        var lesen = BoardLeseseite.AusBoard(Board).Lesen!;
        lesen.Stores.Single(s => s.Name == "IArchiv").Impl.Should().BeEquivalentTo(new StoreImpl { Name = "Archiv", Namespace = "Probe.Speicher" });
        lesen.Stores.Single(s => s.Name == "IBuch").Impl!.Datei.Should().Be("Probe/Buch.cs");
        lesen.Reader.Select(r => r.Name).Should().Equal("ZeilenLeser");   // ohne Projektion kein IReader<T>
        lesen.Reader[0].Handles.Single().Faehigkeiten.Single().Should().BeEquivalentTo(new Parameter { Typ = "IFinde", Name = "finde" });
        BoardLeseseite.AusBoard(Board).Records.Single(r => r.Kind == RecordArt.ReadModel).Typart.Should().BeNull();
    }

    [Fact]
    public void Pipeline_OneOf_auch_bei_einem_Ausgang_Konfig_Konstruktor_und_Entwurf_als_Rumpf()
    {
        var d = Scaffolder.Generiere(Modell());
        Inhalt(d, "Probe/PruefPipeline.cs").Should().Be(
            """
            using Abstractions;

            namespace Probe;

            public partial class PruefPipeline : IPipelineHandler
            {
                public string PipelineId => "pruef";

                private readonly PruefKonfig _pruefKonfig;

                public PruefPipeline(PruefKonfig pruefKonfig)
                {
                    _pruefKonfig = pruefKonfig;
                }

                public async IAsyncEnumerable<OneOf<MachWeiter, Selbst<Tick>>> Handle(Pruefe pruefe, PipelineContext ctx, IFindeZeile finde)
                {
                    if (await finde.FindeAsync(pruefe.Id) is null) yield break;
                    yield return new MachWeiter(pruefe.Id);
                }

                public IAsyncEnumerable<OneOf<MachWeiter>> Handle(Tick tick, PipelineContext ctx)
                {
                    throw new NotImplementedException("TODO: Handle(Tick)");
                }
            }
            """);
        Inhalt(d, "Probe/Triggers.cs").Should().Contain("public record Pruefe(Guid Id) : IPipelineTrigger;");
        Inhalt(d, "Probe/SelbstNachrichten.cs").Should().Contain("public record Tick : IPipelineSelfMessage { }");
        Inhalt(d, "Probe/Konfigs.cs").Should().Contain("public record PruefKonfig(int Tage);");
    }

    [Fact]
    public void Pipeline_Handle_ohne_Ausgang_ist_Task_und_bekommt_er_einen_wird_er_Strom()
    {
        var m = Modell();
        var p = m.Lesen!.Pipelines[0];
        m = m with { Lesen = m.Lesen with { Pipelines = [p with { Handles = [.. p.Handles, new() { Eingang = "Gemacht", Parameter = "e" }] }] } };
        Inhalt(Scaffolder.Generiere(m), "Probe/PruefPipeline.cs").Should().Contain("public Task Handle(Gemacht e, PipelineContext ctx)");

        var board = """
            { "pipelines": [ { "_id": "pl1", "name": "P", "namespace": "Probe", "handles": [
                { "inputKind": "event", "event": "Gemacht", "sends": [ "MachWeiter" ], "emits": [], "schedules": [], "fns": [],
                  "sig": { "parameter": "e", "rueckgabe": "Task", "ausgangsTypen": [], "datei": "Probe/P.cs", "herkunft": "x" } },
                { "inputKind": "event", "event": "Anders", "sends": [], "emits": [], "schedules": [], "fns": [],
                  "sig": { "parameter": "e", "rueckgabe": "IAsyncEnumerable<OneOf<MachWeiter>>", "ausgangsTypen": [ "MachWeiter" ], "datei": "Probe/P.cs", "herkunft": "x" } } ],
              "code": { "datei": "Probe/P.cs", "basen": [ "IPipelineHandler" ] } } ] }
            """;
        var hs = BoardLeseseite.AusBoard(board).Lesen!.Pipelines.Single().Handles;
        hs[0].Rueckgabe.Should().Be("IAsyncEnumerable<OneOf<MachWeiter>>");   // Task + Ausgang ⇒ Strom, nicht Task<…>
        hs[1].Rueckgabe.Should().Be("Task");                                   // alle Ausgänge gelöst ⇒ Task
    }

    [Fact]
    public void Neue_Store_Fn_mit_Entwurf_bekommt_ihn_als_Rumpf()
    {
        var m = Modell();
        var st = m.Lesen!.Stores[0];
        m = m with { Lesen = m.Lesen with { Stores = [st with { Fns = [st.Fns[0] with { ImplRumpf = "await Task.CompletedTask;" }, st.Fns[1]] }] } };
        Inhalt(Scaffolder.Generiere(m), "Probe/Buch.cs").Should()
            .Contain("public async Task SchreibeAsync(Zeile zeile)\n    {\n        await Task.CompletedTask;\n    }");
    }

    [Fact]
    public void Stempel_unveraendert_leer_geaendert_benannt_Stub_Argumente_zaehlen_nicht()
    {
        var m = Herkunft.Stempeln(Modell() with
        {
            Sagas = [new() { Name = "P", Namespace = "Probe", TriggerEvent = "Gemacht", Schritte = [new() { Wenn = ["Gemacht"], Sende = "MachWeiter", SendeAusdruck = "e => new MachWeiter(e.Id)" }] }],
        });
        Herkunft.Geaenderte(m).Should().BeEmpty();
        // Der Browser ergänzt Stub-Argumente — gegenüber dem verbatim gelesenen Ausdruck wirkungslos ⇒ keine Änderung.
        var mitStub = m with { Sagas = [m.Sagas[0] with { Schritte = [m.Sagas[0].Schritte[0] with { SendeArgumente = ["default"], SendeJeCollection = "" }] }] };
        Herkunft.Geaenderte(mitStub).Should().BeEmpty();
        var geaendert = m with { Records = m.Records.Select(r => r.Name == "Gemacht" ? r with { Felder = [.. r.Felder, new() { Name = "Neu", Typ = "int" }] } : r).ToList() };
        Herkunft.Geaenderte(geaendert).Should().Equal("event Probe.Gemacht");
    }

    private const string BoardBestehend = """
        {
          "records": [ { "name": "Pruefe", "kind": "trigger", "namespace": "Probe", "felder": [] } ],
          "stores": [ { "_id": "st1", "name": "IBuch", "namespace": "Probe", "datei": "Probe/IBuch.cs",
            "readFns": [ { "_id": "rf1", "name": "FindeAsync", "faehigkeit": "IFindeZeile", "rueckgabe": "Zeile?",
                           "params": [ { "name": "id", "typ": "Guid" }, { "name": "mitArchiv", "typ": "bool" } ],
                           "sig": { "rueckgabe": "Task<Zeile?>", "datei": "Probe/IBuch.cs", "herkunft": "x", "parameter": [ { "typ": "System.Guid", "name": "id" } ] } } ] } ],
          "projektionen": [ { "_id": "pj1", "name": "Zeilenliste", "namespace": "Probe", "pull": true, "append": true,
              "code": { "datei": "Probe/Zeilenliste.cs", "basen": [ "ISubscriber", "IPullSubscriber" ], "herkunft": "x" }, "handles": [] } ],
          "reader": [ { "_id": "rd1", "name": "ZeilenLeser", "namespace": "Probe", "projektion": "Andere", "trackDeps": false,
              "code": { "datei": "Probe/ZeilenLeser.cs", "basen": [ "IReader<Zeilenliste>" ], "herkunft": "x" },
              "handles": [ { "query": "HoleZeile", "fns": [], "responses": [ "ZeileAntwort", "KeineZeile" ],
                "sig": { "parameter": "q", "rueckgabe": "Task<ZeileAntwort>", "ausgangsTypen": [ "ZeileAntwort" ], "datei": "Probe/ZeilenLeser.cs", "herkunft": "x" } } ] } ],
          "pipelines": [ { "_id": "pl1", "name": "P", "namespace": "Probe", "pipelineId": "p", "handles": [
              { "inputKind": "trigger", "trigId": "tg1", "sends": [ "MachWeiter" ], "emits": [], "schedules": [ { "name": "NeuTick" } ], "fns": [] } ] } ],
          "triggers": [ { "_id": "tg1", "name": "Pruefe", "msgName": "Pruefe", "namespace": "Probe", "felder": [ { "name": "Id", "typ": "Guid" } ],
                          "modus": "webhook", "route": "/webhook/pruefe" } ]
        }
        """;

    [Fact]
    public void Board_Aenderungen_an_Bestehendem_werden_Modell_Signaturen()
    {
        var m = BoardLeseseite.AusBoard(BoardBestehend);
        var l = m.Lesen!;
        // Reader: neue Response ⇒ nur das Typ-Argument der Rückgabe wechselt; Projektion ⇒ IReader<P>.
        var rh = l.Reader.Single().Handles.Single();
        rh.Ausgaenge.Should().Equal("ZeileAntwort", "KeineZeile");
        rh.Rueckgabe.Should().Be("Task<OneOf<ZeileAntwort, KeineZeile>>");
        l.Reader.Single().Basen.Should().Equal("IReader<Andere>");
        // Flags ⇒ Marker in der Basisliste.
        l.Konsumenten.Single().Basen.Should().Equal("ISubscriber", "IPullSubscriber", "IAppendProjektion");
        // Store-Fn: Parameter geändert ⇒ Board; unveränderter Parameter bleibt wörtlich (auch qualifiziert).
        l.Stores.Single().Fns.Single().Parameter.Select(p => (p.Typ, p.Name)).Should().Equal(("System.Guid", "id"), ("bool", "mitArchiv"));
        // Neue Pipeline: Trigger-Eingang über trigId, Self-Tick ⇒ Selbst<T> + neuer Selbst-Record; Webhook ⇒ neue Ingress-Bindung.
        l.Pipelines.Single().Handles.Single().Should().Match<Handle>(h => h.Eingang == "Pruefe" && h.Ausgaenge.SequenceEqual(new[] { "MachWeiter", "Selbst<NeuTick>" }));
        m.Records.Should().Contain(r => r.Kind == RecordArt.Selbst && r.Name == "NeuTick" && r.Namespace == "Probe");
        m.Records.Single(r => r.Kind == RecordArt.Trigger).Felder.Single().Name.Should().Be("Id");
        m.Ingress.Should().ContainSingle(b => b.Trigger == "Pruefe" && b.Modus == "webhook" && b.Ort == "/webhook/pruefe" && b.Datei == null);
    }

    [Fact]
    public void Validator_meldet_Reader_ohne_Response_und_doppelte_Faehigkeit()
    {
        var m = Modell();
        var st = m.Lesen!.Stores[0];
        m = m with
        {
            Lesen = m.Lesen with
            {
                Stores = [st with { Fns = [st.Fns[0], st.Fns[1] with { Name = st.Fns[0].Name }] }],
                Reader = [m.Lesen.Reader[0] with { Handles = [new() { Eingang = "HoleZeile" }] }],
            },
        };
        Validator.Prüfe(m).Select(b => b.Code).Should().Contain(["EDIT-FAEHIGKEIT-DUP", "EDIT-READER-OHNE-ANTWORT"]);
        Validator.Prüfe(Modell()).Where(b => b.Code.StartsWith("EDIT-FAEHIGKEIT") || b.Code.StartsWith("EDIT-READER")).Should().BeEmpty();
    }

    [Fact]
    public void Leseseite_ist_JSON_roundtrip_stabil()
    {
        var m = Modell();
        EditorModell.AusJson(m.AlsJson()).AlsJson().Should().Be(m.AlsJson());
        Scaffolder.Generiere(EditorModell.AusJson(m.AlsJson())).Select(d => d.Inhalt)
            .Should().Equal(Scaffolder.Generiere(m).Select(d => d.Inhalt));
    }
}
