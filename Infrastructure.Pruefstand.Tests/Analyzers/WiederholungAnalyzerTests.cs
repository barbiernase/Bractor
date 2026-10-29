using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Domain.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// Die Regeln der Wiederholbarkeit (docs/10 §10.6): R2 (CQRS101, ein Auf/Handle je Eingang), R11 (CQRS102, Aggregat und
/// Projektion rechnen gleich), R12 (CQRS103, gleichbleibende Funktion ohne Uhr und Zufall), R13 (CQRS104, neue Ids nur abgeleitet).
/// </summary>
[Trait("Kategorie", "Garantie")]
public class WiederholungAnalyzerTests
{
    private const string Vorspann = """
        using System; using System.Collections.Generic; using System.Threading.Tasks; using Abstractions; using Core;
        namespace Probe {
        public sealed record Ueberfaellig(int Tage) : IEvent;
        public sealed record Bezahlt(decimal Betrag) : IEvent;
        public partial class Konto : IState
        {
            public int Version { get; set; } public Guid Id { get; set; } public DateTimeOffset Seit { get; set; }
        }
        public sealed record Mahne(Id<Konto> AggregateId, DateTimeOffset Am) : ICommand<Konto>;
        public sealed record Gemahnt(DateTimeOffset Am) : IEvent;
        public interface IPost : IDienst { Task<string> SendeAsync(); }
        public interface ISchreibe : IWriteStore { Task SetzeAsync(Guid id); }
        """;

    private static async Task<(string Id, string Text)[]> Befunde(string code)
    {
        _ = typeof(IState);
        _ = typeof(Core.MartenStore);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("Probe.Pipeline", new[] { CSharpSyntaxTree.ParseText(Vorspann + code + "\n}") }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GleichbleibendAnalyzer(), new EinEingangAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => (d.Id, d.GetMessage())).ToArray();
    }

    [Fact]
    public async Task R2_zwei_Auf_oder_Handle_fuer_denselben_Eingang_sind_CQRS101()
    {
        (await Befunde("""
            public sealed class Doppelt : IFunktion
            {
                public Mahne? Auf(Ueberfaellig e) => null;
                public Mahne? Auf(Ueberfaellig e, Herkunft h) => null;
                public Mahne? Auf(Bezahlt e) => null;
            }
            """)).Should().ContainSingle(b => b.Id == EinEingangAnalyzer.Id).Which.Text.Should().Contain("Ueberfaellig");
        (await Befunde("""
            public sealed class Liste : IProjektion
            {
                public Task Auf(Ueberfaellig e, Herkunft h, ISchreibe s) => Task.CompletedTask;
                public Task Auf(Ueberfaellig e, Herkunft h) => Task.CompletedTask;
            }
            """)).Select(b => b.Id).Should().Contain(EinEingangAnalyzer.Id);
    }

    [Fact]
    public async Task R11_ein_Aggregat_oder_eine_Projektion_mit_Uhr_Zufall_oder_Datei_ist_CQRS102()
    {
        (await Befunde("""
            public partial class Konto
            {
                public partial class Decider : IDecider<Konto>
                {
                    public IEnumerable<OneOf<Gemahnt>> Decide(Mahne c) { yield return new Gemahnt(DateTimeOffset.UtcNow); }
                }
            }
            """)).Should().ContainSingle(b => b.Id == GleichbleibendAnalyzer.GleichId).Which.Text.Should().Contain("die Uhr");
        (await Befunde("""
            public sealed class Liste : IProjektion
            {
                public Task Auf(Ueberfaellig e, Herkunft h, ISchreibe s)
                    => new Random().Next() > 0 ? s.SetzeAsync(Guid.Empty) : System.IO.File.ReadAllTextAsync("x");
            }
            """)).Where(b => b.Id == GleichbleibendAnalyzer.GleichId).Select(b => b.Text).Should()
            .Contain(t => t.Contains("Zufall")).And.Contain(t => t.Contains("fremde Daten"));
        (await Befunde("""
            public partial class Konto
            {
                public partial class Decider : IDecider<Konto>
                {
                    public IEnumerable<OneOf<Gemahnt>> Decide(Mahne c) { yield return new Gemahnt(c.Am); }
                }
            }
            """)).Should().BeEmpty("die Zeit aus dem Command ist erlaubt");
    }

    [Fact]
    public async Task R12_eine_gleichbleibende_Funktion_mit_Uhr_ist_CQRS103_mit_Dienst_haelt_das_Framework_fest()
    {
        (await Befunde("""
            public sealed class Mahner : IFunktion
            {
                public Mahne Auf(Ueberfaellig e, Herkunft h) => new(h.NeueId<Konto>(), DateTimeOffset.Now);
            }
            """)).Should().ContainSingle(b => b.Id == GleichbleibendAnalyzer.GleichbleibendId);
        (await Befunde("""
            public sealed class Mahner(IPost post) : IFunktion
            {
                public Mahne Auf(Ueberfaellig e, Herkunft h) => new(h.NeueId<Konto>(), DateTimeOffset.Now);
            }
            """)).Should().BeEmpty("mit Dienst hält das Framework die Ausgabe fest — die Uhr ist dann erlaubt");
        (await Befunde("""
            public sealed class Mahner : IFunktion
            {
                public Mahne Auf(Ueberfaellig e, Herkunft h) => new(h.NeueId<Konto>(), h.Zeit);
            }
            """)).Should().BeEmpty("die Zeit aus der Herkunft ist gleichbleibend");
    }

    [Fact]
    public async Task R13_eine_gewuerfelte_Id_in_Funktion_Aggregat_oder_Reader_ist_CQRS104_beim_Client_nicht()
    {
        (await Befunde("""
            public sealed class Mahner(IPost post) : IFunktion
            {
                public Mahne Auf(Ueberfaellig e) => new(Id<Konto>.Neu(), DateTimeOffset.UnixEpoch);
            }
            """)).Should().ContainSingle(b => b.Id == GleichbleibendAnalyzer.NeueIdId).Which.Text.Should().Contain("Herkunft.NeueId");
        (await Befunde("""
            public partial class Konto
            {
                public partial class Decider : IDecider<Konto>
                {
                    public IEnumerable<OneOf<Gemahnt>> Decide(Mahne c) { var _ = Guid.NewGuid(); yield return new Gemahnt(c.Am); }
                }
            }
            """)).Should().ContainSingle(b => b.Id == GleichbleibendAnalyzer.NeueIdId);
        (await Befunde("""
            public sealed record Kunde : IMensch, IDarf<Mahne>;
            public static class Oberflaeche { public static Mahne Neu() => new(Id<Konto>.Neu(), DateTimeOffset.UnixEpoch); }
            """)).Should().BeEmpty("ein Client würfelt neue Ids (er hält die Command-Id fest)");
    }
}
