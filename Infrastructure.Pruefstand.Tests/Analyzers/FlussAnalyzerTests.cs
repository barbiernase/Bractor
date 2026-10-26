using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Domain.SourceGeneration;
using DomainEditor;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Domain.Pipeline.ImagePair;
using Record = DomainEditor.Record;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// CQRS073–078 — die Form einer Pipeline als Fluss im Build (Werkbank P7.4, „Prüfen = Bauen“; früher Editor-Validator bzw. erst der
/// <c>PipelineBauer</c> beim Start): eine Quelle, Warten mit Zeitlimit, ⏳-Port nur mit Zeitlimit, Fall nur aus den Ausgängen, ∧ auf einen
/// Weg, der nicht immer liefert (Warnung), Command ohne Decider.
/// </summary>
public class FlussAnalyzerTests
{
    private static Task<Diagnostic[]> Befunde(params string[] quellen) => Befunde(false, quellen);

    /// <param name="wieImBuild">Die Domänen-Generatoren laufen vorher (der Scaffolder schreibt z. B. den State ohne Version/Id — die generiert der Build).</param>
    private static async Task<Diagnostic[]> Befunde(bool wieImBuild, params string[] quellen)
    {
        _ = typeof(Abstractions.IPipeline);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToImmutableArray();
        Compilation comp = CSharpCompilation.Create("FlussProbe", quellen.Select(q => CSharpSyntaxTree.ParseText(q)), refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (wieImBuild)
            CSharpGeneratorDriver.Create(new StatePropertyGenerator()).RunGeneratorsAndUpdateCompilation(comp, out comp, out _);
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return (await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FlussAnalyzer())).GetAnalyzerDiagnosticsAsync()).ToArray();
    }

    private static async Task<string[]> Ids(string fluss) => (await Befunde(Vorspann + fluss + "\n}")).Select(d => d.Id).ToArray();

    private const string Vorspann = @"
