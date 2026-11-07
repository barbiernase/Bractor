using System.Linq;
using DomainEditor;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Der Python-Quelltext eines Clients (docs/konzept-akteure.md §4): Implementierung = Klasse, die die generierte Basis erbt; je gehörtem Event ihr
/// Rumpf (die Reaktion). Lesen, Ersetzen und Anhängen ändern nur den einen Rumpf — der Rest der Datei bleibt Wort für Wort.
/// </summary>
public class PythonQuelleTests
{
    private const string Basis = """
        class KasseBasis(ClientVertragBasis[S]):
            \"\"\"Client Shop.Kasse — verkörpert Kassierer.\"\"\"

            CLIENT: ClassVar[str] = "Kasse"
            AKTEURE: ClassVar[tuple] = ("Kassierer",)
            HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Reaktion oder Kenntnis
                BestelltDto: "auf_bestellt",
                StorniertDto: "auf_storniert",
            }
            SPONTAN: ClassVar[tuple] = (KassiereDto, StorniereDto,)   # IDarf der Akteure: Commands und Hinweise

            @abstractmethod
            async def auf_bestellt(self, e: BestelltDto, ctx: MessageContext, state: S) -> AsyncIterator[KassiereDto | StorniereDto] | None:
                \"\"\"IHoert<Bestellt> — die Reaktion\"\"\"
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
                self, e: BestelltDto, h, state
            ):
                state.offen += 1
                if e.betrag > 0:
                    return KassiereDto(aggregate_id=e.id)

            async def auf_storniert(self, e, ctx, state):
                state.offen -= 1


        def hilfe():
            return 1
        """;

    [Fact]
    public void Die_generierte_Basis_nennt_Client_Gehoertes_Methoden_und_Signatur()
    {
        var b = PythonQuelle.ClientBasen(Basis).Single();
        b.Client.Should().Be("Kasse");
        b.Basis.Should().Be("KasseBasis");
        b.Auf.Select(a => (a.Event, a.Methode)).Should().Equal(("Bestellt", "auf_bestellt"), ("Storniert", "auf_storniert"));
        b.Auf[0].Ausgaenge.Should().Equal("Kassiere", "Storniere");
        b.Auf[1].Ausgaenge.Should().Equal(["Kassiere", "Storniere"], "jede Reaktion darf mit dem IDarf der Akteure antworten");
        b.Auf[0].Def.Should().StartWith("async def auf_bestellt(self, e: BestelltDto").And.EndWith(":");
    }

    [Fact]
    public void Eine_Implementierung_ist_eine_Klasse_mit_der_Basis_und_ihr_Rumpf_ist_lesbar()
    {
        PythonQuelle.Klassen(Impl).Single().Basen.Should().Equal("KasseBasis");
        PythonQuelle.Rumpf(Impl, "Kasse", "auf_bestellt").Should()
            .Be("state.offen += 1\nif e.betrag > 0:\n    return KassiereDto(aggregate_id=e.id)");
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
    public void Eine_fehlende_Methode_wird_mit_der_Signatur_der_Basis_angehaengt()
    {
        var def = PythonQuelle.ClientBasen(Basis).Single().Auf[1].Def;
        var ohne = PythonQuelle.SetzeRumpf(Impl, "Kasse", "auf_storniert", "pass")!.Replace("    async def auf_storniert(self, e, ctx, state):\n        pass\n", "");
        PythonQuelle.Rumpf(ohne, "Kasse", "auf_storniert").Should().BeNull();
        var mit = PythonQuelle.FuegeMethodeEin(ohne, "Kasse", def, "state.offen -= 1")!;
        PythonQuelle.Rumpf(mit, "Kasse", "auf_storniert").Should().Be("state.offen -= 1");
        mit.Should().Contain("def hilfe():");
    }

    [Fact]
    public void Eine_neue_Implementierung_erbt_die_Basis_und_hat_je_Eingang_ihre_Methode()
    {
        var b = PythonQuelle.ClientBasen(Basis).Single();
        var neu = PythonQuelle.NeueImplementierung("kasse_client", b, "Kasse");
        neu.Should().Contain("from kasse_client.generated.vertraege import KasseBasis")
           .And.Contain("from kasse_client.generated import BestelltDto, KassiereDto, StorniereDto, StorniertDto")
           .And.Contain("class Kasse(KasseBasis[KasseState]):");
        var k = PythonQuelle.Klassen(neu).Single(x => x.Name == "Kasse");
        k.Basen.Should().Equal("KasseBasis");
        PythonQuelle.Rumpf(neu, "Kasse", "auf_bestellt").Should().Be("raise NotImplementedError");
        PythonQuelle.Rumpf(neu, "Kasse", "auf_storniert").Should().Be("raise NotImplementedError");
        neu.Should().Contain("state: KasseState)").And.NotContain("state: S)");
    }

    [Fact]
    public void Der_Bestand_liest_den_Trainings_Worker_als_Code_Fakt()
    {
        // Die Reaktion auf ein Event (HOERT in vertraege.py) mit ihrem Rumpf.
        var wurzel = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", ".."));
        var impl = PythonClients.Lies(wurzel)["TrainingsWorker"].Single(i => i.Klasse == "TrainingWorker");
        impl.Datei.Should().Be("Domain.Client.Worker.Python.ML/domain_client/training_worker.py");
        var r = impl.Rumpfe.Single(x => x.Methode == "auf_training_angefordert");
        r.Event.Should().Be("TrainingAngefordert");
        r.Rumpf.Should().NotBeNullOrWhiteSpace("der Rumpf ist die Logik draußen");
        r.Signatur.Should().Contain("MeldeFortschrittDto", "die Reaktion gibt Commands seines IDarf aus");
    }
}
