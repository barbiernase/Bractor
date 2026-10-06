using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Projections.SourceGeneration;

namespace Infrastructure.Pruefstand.Analyzers;

/// <summary>
/// Ein Co-Commit-Store schreibt nur über die Puffer-Methoden seiner Basis. CQRS066 = von der Datenzugriffs-Bibliothek nur
/// der Lese-Einstieg (QuerySession → LoadAsync/LoadManyAsync/Query + LINQ), kein Zugangs-Objekt an fremden Code;
/// CQRS067 = Schreib-Fähigkeit liest nicht, Lese-Fähigkeit puffert nicht. Gegen die echte <c>MartenCoCommitStoreBase</c>
/// und echte Marten-Typen.
/// </summary>
public class StoreZugriffAnalyzerTests
{
    private static async Task<string[]> Ids(string code)
    {
        // Marten samt seinen Abhängigkeiten, die echte Basis und LINQ auf IQueryable ausdrücklich (nicht nur über die geladenen Assemblies).
        var marten = typeof(Marten.IDocumentStore).Assembly;
        var noetig = new[] { typeof(Abstractions.ICoCommitTracker).Assembly, marten,
                             typeof(Domain.Infrastructure.MartenCoCommitStoreBase).Assembly, typeof(System.Linq.Queryable).Assembly }
            .Concat(marten.GetReferencedAssemblies().Select(n => { try { return System.Reflection.Assembly.Load(n); } catch { return null; } })
                .Where(a => a != null).Select(a => a!));
        var refs = AppDomain.CurrentDomain.GetAssemblies().Concat(noetig)
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location).Distinct()
            .Select(l => (MetadataReference)MetadataReference.CreateFromFile(l))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("StoreZugriffProbe", new[] { CSharpSyntaxTree.ParseText(Vorspann + code) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        var ds = await comp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StoreZugriffAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return ds.Select(d => d.Id).ToArray();
    }

    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Linq; using System.Threading.Tasks;
using Abstractions; using Marten; using Domain.Infrastructure;
namespace Probe {
public record Zeile : IReadModel { public Guid Id { get; init; } public int Wert { get; init; } }
public interface ISchreibe : IWriteStore { Task SchreibeAsync(Zeile z); }
public interface IFinde : IReadStore { Task<Zeile?> FindeAsync(Guid id); }
public interface IAlle : IReadStore { Task<IReadOnlyList<Zeile>> AlleAsync(); }
public interface IBuch : IStore, ISchreibe, IFinde, IAlle { }
public static class Fremd { public static Task Schreibe(IDocumentStore s, Zeile z) => Task.CompletedTask; }
}
";

    [Fact]
    public async Task Puffern_und_Lesen_ueber_die_Positivliste_ist_erlaubt()
    {
        (await Ids(@"namespace Probe { public sealed class Buch : MartenCoCommitStoreBase, IBuch {
            public Buch(IDocumentStore s) : base(s) { }
            public Task SchreibeAsync(Zeile z) => EnqueueTransform<Zeile>(z.Id, alt => alt with { Wert = z.Wert });
            public async Task<Zeile?> FindeAsync(Guid id) { await using var s = Store.QuerySession(); return await s.LoadAsync<Zeile>(id); }
            public async Task<IReadOnlyList<Zeile>> AlleAsync() {
                await using var s = Store.QuerySession();
                var n = await s.Query<Zeile>().CountAsync();
                return await s.Query<Zeile>().Where(z => z.Wert > n).OrderBy(z => z.Wert).ToListAsync(); } } }"))
            .Should().BeEmpty("Puffer-Methode + Lese-Einstieg + LINQ-Materialisierung sind der vorgesehene Weg");
    }

    [Fact]
    public async Task Der_uebliche_Marten_Schreibweg_ist_CQRS066()
    {
        (await Ids(@"namespace Probe { public sealed class Buch : MartenCoCommitStoreBase, IBuch {
            public Buch(IDocumentStore s) : base(s) { }
            public async Task SchreibeAsync(Zeile z) {
                await using var s = Store.LightweightSession();
                s.Store(z);
                await s.SaveChangesAsync(); }
            public Task<Zeile?> FindeAsync(Guid id) => Task.FromResult<Zeile?>(null);
            public Task<IReadOnlyList<Zeile>> AlleAsync() => Task.FromResult<IReadOnlyList<Zeile>>(new List<Zeile>()); } }"))
            .Should().Equal(Enumerable.Repeat(StoreZugriffAnalyzer.ZugriffId, 3), "Session öffnen, speichern, committen — jeder Schritt am Puffer vorbei");
    }

    [Fact]
    public async Task Hintertuer_ueber_die_Lese_Session_ist_CQRS066()
    {
        (await Ids(@"namespace Probe { public sealed class Buch : MartenCoCommitStoreBase, IBuch {
            public Buch(IDocumentStore s) : base(s) { }
            public Task SchreibeAsync(Zeile z) => EnqueueStore(z.Id, z);
            public async Task<Zeile?> FindeAsync(Guid id) {
                await using var s = Store.QuerySession();
                var store = s.DocumentStore;
                var verbindung = s.Connection;
                return null; }
            public Task<IReadOnlyList<Zeile>> AlleAsync() => Task.FromResult<IReadOnlyList<Zeile>>(new List<Zeile>()); } }"))
            .Should().Equal(StoreZugriffAnalyzer.ZugriffId, StoreZugriffAnalyzer.ZugriffId);
    }

    [Fact]
    public async Task Zugang_an_fremden_Code_weiterreichen_ist_CQRS066_an_eigene_Helfer_nicht()
    {
        (await Ids(@"namespace Probe { public sealed class Buch : MartenCoCommitStoreBase, IBuch {
            public Buch(IDocumentStore s) : base(s) { }
            public Task SchreibeAsync(Zeile z) => Fremd.Schreibe(Store, z);
            public async Task<Zeile?> FindeAsync(Guid id) { await using var s = Store.QuerySession(); return await Lade(s, id); }
            private static Task<Zeile?> Lade(IQuerySession s, Guid id) => s.LoadAsync<Zeile>(id);
            public Task<IReadOnlyList<Zeile>> AlleAsync() => Task.FromResult<IReadOnlyList<Zeile>>(new List<Zeile>()); } }"))
            .Should().Equal(StoreZugriffAnalyzer.ZugriffId);
    }

    [Fact]
    public async Task Schreib_Fn_liest_und_Lese_Fn_puffert_ist_CQRS067()
    {
        (await Ids(@"namespace Probe { public sealed class Buch : MartenCoCommitStoreBase, IBuch {
            public Buch(IDocumentStore s) : base(s) { }
            public async Task SchreibeAsync(Zeile z) {
                await using var s = Store.QuerySession();
                if (await s.LoadAsync<Zeile>(z.Id) is null) await EnqueueStore(z.Id, z); }
            public async Task<Zeile?> FindeAsync(Guid id) {
                await EnqueueStore(id, new Zeile { Id = id });
                return null; }
            public Task<IReadOnlyList<Zeile>> AlleAsync() => Task.FromResult<IReadOnlyList<Zeile>>(new List<Zeile>()); } }"))
            .Should().Equal(StoreZugriffAnalyzer.RolleId, StoreZugriffAnalyzer.RolleId);
    }

    [Fact]
    public async Task Ohne_Co_Commit_Basis_oder_ohne_Faehigkeiten_greift_die_Regel_nicht()
    {
        (await Ids(@"namespace Probe {
            public sealed class Werkzeug { private readonly IDocumentStore _s; public Werkzeug(IDocumentStore s) => _s = s;
                public async Task Schreibe(Zeile z) { await using var x = _s.LightweightSession(); x.Store(z); await x.SaveChangesAsync(); } }
            public sealed class OhneFaehigkeit : MartenCoCommitStoreBase {
                public OhneFaehigkeit(IDocumentStore s) : base(s) { }
                public async Task Schreibe(Zeile z) { await using var x = Store.LightweightSession(); x.Store(z); await x.SaveChangesAsync(); } } }"))
            .Should().BeEmpty("die Regel gilt für Co-Commit-Stores (Basis mit Marke + Fähigkeiten), nicht für beliebige Marten-Nutzer");
    }
}
