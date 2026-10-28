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
/// Funktion als Baustein (Plan Pub/Sub-Neustrukturierung §3.3, Phase 3): der <c>FunktionsBausteinGenerator</c> liest NUR Signaturen — je Eingang ein
/// <c>Auf</c>, Herkunft, Lese-Fähigkeiten, freie Ausgabe (konkret, OneOf, T?, Strom, je auch als Task) — und erzeugt Dispatch (Einzel- und
/// Seiten-Form), den Hinweisplan und die Bausteine-Tabelle (den Weckplan prüft WeckplanGeneratorTests). Jede erlaubte Form muss als Generat kompilieren; was nicht passt, ist CQRS100/101.
/// </summary>
public class FunktionsBausteinGeneratorTests
{
    private const string Vorspann = @"
using System; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks; using Abstractions;
namespace Probe {
public partial class Konto : IState { public int Version { get; set; } public Guid Id { get; set; } }
public sealed record Mahne(Id<Konto> AggregateId, int Stufe) : ICommand<Konto>;
public sealed record Gemahnt(int Stufe) : IEvent;
public sealed record Ueberfaellig(int Tage) : IEvent;
public sealed record Bezahlt(decimal Betrag) : IEvent;
public sealed record Unnoetig() : ITransientEvent;
public sealed record Messwert(double Wert) : ITransientEvent;
public sealed record Datei : DateiGemeldet;
public interface ISucheKonto : IReadStore { Task<int> StufeAsync(Guid konto); }
public interface ISchreibeKonto : IWriteStore { Task SetzeAsync(Guid konto); }
public interface IPost : IDienst { Task SendeAsync(string an); }
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
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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
                public OneOf<Mahne, Unnoetig> Auf(Ueberfaellig e, Herkunft h) => new Mahne(h.NeueId<Konto>(), e.Tage);
            } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("new FunktionsBaustein(typeof(global::Probe.Mahnwesen), \"Mahnwesen\", Draussen: false, Gleichbleibend: true,");
        quelle.Should().Contain("new FunktionsEingang(typeof(global::Probe.Ueberfaellig), Hinweis: false, Strom: false, MitEvents: false, "
            + "new Type[] { typeof(global::Probe.Mahne), typeof(global::Probe.Unnoetig) }, new Type[] {  })");
        quelle.Should().Contain("case global::Probe.Ueberfaellig e: return Ausgaben.Einzeln(f.Auf(e, h), y => y.Value);");
        quelle.Should().Contain("new FunktionsUmsetzung(\"Mahnwesen\", typeof(global::Probe.Mahnwesen), (sp, z) => new global::Probe.Mahnwesen())");
        quelle.Should().Contain("Seite0(object o, IReadOnlyList<(IEvent Eingang, Herkunft Herkunft)> seite", "die Seiten-Form kommt aus der Einzel-Form");
    }

    [Fact]
    public void Jede_erlaubte_Rueckgabe_kompiliert_als_Generat()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Konkret : IFunktion { public Mahne Auf(Ueberfaellig e) => null!; }
            public sealed class Nichts : IFunktion { public Mahne? Auf(Ueberfaellig e) => null; }
            public sealed class Fall : IFunktion { public OneOf<Mahne, Gemahnt>? Auf(Ueberfaellig e) => null; }
            public sealed class Spaeter : IFunktion { public Task<OneOf<Mahne, Gemahnt>> Auf(Ueberfaellig e) => null!; }
            public sealed class Wert : IFunktion { public ValueTask<Mahne?> Auf(Ueberfaellig e) => default; }
            public sealed class Liste : IFunktion { public IEnumerable<OneOf<Mahne, Gemahnt>> Auf(Ueberfaellig e) { yield break; } }
            public sealed class SpaeteListe : IFunktion { public Task<IEnumerable<Mahne>> Auf(Ueberfaellig e) => null!; }
            public sealed class Strom : IFunktion
            {
                public async IAsyncEnumerable<OneOf<Mahne, Unnoetig>> Auf(Ueberfaellig e, Herkunft h, CancellationToken ct) { await Task.CompletedTask; yield break; }
                public Gemahnt Auf(Bezahlt e) => new(0);
            } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty("jede Form der Signatur hat ihren Adapter im Generat");
        quelle.Should().Contain("return Ausgaben.Einzeln(f.Auf(e), y => y);");
        quelle.Should().Contain("return Ausgaben.Einzeln(f.Auf(e), y => y?.Value);");
        quelle.Should().Contain("return Ausgaben.EinzelnAsync(f.Auf(e), y => y.Value);");
        quelle.Should().Contain("return Ausgaben.Strom(f.Auf(e), y => y.Value, ct);");
        quelle.Should().Contain("return Ausgaben.StromAsync(f.Auf(e, h, ct), y => y.Value, ct);");
        quelle.Should().Contain("return Ausgaben.StromAsync(f.Auf(e), y => y, ct);", "ein Strom im Task ist ein Strom");
        quelle.Should().Contain("Strom: true", "ein Strom bekommt Fortschritt beim Framework");
        quelle.Should().Contain("MitEvents: true", "ein Event in der Ausgabe steht immer im festgehaltenen Eintrag");
    }

    [Fact]
    public void Eine_Schnittstelle_laeuft_draussen_und_kennt_ihre_Umsetzungen()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public interface IPruefung : IFunktion { OneOf<Gemahnt, Unnoetig> Auf(Ueberfaellig e); }
            public sealed class Pruefung(IPost post) : IPruefung { public OneOf<Gemahnt, Unnoetig> Auf(Ueberfaellig e) => new Gemahnt(1); } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("typeof(global::Probe.IPruefung), \"IPruefung\", Draussen: true, Gleichbleibend: false,");
        quelle.Should().Contain("new FunktionsUmsetzung(\"Pruefung\", typeof(global::Probe.Pruefung), (sp, z) => new global::Probe.Pruefung(");
        quelle.Should().NotContain("\"Pruefung\", Draussen", "die Implementierung ist keine eigene Funktion");
    }

    [Fact]
    public void Dienst_oder_Lese_Faehigkeit_heisst_nicht_gleichbleibend()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class MitPost(IPost post) : IFunktion { public Mahne? Auf(Ueberfaellig e) => null; }
            public sealed class MitLesen : IFunktion { public Mahne? Auf(Ueberfaellig e, ISucheKonto suche) => null; } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("\"MitPost\", Draussen: false, Gleichbleibend: false,");
        quelle.Should().Contain("new Type[] { typeof(global::Probe.IPost) }", "der Dienst steht im Bedarf der Funktion");
        quelle.Should().Contain("\"MitLesen\", Draussen: false, Gleichbleibend: false,");
        quelle.Should().Contain("f.Auf(e, b!.Hole<global::Probe.ISucheKonto>())", "die Lese-Fähigkeit kommt je Aufruf aus dem Bereich");
    }

    [Fact]
    public void Ein_Hinweis_als_Eingang_steht_im_Hinweisplan()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Sensorik : IFunktion { public Mahne? Auf(Messwert m) => null; } }");

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("new FunktionsEingang(typeof(global::Probe.Messwert), Hinweis: true,");
        quelle.Should().Contain("[typeof(global::Probe.Messwert)] = new[] { \"funktion-Sensorik\" }");
    }

    /// <summary>Der Serializer-Kontext der Komposition, wie ihn der Codegen-Prepass um die Zustände ergänzt (hier von Hand, für die Probe).</summary>
    private const string Kontext = @"
        namespace Infrastructure.Serialization {
            public sealed class EventJsonSerializerContext
            {
                public static EventJsonSerializerContext Default { get; } = new();
                public System.Text.Json.Serialization.Metadata.JsonTypeInfo<Probe.Mittelwerte> Mittelwerte => null!;
            } }";

    [Fact]
    public void Ein_Zustand_im_Konstruktor_wird_erkannt_verdrahtet_und_ueber_den_Kontext_gesichert()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Mittelwerte { public Dictionary<Guid, double> Je { get; set; } = new(); public int Anzahl { get; set; } }
            [EineAdresse]
            public sealed class Schaetzer(IZustand<Mittelwerte> modell, IPost post) : IFunktion
            {
                public void Auf(Bezahlt e, Herkunft h) { modell.Wert.Anzahl++; }                    // lernt aus dem Log, ohne Ausgabe
                public Task Auf(Ueberfaellig e) => Task.CompletedTask;                                 // ebenso, asynchron
                public Mahne? Auf(Messwert m) => modell.Wert.Anzahl > 0 ? null : null;                 // Hinweis rein, Command raus
            } }" + Kontext);

        ids.Should().BeEmpty();
        fehler.Should().BeEmpty("das Generat kompiliert gegen den Kontext");
        quelle.Should().Contain("\"Schaetzer\", Draussen: false, Gleichbleibend: false,", "eine Funktion mit Zustand ist nie gleichbleibend (R12, R19)");
        quelle.Should().Contain("(sp, z) => new global::Probe.Schaetzer((global::Abstractions.IZustand<global::Probe.Mittelwerte>)z!, ",
            "der Zustand kommt aus der Aktivierung, nie aus dem Container");
        quelle.Should().Contain("Zustand: new FunktionsZustandsform(typeof(global::Probe.Mittelwerte), () => new global::Infrastructure.Funktionen.Zustand<global::Probe.Mittelwerte>()");
        quelle.Should().Contain("global::Infrastructure.Serialization.EventJsonSerializerContext.Default.Mittelwerte", "ohne Reflection: der generierte Kontext");
        quelle.Should().Contain("EineAdresse: true");
        quelle.Should().Contain("case global::Probe.Bezahlt e: f.Auf(e, h); return Ausgaben.Leer;");
        quelle.Should().Contain("case global::Probe.Ueberfaellig e: return Ausgaben.KeineAsync(f.Auf(e));");
        quelle.Should().Contain("new Type[] { typeof(global::Probe.IPost) }", "der Zustand ist kein Dienst im Bedarf");
    }

    [Fact]
    public void Eine_Funktion_mit_Lebenszyklus_ist_nie_gleichbleibend()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Laedt : IFunktion, ILebenszyklus { public Mahne? Auf(Ueberfaellig e) => null; } }");
        ids.Should().BeEmpty();
        fehler.Should().BeEmpty();
        quelle.Should().Contain("\"Laedt\", Draussen: false, Gleichbleibend: false,", "sie lädt etwas beim Aktivieren — ihre Ausgabe wird festgehalten");
    }

    [Theory]
    [InlineData("public sealed class Z(IZustand<Zahl> a, IZustand<Zahl> b) : IFunktion { public Mahne? Auf(Ueberfaellig e) => null; }", "zwei Zustände")]
    [InlineData("public sealed class Z(IZustand<List<int>> a) : IFunktion { public Mahne? Auf(Ueberfaellig e) => null; }", "generischer Zustand")]
    [InlineData("public sealed class Z(IZustand<Zahl> a) : IFunktion { public Gemahnt? Auf(Messwert m) => null; }", "Event aus einem Hinweis")]
    public void Zustand_und_Hinweis_ausser_Form_sind_CQRS100(string klasse, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Zahl { public int N { get; set; } } " + klasse + " }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Z\", Draussen", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    [Theory]
    [InlineData("public Mahne? Auf(Mahne c) => null;", "Eingang")]                                          // Command als Eingang
    [InlineData("public IEvent? Auf(Ueberfaellig e) => null;", "Ausgabe")]                                   // Schnittstelle als Ausgabe
    [InlineData("public Mahne? Auf(Ueberfaellig e, ISchreibeKonto s) => null;", "Parameter")]                // Schreib-Fähigkeit
    [InlineData("public Datei? Auf(Ueberfaellig e) => null;", "Quell-Nachricht")]                            // Quell-Nachricht als Ausgabe
    [InlineData("public void Auf(Ueberfaellig e) { }", "Ausgabe")]                                          // nichts (ohne Zustand)
    [InlineData("public static Mahne? Auf(Ueberfaellig e) => null;", "statisch")]                            // statisch
    public void Eine_Funktion_ausser_Form_ist_CQRS100(string methode, string worum)
    {
        var (quelle, ids, _) = Bausteine("namespace Probe { public sealed class Falsch : IFunktion { " + methode + " } }");
        ids.Should().Contain("CQRS100", worum);
        quelle.Should().NotContain("\"Falsch\", Draussen", "eine fehlerhafte Funktion fehlt im Dispatch");
    }

    [Fact]
    public void Fremde_Methoden_oder_kein_Auf_an_einer_Schnittstelle_sind_CQRS100()
    {
        Bausteine(@"namespace Probe { public interface IFremd : IFunktion { Mahne? Auf(Ueberfaellig e); int Zaehle(); } }")
            .Ids.Should().Contain("CQRS100");
        Bausteine(@"namespace Probe { public interface ILeer : IFunktion { } }")
            .Ids.Should().Contain("CQRS100", "eine Funktion hört mindestens einen Eingang");
        Bausteine(@"namespace Probe { public sealed class Leer : IFunktion { } }")
            .Ids.Should().Contain("CQRS100", "auch als Klasse");
    }

    [Fact]
    public void Zwei_Auf_fuer_denselben_Eingang_sind_CQRS101()
    {
        var (quelle, ids, fehler) = Bausteine(@"namespace Probe {
            public sealed class Doppelt : IFunktion
            {
                public Mahne? Auf(Ueberfaellig e) => null;
                public Gemahnt? Auf(Ueberfaellig e, Herkunft h) => null;
            } }");
        ids.Should().Contain("CQRS101");
        fehler.Should().BeEmpty("das Generat bleibt baubar — die Funktion fehlt");
        quelle.Should().NotContain("\"Doppelt\", Draussen");
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
    public async Task Die_Adapter_bringen_jede_Rueckgabe_auf_einen_Strom_und_null_heisst_nichts()
    {
        static async Task<List<IAusgabe>> Alle(IAsyncEnumerable<IAusgabe> s)
        {
            var l = new List<IAusgabe>();
            await foreach (var a in s) l.Add(a);
            return l;
        }

        (await Alle(Ausgaben.Einzeln<Aus?>(new Aus(1), y => y))).Should().ContainSingle();
        (await Alle(Ausgaben.Einzeln<Aus?>(null, y => y))).Should().BeEmpty();
        (await Alle(Ausgaben.EinzelnAsync(Task.FromResult<Aus?>(new Aus(2)), y => y))).Should().ContainSingle();
        (await Alle(Ausgaben.Strom(new[] { new Aus(1), null, new Aus(3) }, y => y))).Should().HaveCount(2);
        (await Alle(Ausgaben.StromAsync(Task.FromResult<IEnumerable<Aus>>(new[] { new Aus(1), new Aus(2) }), y => y))).Should().HaveCount(2);
        (await Alle(Ausgaben.StromAsync(Async(), y => y))).Select(a => ((Aus)a).N).Should().Equal(1, 2, 3);

        static async IAsyncEnumerable<Aus> Async()
        {
            for (var i = 1; i <= 3; i++) { await Task.Yield(); yield return new Aus(i); }
        }
    }
}
