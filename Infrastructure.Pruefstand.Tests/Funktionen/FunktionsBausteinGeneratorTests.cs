using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Funktion als Baustein (docs/04 §4.4, docs/10 §10.3): der <c>FunktionsBausteinGenerator</c> liest NUR Signaturen — je Eingang (ein Command ohne
/// Ziel) ein <c>Auf</c>, Herkunft, Lese-Fähigkeiten, die Ausgabe (immer asynchron: <c>Task&lt;T&gt;</c>, <c>Task&lt;OneOf&lt;…&gt;&gt;</c> oder
/// <c>IAsyncEnumerable&lt;…&gt;</c>; Commands ohne Ziel und Hinweise) — und erzeugt die Rechnung, die Verkettung und die Bausteine-Tabelle. Jede
/// erlaubte Form muss als Generat kompilieren; was nicht passt, ist CQRS100/101.
/// </summary>
public class FunktionsBausteinGeneratorTests
{
    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
public sealed record Mahne(Id<Konto> AggregateId, int Stufe) : ICommand<Konto>;
public sealed record Gemahnt(int Stufe) : ICommand;
public sealed record Ueberfaellig(int Tage) : ICommand;
public sealed record Bezahlt(decimal Betrag) : ICommand;
public sealed record Gebucht(int Stufe) : IEvent;
public sealed record Unnoetig() : ITransientEvent;
public sealed record Messwert(double Wert) : ITransientEvent;
public interface ISucheKonto : IReadStore { Task<int> StufeAsync(Guid konto); }
public interface ISchreibeKonto : IWriteStore { Task SetzeAsync(Guid konto); }
public interface IPost { Task SendeAsync(string an); }
}
";

    private static (string Quelle, string[] Ids, string[] Fehler) Lauf(ISourceGenerator generator, string code)
    {
        _ = typeof(IFunktion);
        _ = typeof(FunktionsBaustein);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();
        var comp = CSharpCompilation.Create("BausteinProbe",
            new[] { CSharpSyntaxTree.ParseText(Vorspann), CSharpSyntaxTree.ParseText("using System; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks; using Abstractions;\n" + code) },
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");

        CSharpGeneratorDriver.Create(generator).RunGeneratorsAndUpdateCompilation(comp, out var neu, out var diags);
        var quelle = string.Join("\n", neu.SyntaxTrees.Skip(2).Select(t => t.ToString()));
        // Das Generat muss mit der Probe zusammen kompilieren — sonst wäre eine erlaubte Form nur Text.
        var fehler = neu.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        return (quelle, diags.Select(d => d.Id).ToArray(), fehler);
    }

    private static (string Quelle, string[] Ids, string[] Fehler) Bausteine(string code) =>
        Lauf(new Infrastructure.SourceGeneration.FunktionsBausteinGenerator(), code);

    [Fact]
    public void Eine_Klasse_mit_Auf_ergibt_Dispatch_und_Tabelle()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Mahnwesen : IFunktion
            {
                public async Task<OneOf<Gemahnt, Unnoetig>> Auf(Ueberfaellig e, Herkunft h) => new Gemahnt(e.Tage);
            } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("new FunktionsBaustein(typeof(global::Probe.Mahnwesen), \"Mahnwesen\",");
        quelle.Should().Contain("new FunktionsEingang(typeof(global::Probe.Ueberfaellig), Strom: false, "
            + "new Type[] { typeof(global::Probe.Gemahnt), typeof(global::Probe.Unnoetig) }, new Type[] {  }, new Type[] { typeof(global::Probe.Ueberfaellig) })");
        quelle.Should().Contain("case 0: return Ausgaben.Einzeln(f.Auf((global::Probe.Ueberfaellig)a[0]!, h), y => (object?)y.Value);");
        quelle.Should().Contain("(sp, z) => new global::Probe.Mahnwesen(),", "die Fabrik der Klasse — eine Funktion rechnet im Host");
    }

    [Fact]
    public void Jede_erlaubte_Rueckgabe_kompiliert_als_Generat()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Einer : IFunktion { public Task<Gemahnt> Auf(Ueberfaellig e) => null!; }
            public sealed class Fall : IFunktion { public Task<OneOf<Gemahnt, Unnoetig>> Auf(Ueberfaellig e) => null!; }
            public sealed class Liste : IFunktion { public async IAsyncEnumerable<Gemahnt> Auf(Ueberfaellig e) { await Task.CompletedTask; yield break; } }
            public sealed class Strom : IFunktion
            {
                public async IAsyncEnumerable<OneOf<Gemahnt, Unnoetig>> Auf(Ueberfaellig e, Herkunft h, CancellationToken ct) { await Task.CompletedTask; yield break; }
                public async Task<Gemahnt> Auf(Bezahlt e) => new(0);
            } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty("jede Form der Signatur hat ihren Adapter im Generat");
        quelle.Should().Contain("return Ausgaben.Einzeln(f.Auf((global::Probe.Ueberfaellig)a[0]!), y => (object?)y);");
        quelle.Should().Contain("return Ausgaben.Einzeln(f.Auf((global::Probe.Ueberfaellig)a[0]!), y => (object?)y.Value);");
        quelle.Should().Contain("return Ausgaben.Strom(f.Auf((global::Probe.Ueberfaellig)a[0]!), y => (object?)y, ct);");
        quelle.Should().Contain("return Ausgaben.Strom(f.Auf((global::Probe.Ueberfaellig)a[0]!, h, ct), y => (object?)y.Value, ct);");
        quelle.Should().Contain("return Ausgaben.Einzeln(f.Auf((global::Probe.Bezahlt)a[0]!), y => (object?)y);");
        quelle.Should().Contain("Strom: true", "ein Strom: jede Ausgabe geht hinaus, bevor die nächste kommt");
    }

    // Eine Funktion ist immer asynchron (docs/04 §4.4): genau eine Ausgabe als Task<…>, keine bis viele als IAsyncEnumerable<…>.
    [Theory]
    [InlineData("public Gemahnt Auf(Ueberfaellig e) => null!;", "ein Wert ohne Task")]
    [InlineData("public OneOf<Gemahnt, Unnoetig> Auf(Ueberfaellig e) => default!;", "eine Wahl ohne Task")]
    [InlineData("public IEnumerable<Gemahnt> Auf(Ueberfaellig e) => [];", "ein synchroner Strom")]
    [InlineData("public IEnumerable<OneOf<Gemahnt, Unnoetig>> Auf(Ueberfaellig e) { yield break; }", "ein synchroner Strom mit Wahl")]
    public void Eine_synchrone_Rueckgabe_ist_CQRS100(string methode, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Falsch : IFunktion { " + methode + " } }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Falsch\",", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    // Eine Funktion kennt kein Aggregat: an ein Aggregat geht es über eine Übersetzung (ITranslate<TSource, TTarget>).
    [Theory]
    [InlineData("public Task<Mahne> Auf(Ueberfaellig e) => null!;", "allein")]
    [InlineData("public Task<OneOf<Gemahnt, Mahne>> Auf(Ueberfaellig e) => null!;", "in einer Wahl")]
    [InlineData("public async IAsyncEnumerable<Mahne> Auf(Ueberfaellig e) { await Task.CompletedTask; yield break; }", "in einem Strom")]
    public void Ein_Command_an_ein_Aggregat_als_Ausgabe_ist_CQRS100(string methode, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Falsch : IFunktion { " + methode + " } }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Falsch\",", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    [Fact]
    public void Eine_Schnittstelle_mit_IFunktion_ist_CQRS100()
    {
        var (quelle, ids, _) = Bausteine(@"namespace Probe {
            public interface IPruefung : IFunktion { Task<OneOf<Gemahnt, Unnoetig>> Auf(Ueberfaellig e); } }");

        ids.Should().Contain("CQRS100", "eine Funktion ist eine Klasse im Host — was draußen rechnet, ist Arbeit eines Akteurs");
        quelle.Should().NotContain("\"IPruefung\",", "eine Schnittstelle ist kein Baustein");
    }

    [Fact]
    public void Technik_und_Lese_Faehigkeit_stehen_im_Bedarf_bzw_im_Aufruf()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class MitPost(IPost post) : IFunktion { public Task<Gemahnt> Auf(Ueberfaellig e) => null!; }
            public sealed class MitLesen : IFunktion { public Task<Gemahnt> Auf(Ueberfaellig e, ISucheKonto suche) => null!; } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("\"MitPost\",");
        quelle.Should().Contain("new Type[] { typeof(global::Probe.IPost) }", "die Technik steht im Bedarf");
        quelle.Should().Contain("\"MitLesen\",");
        quelle.Should().Contain("f.Auf((global::Probe.Ueberfaellig)a[0]!, b!.Hole<global::Probe.ISucheKonto>())", "die Lese-Fähigkeit kommt je Aufruf aus dem Bereich");
    }

    [Theory]
    [InlineData("public Task<Gemahnt> Auf(Ueberfaellig u, Messwert m) => null!;", "ein Hinweis ist kein Eingang")]
    [InlineData("public Task<Gemahnt> Auf(Ueberfaellig u, Bezahlt b) => null!;", "ein Auf nimmt genau eine Nachricht")]
    [InlineData("public Task<Gemahnt> Auf(Ueberfaellig u, Gebucht g) => null!;", "ein Event ist kein Eingang")]
    [InlineData("public Task<Gemahnt> Auf(Bezahlt? b) => null!;", "ein Eingang ist nie nullable")]
    [InlineData("public Task<Gemahnt> Auf(IReadOnlyList<Bezahlt> alle) => null!;", "eine Liste ist kein Eingang")]
    [InlineData("public Task<Gemahnt> Auf(OneOf<Ueberfaellig, Bezahlt> m) => null!;", "ein OneOf ist kein Eingang")]
    public void Ein_Auf_nimmt_genau_eine_Nachricht_sonst_CQRS100(string methode, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Falsch : IFunktion { " + methode + " } }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Falsch\",");
    }

    [Fact]
    public void Eine_generische_Funktion_wird_je_Typargument_eines_Knotens_ein_Baustein()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public interface IFormat { }
            public sealed record Vorschau : IFormat;
            public sealed record Gross : IFormat;
            public sealed record Verkleinert<TFormat>(int Breite) : ICommand where TFormat : IFormat;
            public sealed class Lies : IFunktion { public async Task<Gemahnt> Auf(Ueberfaellig u) => new(1); }
            public sealed class Verkleinerung<TFormat> : IFunktion where TFormat : IFormat { public async Task<Verkleinert<TFormat>> Auf(Gemahnt g) => new(1); }
            public sealed class Melde : IFunktion
            {
                public async Task<Unnoetig> Auf(Verkleinert<Vorschau> v) => new();
                public async Task<Unnoetig> Auf(Verkleinert<Gross> g) => new();
            }
            public sealed record Aufbereitung([Kopf] Lies Lies, [Von(nameof(Lies))] Verkleinerung<Vorschau> Klein, [Von(nameof(Lies))] Verkleinerung<Gross> Gross,
                [Von(nameof(Klein), nameof(Gross))] Melde Melde) : IGraph; }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty("die konstruierten Bausteine kompilieren");
        quelle.Should().Contain("new FunktionsBaustein(typeof(global::Probe.Verkleinerung<global::Probe.Vorschau>), \"Verkleinerung<Vorschau>\",");
        quelle.Should().Contain("new FunktionsBaustein(typeof(global::Probe.Verkleinerung<global::Probe.Gross>), \"Verkleinerung<Gross>\",");
        quelle.Should().NotContain("Verkleinerung<TFormat>", "die offene Definition ist eine Blaupause");
    }

    [Fact]
    public void Eine_Quellfunktion_lauscht_ihre_Rechnung_ruft_Lausche_unter_der_Nummer_hinter_den_Auf()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Wecker : IFunktion
            {
                public async IAsyncEnumerable<OneOf<Ueberfaellig, Unnoetig>> Lausche([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
                { await Task.CompletedTask; yield break; }
            } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("\"Wecker\",").And.Contain("Lausche: 0)");
        quelle.Should().Contain("case 0: return Ausgaben.Strom(f.Lausche(ct), y => (object?)y.Value, ct);");
    }

    [Theory]
    [InlineData("public IEnumerable<Ueberfaellig> Lausche(CancellationToken ct) => [];", "kein asynchroner Strom")]
    [InlineData("public async IAsyncEnumerable<Ueberfaellig> Lausche() { await Task.CompletedTask; yield break; }", "ohne Abbruch")]
    [InlineData("public async IAsyncEnumerable<Mahne> Lausche(CancellationToken ct) { await Task.CompletedTask; yield break; }", "ein Command an ein Aggregat")]
    [InlineData("public async IAsyncEnumerable<Ueberfaellig> Lausche(CancellationToken ct) { await Task.CompletedTask; yield break; } "
        + "public Task<Gemahnt> Auf(Bezahlt b) => null!;", "Lausche und Auf")]
    public void Eine_Quellfunktion_ausser_Form_ist_CQRS100(string methoden, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Falsch : IFunktion { " + methoden + " } }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Falsch\",");
    }

    [Fact]
    public void Eine_Funktion_hoert_keinen_Hinweis_aber_gibt_einen_aus()
    {
        // Hinweise gehen nur an Akteure draußen (Entscheidung 14): als Eingang ein Formfehler, als Ausgabe erlaubt.
        var (quelle, ids, _) = Bausteine(@"namespace Probe {
            public sealed class Sensorik : IFunktion { public async IAsyncEnumerable<Gemahnt> Auf(Messwert m) { await Task.CompletedTask; yield break; } } }");
        ids.Should().Contain("CQRS100");
        quelle.Should().NotContain("\"Sensorik\",");
        var (aus, ausIds, fehler) = Bausteine(@"namespace Probe {
            public sealed class Melder : IFunktion { public async Task<OneOf<Gemahnt, Messwert>> Auf(Ueberfaellig u) => new Messwert(u.Tage); } }");
        ausIds.Should().BeEmpty();
        fehler.Should().BeEmpty();
        aus.Should().Contain("typeof(global::Probe.Gemahnt), typeof(global::Probe.Messwert)", "ein Hinweis ist ein Ausgang");
    }

    /// <summary>
    /// Der Serializer-Kontext der Komposition, wie ihn der Codegen-Prepass um die Zustände ergänzt (hier von Hand, für die Probe) — auch um die der
    /// Katalogfunktionen aus Core, die der Generator mit jeder Komposition sieht.
    /// </summary>
    private const string Kontext = @"
        namespace Infrastructure.Serialization {
            public sealed class EventJsonSerializerContext
            {
                public static EventJsonSerializerContext Default { get; } = new();
                public System.Text.Json.Serialization.Metadata.JsonTypeInfo<Probe.Mittelwerte> Mittelwerte => null!;
                public System.Text.Json.Serialization.Metadata.JsonTypeInfo<Core.Katalog.Gesehen> Gesehen => null!;
                public System.Text.Json.Serialization.Metadata.JsonTypeInfo<Core.Katalog.Taktstand> Taktstand => null!;
            } }";

    [Fact]
    public void Ein_Zustand_im_Konstruktor_wird_erkannt_verdrahtet_und_ueber_den_Kontext_gesichert()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Mittelwerte { public Dictionary<Guid, double> Je { get; set; } = new(); public int Anzahl { get; set; } }
            public sealed class Schaetzer(IZustand<Mittelwerte> modell, IPost post) : IFunktion
            {
                // lernt aus einem Command, ohne Ausgabe
                public async IAsyncEnumerable<Gemahnt> Auf(Bezahlt e, Herkunft h) { await Task.CompletedTask; modell.Wert.Anzahl++; yield break; }
                // Command rein, Command raus
                public async IAsyncEnumerable<Gemahnt> Auf(Ueberfaellig u) { await Task.CompletedTask; if (modell.Wert.Anzahl < 0) yield return null!; }
            } }" + Kontext);

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty("das Generat kompiliert gegen den Kontext");
        quelle.Should().Contain("\"Schaetzer\",");
        quelle.Should().Contain("(sp, z) => new global::Probe.Schaetzer((global::Abstractions.IZustand<global::Probe.Mittelwerte>)z!, ",
            "der Zustand kommt aus der Aktivierung, nie aus dem Container");
        quelle.Should().Contain("Zustand: new global::Infrastructure.Funktionen.FunktionsZustandsform(typeof(global::Probe.Mittelwerte), () => new global::Infrastructure.Funktionen.Zustand<global::Probe.Mittelwerte>()");
        quelle.Should().Contain("global::Infrastructure.Serialization.EventJsonSerializerContext.Default.Mittelwerte", "ohne Reflection: der generierte Kontext");
        quelle.Should().Contain("case 0: return Ausgaben.Strom(f.Auf((global::Probe.Bezahlt)a[0]!, h), y => (object?)y, ct);", "ein leerer Strom heißt nichts");
        quelle.Should().Contain("new Type[] { typeof(global::Probe.IPost) }", "der Zustand ist keine Technik im Bedarf");
    }

    [Fact]
    public void Eine_Funktion_hoert_nie_ein_Event_auch_nicht_als_Antwort_und_gibt_nie_eins_aus()
    {
        Bausteine(@"namespace Probe { public sealed class Lauscher : IFunktion { public Task<Gemahnt> Auf(Gebucht e) => null!; } }")
            .Ids.Should().Contain("CQRS100", "eine Funktion liest nie das Log");
        Bausteine(@"namespace Probe { public sealed class Schreiber : IFunktion { public Task<Gebucht> Auf(Ueberfaellig e) => null!; } }")
            .Ids.Should().Contain("CQRS100", "eine Funktion schreibt nie ins Log");
        var (quelle, ids, _) = Bausteine(@"namespace Probe {
            public sealed class Kette : IFunktion
            {
                public Task<Gemahnt> Auf(Ueberfaellig e) => null!;
                public async IAsyncEnumerable<Bezahlt> Auf(Gebucht e) { await Task.CompletedTask; yield break; }
            } }");
        ids.Should().Contain("CQRS100", "auch neben einem Command ist ein Event kein Eingang — die Antwort eines Aggregats geht über eine Übersetzung "
            + "an den Kopf eines Graphen");
        quelle.Should().NotContain("\"Kette\",");
    }

    [Theory]
    [InlineData("public sealed class Z(IZustand<Zahl> a, IZustand<Zahl> b) : IFunktion { public Task<Gemahnt> Auf(Ueberfaellig e) => null!; }", "zwei Zustände")]
    [InlineData("public sealed class Z(IZustand<List<int>> a) : IFunktion { public Task<Gemahnt> Auf(Ueberfaellig e) => null!; }", "generischer Zustand")]
    [InlineData("public sealed class Z(IZustand<Zahl> a) : IFunktion { public Task<Gemahnt> Auf(Messwert m) => null!; }", "Hinweis als Eingang")]
    public void Zustand_und_Hinweis_ausser_Form_sind_CQRS100(string klasse, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Zahl { public int N { get; set; } } " + klasse + " }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Z\",", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    [Theory]
    [InlineData("public Task<Gemahnt> Auf(Mahne c) => null!;", "Eingang")]                                     // Command an ein Aggregat als Eingang
    [InlineData("public Task<IEvent> Auf(Ueberfaellig e) => null!;", "Ausgabe")]                              // Schnittstelle als Ausgabe
    [InlineData("public Task<Gemahnt> Auf(Ueberfaellig e, ISchreibeKonto s) => null!;", "Parameter")]           // Schreib-Fähigkeit
    [InlineData("public Task<Gebucht> Auf(Ueberfaellig e) => null!;", "Event")]                                // Event als Ausgabe
    [InlineData("public void Auf(Ueberfaellig e) { }", "Ausgabe")]                                              // nichts
    [InlineData("public Task Auf(Ueberfaellig e) => Task.CompletedTask;", "Ausgabe")]                           // nichts, asynchron
    [InlineData("public Task<Gemahnt?> Auf(Ueberfaellig e) => null!;", "nullable")]                             // T? — „nichts“ ist ein leerer Strom
    [InlineData("public ValueTask<Gemahnt> Auf(Ueberfaellig e) => default;", "ValueTask")]                     // ValueTask
    [InlineData("public Task<IEnumerable<Gemahnt>> Auf(Ueberfaellig e) => null!;", "Strom im Task")]           // ein Strom im Task ist ein IAsyncEnumerable
    [InlineData("public Task<IAsyncEnumerable<Gemahnt>> Auf(Ueberfaellig e) => null!;", "asynchroner Strom im Task")]
    [InlineData("public static Task<Gemahnt> Auf(Ueberfaellig e) => null!;", "statisch")]                       // statisch
    public void Eine_Funktion_ausser_Form_ist_CQRS100(string methode, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Falsch : IFunktion { " + methode + " } }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Falsch\",", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    [Fact]
    public void Eine_Funktion_ohne_Auf_ist_CQRS100()
    {
        Bausteine(@"namespace Probe { public sealed class Leer : IFunktion { } }")
            .Ids.Should().Contain("CQRS100", "eine Funktion hört mindestens einen Eingang");
        Bausteine(@"namespace Probe { public sealed class Mit : IFunktion { public Task<Gemahnt> Auf(Ueberfaellig e) => null!; public int Zaehle() => 0; } }")
            .Ids.Should().BeEmpty("eine Klasse darf weitere Methoden haben — Funktion ist nur, was Auf heißt");
    }

    [Fact]
    public void Zwei_Auf_fuer_denselben_Eingang_sind_CQRS101()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Doppelt : IFunktion
            {
                public Task<Gemahnt> Auf(Ueberfaellig e) => null!;
                public Task<Bezahlt> Auf(Ueberfaellig e, Herkunft h) => null!;
            } }");
        ids.Should().Contain("CQRS101");
        fehler.Should().BeEmpty("das Generat bleibt baubar — die Funktion fehlt");
        quelle.Should().NotContain("\"Doppelt\",");
    }

    // ── Herkunft und Ausgaben (Laufzeit, store-frei) ─────────────────────────────────────────────────────────────────

    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed class Anderes : IState { public int Version { get; set; } public Guid Id { get; set; } }

    [Fact]
    public void Neue_Ids_sind_abgeleitet_gleich_beim_Wiederholen_und_verschieden_je_Baustein_Zweck_und_Typ()
    {
        var stream = Guid.NewGuid();
        var zeit = DateTimeOffset.UnixEpoch;
        var h = new Herkunft(stream, 3, zeit, "anna", "funktion:A");
        var wieder = new Herkunft(stream, 3, zeit.AddDays(1), null, "funktion:A");

        h.NeueId<Ziel>().Should().Be(wieder.NeueId<Ziel>(), "derselbe Eingang ergibt beim Wiederholen dieselbe Id (R13)");
        h.NeueId<Ziel>(1).Should().Be(wieder.NeueId<Ziel>("1"));
        h.NeueId<Ziel>().Should().NotBe(new Herkunft(stream, 3, zeit, null, "funktion:B").NeueId<Ziel>(), "zwei Bausteine leiten verschieden ab");
        h.NeueId<Ziel>().Should().NotBe(new Herkunft(stream, 4, zeit, null, "funktion:A").NeueId<Ziel>(), "ein anderer Eingang");
        h.NeueId<Ziel>(1).Should().NotBe(h.NeueId<Ziel>(2), "der Zweck trennt mehrere Ids aus einem Eingang");
        h.NeueId<Ziel>().Wert.Should().NotBe(h.NeueId<Anderes>().Wert, "der Ziel-Typ gehört zur Ableitung");
    }

    private sealed record Aus(int N) : ICommand { public Guid Ziel => Guid.Empty; }

    [Fact]
    public async Task Die_Adapter_bringen_Task_und_Strom_auf_einen_Strom_und_null_heisst_nichts()
    {
        static async Task<List<object>> Alle(IAsyncEnumerable<object> s)
        {
            var l = new List<object>();
            await foreach (var a in s) l.Add(a);
            return l;
        }

        (await Alle(Ausgaben.Einzeln<Aus?, Aus>(Task.FromResult<Aus?>(new Aus(1)), y => y))).Should().ContainSingle();
        (await Alle(Ausgaben.Einzeln<Aus?, Aus>(Task.FromResult<Aus?>(null), y => y))).Should().BeEmpty();
        (await Alle(Ausgaben.Strom<Aus?, Aus>(MitLuecke(), y => y))).Should().HaveCount(2);
        (await Alle(Ausgaben.Strom(Async(), y => y))).Select(a => ((Aus)a).N).Should().Equal(1, 2, 3);
        (await Alle(Ausgaben.Strom<Aus, Aus>(null, y => y))).Should().BeEmpty("kein Strom heißt nichts");

        static async IAsyncEnumerable<Aus> Async()
        {
            for (var i = 1; i <= 3; i++) { await Task.Yield(); yield return new Aus(i); }
        }

        static async IAsyncEnumerable<Aus?> MitLuecke()
        {
            foreach (var a in new[] { new Aus(1), null, new Aus(3) }) { await Task.Yield(); yield return a; }
        }
    }
}
