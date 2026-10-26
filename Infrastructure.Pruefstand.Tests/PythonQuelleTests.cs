using System.Linq;
using DomainEditor;
using FluentAssertions;
using Domain.Pipeline.Bildaufbereitung;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Der Python-Quelltext eines Clients (docs/konzept-akteure.md §4): Implementierung = Klasse, die die generierte Basis erbt; je Zusage
/// ihr Rumpf. Lesen, Ersetzen und Anhängen ändern nur den einen Rumpf — der Rest der Datei bleibt Wort für Wort.
/// </summary>
public class PythonQuelleTests
{
    private const string Basis = """
        class KasseBasis(AkteurVertragBasis[S]):
            \"\"\"Client Kasse.\"\"\"

            CLIENT: ClassVar[str] = "Kasse"
            ZUSAGEN: ClassVar[dict] = {
                BestelltDto: Zusage("auf_bestellt", (KassiereDto,), akteur="Kassierer"),
                StorniertDto: Zusage("auf_storniert", ()),
            }

            @abstractmethod
            async def auf_bestellt(self, e: BestelltDto, ctx: MessageContext, state: S) -> AsyncIterator[KassiereDto]:
                \"\"\"Zusage.\"\"\"
                raise NotImplementedError

            @abstractmethod
            async def auf_storniert(self, e: StorniertDto, ctx: MessageContext, state: S) -> None:
                raise NotImplementedError
        """;

    private const string Impl = """
        import logging

        class Kasse(KasseBasis[KassenState]):
            def __init__(self):
                self.n = 0

            async def auf_bestellt(
                self, e: BestelltDto, ctx, state
            ):
                state.offen += 1
                if e.betrag > 0:
                    yield KassiereDto(aggregate_id=e.id)

            async def auf_storniert(self, e, ctx, state):
                state.offen -= 1


        def hilfe():
            return 1
        """;

    [Fact]
    public void Die_generierte_Basis_nennt_Client_Zusagen_Methoden_und_Signatur()
    {
        var b = PythonQuelle.ClientBasen(Basis).Single();
        b.Client.Should().Be("Kasse");
        b.Basis.Should().Be("KasseBasis");
        b.Auf.Select(a => (a.Event, a.Methode)).Should().Equal(("Bestellt", "auf_bestellt"), ("Storniert", "auf_storniert"));
        b.Auf[0].Ausgaenge.Should().Equal("Kassiere");
        b.Auf[0].Akteur.Should().Be("Kassierer");
        b.Auf[0].Def.Should().StartWith("async def auf_bestellt(self, e: BestelltDto").And.EndWith(":");
    }

    [Fact]
    public void Eine_Implementierung_ist_eine_Klasse_mit_der_Basis_und_ihr_Rumpf_ist_lesbar()
    {
        PythonQuelle.Klassen(Impl).Single().Basen.Should().Equal("KasseBasis");
        PythonQuelle.Rumpf(Impl, "Kasse", "auf_bestellt").Should()
            .Be("state.offen += 1\nif e.betrag > 0:\n    yield KassiereDto(aggregate_id=e.id)");
        PythonQuelle.Rumpf(Impl, "Kasse", "auf_storniert").Should().Be("state.offen -= 1");
        PythonQuelle.Rumpf(Impl, "Kasse", "auf_gibtsnicht").Should().BeNull();
    }

    [Fact]
    public void Rumpf_ersetzen_aendert_nur_diesen_Rumpf()
    {
        var neu = PythonQuelle.SetzeRumpf(Impl, "Kasse", "auf_storniert", "state.offen = 0\nreturn")!;
        PythonQuelle.Rumpf(neu, "Kasse", "auf_storniert").Should().Be("state.offen = 0\nreturn");
        PythonQuelle.Rumpf(neu, "Kasse", "auf_bestellt").Should().Be(PythonQuelle.Rumpf(Impl, "Kasse", "auf_bestellt"));
        neu.Should().Contain("def hilfe():\n    return 1");
    }

    [Fact]
    public void Eine_fehlende_Zusage_wird_mit_der_Signatur_der_Basis_angehaengt()
    {
        var def = PythonQuelle.ClientBasen(Basis).Single().Auf[1].Def;
        var ohne = PythonQuelle.SetzeRumpf(Impl, "Kasse", "auf_storniert", "pass")!.Replace("    async def auf_storniert(self, e, ctx, state):\n        pass\n", "");
        PythonQuelle.Rumpf(ohne, "Kasse", "auf_storniert").Should().BeNull();
        var mit = PythonQuelle.FuegeMethodeEin(ohne, "Kasse", def, "state.offen -= 1")!;
        PythonQuelle.Rumpf(mit, "Kasse", "auf_storniert").Should().Be("state.offen -= 1");
        mit.Should().Contain("def hilfe():");
    }

