using System.Linq;
using DomainEditor;
using FluentAssertions;

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
}