using System; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public sealed record DateiDa(string Pfad) : IQuellNachricht { public string Kennung => Pfad; }
public sealed record Deute(string Pfad) : IAuftrag<IDeuten>;
public record Gedeutet(Guid Id) : IEvent;
public record Unbekannt(string Pfad) : IEvent;
public interface IDeuten : IFunktion { Task<OneOf<Gedeutet, Unbekannt>> RufeAsync(Deute a, IAusfuehrung x); }
public record Melde(Id<Akte> AggregateId) : ICommand<Akte>;
public record Gemeldet() : IEvent;
public record SchonGemeldet() : ITransientEvent;
public record NieGesendet(Id<Akte> AggregateId) : ICommand<Akte>;
public record Begonnen(Guid Id) : IEvent;
public record Fertig() : IEvent;
public partial class Akte : IState
{
    public int Version { get; set; } public Guid Id { get; set; }
    public class Decider : IDecider<Akte> { public OneOf<Gemeldet, SchonGemeldet> Decide(Melde c) => default!; }
    public class Applier : IApplier<Akte> { public void Apply(Gemeldet e) { } }
}
";

    private static string Fluss(string rumpf) => "public sealed class F : IPipeline { public PipelineFluss Fluss => PipelineFluss.Definiere(p => {\n" + rumpf + "\n}); }";

    [Fact]
    public async Task Ein_gueltiger_Fluss_ist_still()
        => (await Ids(Fluss(@"
            var datei = p.Quelle<DateiDa>();
            var deuten = datei.Rufe<IDeuten>(d => new Deute(d.Pfad)).Zeitlimit(TimeSpan.FromMinutes(1));
            var melden = deuten.Bei<Gedeutet>().Sende<Melde>(g => new Melde(new(g.Id)));
            var nein = deuten.Bei<Unbekannt>().Sende<Melde>(u => new Melde(new(Guid.Empty)));
            var zeit = deuten.BeiZeitlimit().Sende<Melde>(z => new Melde(new(Guid.Empty)));
            var doppelt = melden.BeiAbgelehnt().Sende<Melde>(a => new Melde(new(Guid.Empty)));"))).Should().BeEmpty();

    [Fact]
    public async Task Keine_oder_zwei_Quellen_sind_ein_Fehler()
    {
        (await Ids(Fluss("var x = p.Quelle<DateiDa>(); var y = p.Auf<Begonnen>();"))).Should().Equal(FlussAnalyzer.QuelleId);
        (await Ids(Fluss("var q = p.Alle<Begonnen, Fertig>(null!, null!);"))).Should().Equal(FlussAnalyzer.QuelleId);
    }

    [Fact]
    public async Task Warten_ohne_Zeitlimit_ist_ein_Fehler()
    {
        (await Ids(Fluss("var b = p.Auf<Begonnen>(); var ende = b.Strom().Warte<Fertig>();"))).Should().Equal(FlussAnalyzer.WarteId);
        (await Ids(Fluss("var b = p.Auf<Begonnen>(); var ende = b.Strom().Warte<Fertig>().Zeitlimit(TimeSpan.FromHours(1));"))).Should().BeEmpty();
    }

    [Fact]
    public async Task Zeitlimit_Port_ohne_Zeitlimit_ist_ein_Fehler()
        => (await Ids(Fluss(@"
            var datei = p.Quelle<DateiDa>();
            var deuten = datei.Rufe<IDeuten>(d => new Deute(d.Pfad));
            var zeit = deuten.BeiZeitlimit().Sende<Melde>(z => new Melde(new(Guid.Empty)));"))).Should().Equal(FlussAnalyzer.ZeitlimitId);

    [Fact]
    public async Task Ein_Fall_ausserhalb_der_Ausgaenge_ist_ein_Fehler()
    {
        var befunde = await Befunde(Vorspann + Fluss(@"
            var datei = p.Quelle<DateiDa>();
            var deuten = datei.Rufe<IDeuten>(d => new Deute(d.Pfad));
            var melden = deuten.Bei<Gedeutet>().Sende<Melde>(g => new Melde(new(g.Id)));
            var falsch = deuten.Bei<Fertig>().Sende<Melde>(f => new Melde(new(Guid.Empty)));
            var ablehnung = melden.Bei<SchonGemeldet>().Sende<Melde>(a => new Melde(new(Guid.Empty)));") + "\n}");
        befunde.Select(d => d.Id).Should().Equal(FlussAnalyzer.DrahtId, FlussAnalyzer.DrahtId);
        befunde[0].GetMessage().Should().Contain("Fertig ist kein Ausgang von 'deuten' (Ausgänge: Gedeutet, Unbekannt)");
        befunde[1].GetMessage().Should().Contain("eine Ablehnung kommt am ✕-Port: melden.BeiAbgelehnt()");
    }

    [Fact]
    public async Task Ein_Warten_hat_keinen_Abgelehnt_Port()
        => (await Ids(Fluss(@"
            var b = p.Auf<Begonnen>();
            var ende = b.Strom().Warte<Fertig>().Zeitlimit(TimeSpan.FromHours(1));
            var x = ende.BeiAbgelehnt().Sende<Melde>(a => new Melde(new(Guid.Empty)));"))).Should().Equal(FlussAnalyzer.DrahtId);

    [Fact]
    public async Task Ein_Command_ohne_Decider_ist_ein_Fehler()
        => (await Ids(Fluss("var b = p.Auf<Begonnen>(); var x = b.Sende<NieGesendet>(e => new NieGesendet(new(e.Id)));"))).Should().Equal(FlussAnalyzer.OhneDeciderId);

    [Fact]
    public async Task Und_auf_einen_Weg_der_nicht_immer_liefert_ist_eine_Warnung_je_Fall_einmal()
    {
        var befunde = await Befunde(Vorspann + Fluss(@"
            var datei = p.Quelle<DateiDa>();
            var deuten = datei.Rufe<IDeuten>(d => new Deute(d.Pfad));
            var a = p.Alle(deuten.Bei<Gedeutet>(), datei).Sende<Melde>((g, d) => new Melde(new(g.Id)));
            var b = p.Alle(deuten.Bei<Gedeutet>(), datei).Sende<Melde>((g, d) => new Melde(new(g.Id)));") + "\n}");
        befunde.Should().ContainSingle().Which.Id.Should().Be(FlussAnalyzer.WartetId);
        befunde[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        befunde[0].GetMessage().Should().Contain("'deuten.Gedeutet'").And.Contain("Unbekannt").And.Contain("für immer");
    }

    // ── Gezeichnet → Code → Build: der Scaffolder schreibt, was der Editor zeichnet; der Analyzer meldet es am geschriebenen Code. ──

    private static EditorModell Bildeingang(bool unbekanntVerdrahtet)
    {
        const string ns = "Kamera.Eingang";
        Record Rec(string name, string kind, params Feld[] felder) => new() { Name = name, Kind = kind, Namespace = ns, Felder = felder };
        Feld F(string name, string typ) => new() { Name = name, Typ = typ };
        FlussDraht D(string von, string? fall = null) => new() { Von = von, Fall = fall, Port = FlussPort.Fall };
        FlussEingang E(params FlussDraht[] d) => new() { Draehte = d };
        var knoten = new List<FlussSchritt>
        {
            new() { Name = "datei", Art = FlussArt.Quelle, Typ = "DateiDa" },
            new() { Name = "deuten", Art = FlussArt.Funktion, Typ = "INameDeuten", Eingaenge = [E(D("datei")) with { Argumente = ["datei.Name"] }] },
            new() { Name = "melden", Art = FlussArt.Command, Typ = "MeldeBild",
                    Eingaenge = [E(D("deuten", "Bilddatei"), D("datei")) with { Argumente = ["new(deuten.PaarId)"] }] },
        };
        if (unbekanntVerdrahtet)
            knoten.Add(new() { Name = "fehler", Art = FlussArt.Command, Typ = "MeldeBild", Eingaenge = [E(D("deuten", "Unbekannt")) with { Argumente = ["default"] }] });
        return new EditorModell
        {
            Rahmen = new Rahmen { Verzeichnisse = new Dictionary<string, string> { [ns] = "Kamera/Eingang" } },
            Records =
            [
                Rec("DateiDa", RecordArt.Event, F("Name", "string")) with { Basen = [Grammatik.QuellBasis], Zusatz = "public string Kennung => Name;" },
                Rec("DeuteName", RecordArt.Auftrag, F("Name", "string")) with { Funktion = "INameDeuten" },
                Rec("Bilddatei", RecordArt.Event, F("PaarId", "Guid")), Rec("Unbekannt", RecordArt.Event, F("Name", "string")),
                Rec("MeldeBild", RecordArt.Command, F("AggregateId", "Guid")), Rec("BildGemeldet", RecordArt.Event),
            ],
            Aggregate = [new() { Name = "Bildakte", Namespace = ns }],
            Decider = [new() { Aggregat = "Bildakte", Command = "MeldeBild", Ergibt = [new() { Event = "BildGemeldet" }] }],
            Applier = [new() { Aggregat = "Bildakte", Event = "BildGemeldet" }],
            Funktionen = [new() { Name = "INameDeuten", Namespace = ns, Auftrag = "DeuteName", Ergebnisse = ["Bilddatei", "Unbekannt"] }],
            Fluesse = [new() { Name = "Bildeingang", Namespace = ns, Knoten = knoten }],
        };
    }

    [Fact]
    public async Task Der_gezeichnete_Fluss_wird_geschrieben_und_der_Build_meldet_den_offenen_Fall()
    {
        // Die globalen usings des Domänen-Projekts (ImplicitUsings) — der Scaffolder schreibt sie nicht in jede Datei.
        const string global = "global using System; global using System.Collections.Generic; global using System.Linq; global using System.Threading.Tasks;";
        var befunde = await Befunde(true, [global, .. Scaffolder.Generiere(Bildeingang(unbekanntVerdrahtet: false)).Select(d => d.Inhalt)]);
        befunde.Should().ContainSingle(d => d.Id == FlussAnalyzer.WartetId).Which.GetMessage().Should().Contain("'deuten.Bilddatei'").And.Contain("Unbekannt");

        (await Befunde(true, [global, .. Scaffolder.Generiere(Bildeingang(unbekanntVerdrahtet: true)).Select(d => d.Inhalt)]))
            .Should().BeEmpty("der andere Fall ist verdrahtet");
    }
}