    [Fact]
    public void Eine_neue_Implementierung_erbt_die_Basis_und_hat_je_Zusage_ihre_Methode()
    {
        var b = PythonQuelle.ClientBasen(Basis).Single();
        var neu = PythonQuelle.NeueImplementierung("kasse_client", b, "Kasse");
        neu.Should().Contain("from kasse_client.generated.vertraege import KasseBasis")
           .And.Contain("from kasse_client.generated import BestelltDto, KassiereDto, StorniertDto")
           .And.Contain("class Kasse(KasseBasis[KasseState]):");
        var k = PythonQuelle.Klassen(neu).Single(x => x.Name == "Kasse");
        k.Basen.Should().Equal("KasseBasis");
        PythonQuelle.Rumpf(neu, "Kasse", "auf_bestellt").Should().Be("raise NotImplementedError");
        PythonQuelle.Rumpf(neu, "Kasse", "auf_storniert").Should().Be("raise NotImplementedError");
        neu.Should().Contain("state: KasseState)").And.NotContain("state: S)");
    }

    // ── Funktionen draußen in Python: Basis aus funktionen.py (je Eingang eine Methode auf_<eingang>), neue Implementierung, Lesen über die Basis ──

    private const string FunktionsGenerat = """
        class BildaufbereitungBasis(FunktionsBasis):
            \"\"\"Funktion IBildaufbereitung.\"\"\"

            FUNKTION: ClassVar[str] = "IBildaufbereitung"
            EINGAENGE: ClassVar[dict] = {
                DateiErkanntDto: Eingang("auf_datei_erkannt", {BildAufbereitetDto: Ausgabe.EVENT, BildNichtLesbarDto: Ausgabe.EVENT}),
            }

            @abstractmethod
            async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft) -> BildAufbereitetDto | BildNichtLesbarDto | None:
                \"\"\"höchstens eine Ausgabe\"\"\"
                ...
        """;

    [Fact]
    public void Funktions_Basis_nennt_Funktion_Methode_Signatur_und_DTOs()
    {
        var b = PythonQuelle.FunktionsBasen(FunktionsGenerat).Should().ContainSingle().Subject;
        (b.Funktion, b.Basis, b.Methode).Should().Be(("IBildaufbereitung", "BildaufbereitungBasis", "auf_datei_erkannt"));
        b.Def.Should().Be("async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft) -> BildAufbereitetDto | BildNichtLesbarDto | None:");
        b.Dtos.Should().Equal("DateiErkanntDto", "BildAufbereitetDto", "BildNichtLesbarDto");
    }

    [Fact]
    public void Neue_Funktions_Implementierung_erbt_die_Basis_und_hat_einen_Platzhalter_den_der_Rumpf_Edit_findet()
    {
        var neu = PythonQuelle.NeueFunktionsImplementierung("domain_client", PythonQuelle.FunktionsBasen(FunktionsGenerat)[0], "Bildaufbereitung");
        neu.Should().Contain("from domain_client.generated.funktionen import BildaufbereitungBasis")
            .And.Contain("from domain_client.generated import BildAufbereitetDto, BildNichtLesbarDto, DateiErkanntDto")
            .And.Contain("class Bildaufbereitung(BildaufbereitungBasis):\n    async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft)");
        PythonQuelle.Klassen(neu).Single().Basen.Should().Equal("BildaufbereitungBasis");
        PythonQuelle.Rumpf(neu, "Bildaufbereitung", "auf_datei_erkannt").Should().Be("raise NotImplementedError");
        PythonQuelle.Rumpf(PythonQuelle.SetzeRumpf(neu, "Bildaufbereitung", "auf_datei_erkannt", "return BildNichtLesbarDto(pfad=e.pfad, grund=\"x\")")!,
            "Bildaufbereitung", "auf_datei_erkannt").Should().StartWith("return BildNichtLesbarDto(");
    }

    [Fact]
    public void Die_Regel_fuer_Modul_und_Klasse_der_Python_Implementierung()
    {
        Abstractions.Funktionsvertrag.PythonModul("IBildaufbereitung").Should().Be("bildaufbereitung");
        Abstractions.Funktionsvertrag.PythonModul("IBildpaarBeurteilung").Should().Be("bildpaar_beurteilung");
        Abstractions.Funktionsvertrag.PythonModul("IKIModell").Should().Be("kimodell");
        Abstractions.Funktionsvertrag.OhneI("IBildpaarBeurteilung").Should().Be("BildpaarBeurteilung");
        Abstractions.Funktionsvertrag.PythonBasis("IBildpaarBeurteilung").Should().Be("BildpaarBeurteilungBasis");
    }

    [Fact]
    public void Der_Bestand_hat_eine_Python_Implementierung_der_Bildaufbereitung_als_Code_Fakt()
    {
        var wurzel = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", ".."));
        var py = PythonClients.Funktionen(wurzel);
        py["IBildaufbereitung"].Should().BeEquivalentTo(new { Datei = "Domain.Client.Worker.Python.ML/funktionen/bildaufbereitung.py",
            Klasse = "Bildaufbereitung", Basis = "BildaufbereitungBasis", Methode = "auf_datei_erkannt" });
        py["IBildaufbereitung"].Rumpf.Should().Contain("cv2.resize");
        py.Should().NotContainKey("ITraining", "der Training-Worker trägt sie als Client (ITraegt), nicht als eigene Funktions-Klasse");
    }
}
