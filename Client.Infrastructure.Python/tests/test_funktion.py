"""
Funktionen draußen tragen (Plan Pub/Sub-Neustrukturierung §2.2, §2.7: ITraegt) — domänenfrei, mit minimalen betterproto-Fakes.

Bewiesen wird der FunktionsLaeufer des SDK: Anbieten (Funktion + Slots), Eingang auspacken, `auf_<eingang>(e, h)` höchstens SLOTS-mal
gleichzeitig, Lebenszeichen während langer Läufe, Signatur (falscher Typ, zwei Ausgaben ohne Strom oder Ausnahme → Fehler statt Antwort),
die Antwort als geordnete Ausgaben mit Art (Command/Event/Hinweis), ein Strom in Stücken mit Stelle (und ab einer schon festgehaltenen Stelle
weiter) — und der Weg vom Read-Loop über die Verarbeitungsschleife zum Läufer.
"""

import asyncio
from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client import Ausgabe, Eingang, FunktionsBasis, FunktionsLaeufer, Herkunft
from cqrs_client.client import CqrsClient
from cqrs_client.leitung import ArbeitsAuftrag, Nutzlast, ServerMessage
from cqrs_client.mapper import PayloadMapper
from cqrs_client.router import MessageRouter


@dataclass
class DateiDto(betterproto.Message):
    pfad: str = betterproto.string_field(1)


@dataclass
class SkalierteDto(betterproto.Message):
    pfad: str = betterproto.string_field(1)


@dataclass
class UnlesbarDto(betterproto.Message):
    grund: str = betterproto.string_field(1)


@dataclass
class MeldeDto(betterproto.Message):
    n: int = betterproto.int32_field(1)


@dataclass
class FremdDto(betterproto.Message):
    x: int = betterproto.int32_field(1)


# Die „generierte Nutzlast-Tabelle“ (domain_client/generated/nutzlast_tabelle.py). Hüllen und ServerMessage kommen aus der echten Leitung.
GEN = {"Datei": DateiDto, "Skalierte": SkalierteDto, "Unlesbar": UnlesbarDto, "Melde": MeldeDto, "Fremd": FremdDto}


class SkalierungBasis(FunktionsBasis):
    """So sieht die generierte Basis aus (domain_client/generated/funktionen.py)."""
    FUNKTION = "ISkalierung"
    EINGAENGE = {DateiDto: Eingang("auf_datei", {SkalierteDto: Ausgabe.EVENT, UnlesbarDto: Ausgabe.EVENT})}


class StromBasis(FunktionsBasis):
    FUNKTION = "IZaehlung"
    EINGAENGE = {DateiDto: Eingang("auf_datei", {MeldeDto: Ausgabe.COMMAND}, strom=True)}


class Skalierer(SkalierungBasis):
    SLOTS = 2

    def __init__(self, slots=None, dauer=0.0, liefere=None):
        super().__init__(slots)
        self.dauer = dauer
        self.liefere = liefere
        self.gleichzeitig = 0
        self.max_gleichzeitig = 0
        self.gesehen: list[tuple[DateiDto, Herkunft]] = []

    async def auf_datei(self, e, h):
        self.gesehen.append((e, h))
        self.gleichzeitig += 1
        self.max_gleichzeitig = max(self.max_gleichzeitig, self.gleichzeitig)
        try:
            await asyncio.sleep(self.dauer)
            if self.liefere is not None:
                return self.liefere(e)
            return SkalierteDto(pfad=e.pfad + ".klein")
        finally:
            self.gleichzeitig -= 1


class Zaehler(StromBasis):
    async def auf_datei(self, e, h):
        for n in range(3):
            yield MeldeDto(n=n)


class FakeProxy:
    def __init__(self):
        self.angebote = []
        self.ergebnisse = []
        self.lebt = []

    async def send_funktionen_anbieten(self, angebote):
        self.angebote.append(list(angebote))

    async def send_arbeits_ergebnis(self, vorgang, ausgaben=None, ab=0, fertig=True, fehler=""):
        self.ergebnisse.append((vorgang, list(ausgaben or []), ab, fertig, fehler))

    async def send_arbeit_lebt(self, vorgang):
        self.lebt.append(vorgang)


def _eingang(vorgang="v1", funktion="ISkalierung", pfad="/roh/a.tiff", ab=0):
    return ArbeitsAuftrag(vorgang=vorgang, korrelation="k1", funktion=funktion, eingang=Nutzlast(typ="Datei", daten=bytes(DateiDto(pfad=pfad))),
                          akteur="KameraSystem", stream="s1", version=3, zeit_utc=1_700_000_000_000, ab=ab)


def sync(fn):
    """pytest-asyncio ist nicht überall installiert (gpu_env) — async-Tests laufen über asyncio.run."""
    def lauf():
        asyncio.run(fn())
    lauf.__name__ = fn.__name__
    return lauf


def _laeufer(*funktionen, lebenszeichen_s=10.0):
    proxy = FakeProxy()
    return FunktionsLaeufer(list(funktionen), proxy, PayloadMapper(GEN), lebenszeichen_s=lebenszeichen_s), proxy


@sync
async def test_anbieten_nennt_funktion_und_slots():
    laeufer, proxy = _laeufer(Skalierer())
    await laeufer.biete_an()
    assert proxy.angebote == [[("ISkalierung", 2)]]
    assert _laeufer(Skalierer(slots=5))[0].angebote == [("ISkalierung", 5)]


@sync
async def test_eingang_wird_mit_herkunft_gerufen_und_die_antwort_geht_mit_art_zurueck():
    f = Skalierer()
    laeufer, proxy = _laeufer(f)

    await laeufer.nimm(_eingang())

    e, h = f.gesehen[0]
    assert e == DateiDto(pfad="/roh/a.tiff")
    assert (h.stream, h.version, h.akteur, h.korrelation, h.schluessel) == ("s1", 3, "KameraSystem", "k1", "v1")
    vorgang, ausgaben, ab, fertig, fehler = proxy.ergebnisse[0]
    assert (vorgang, ab, fertig, fehler) == ("v1", 0, True, "")
    [(art, nutzlast)] = ausgaben
    assert art == Ausgabe.EVENT and nutzlast.typ == "Skalierte"
    assert SkalierteDto().parse(nutzlast.daten) == SkalierteDto(pfad="/roh/a.tiff.klein")
    assert laeufer.laufend == 0


@sync
async def test_nichts_ist_eine_leere_fertige_antwort():
    laeufer, proxy = _laeufer(Skalierer(liefere=lambda e: None))
    await laeufer.nimm(_eingang())
    assert proxy.ergebnisse == [("v1", [], 0, True, "")]


@sync
async def test_ausgabe_ausserhalb_der_signatur_und_ausnahme_werden_zum_fehler():
    laeufer, proxy = _laeufer(Skalierer(liefere=lambda e: FremdDto(x=1)))
    await laeufer.nimm(_eingang("v1"))

    def wirft(e):
        raise IOError("Datei gesperrt")
    laeufer2, proxy2 = _laeufer(Skalierer(liefere=wirft))
    await laeufer2.nimm(_eingang("v2"))

    assert proxy.ergebnisse[0][1] == [] and "FunktionsVerletzung" in proxy.ergebnisse[0][4] and "FremdDto" in proxy.ergebnisse[0][4]
    assert proxy2.ergebnisse[0][1] == [] and "Datei gesperrt" in proxy2.ergebnisse[0][4]


@sync
async def test_ein_strom_geht_stueckweise_mit_stelle_und_endet_fertig():
    laeufer, proxy = _laeufer(Zaehler())
    await laeufer.nimm(_eingang(funktion="IZaehlung"))

    assert [(ab, fertig, [MeldeDto().parse(n.daten).n for _, n in aus]) for _, aus, ab, fertig, _ in proxy.ergebnisse] == \
        [(0, False, [0]), (1, False, [1]), (2, False, [2]), (3, True, [])]
    assert all(art == Ausgabe.COMMAND for _, aus, _, _, _ in proxy.ergebnisse for art, _ in aus)


@sync
async def test_ein_strom_setzt_bei_der_schon_festgehaltenen_stelle_fort():
    laeufer, proxy = _laeufer(Zaehler())
    await laeufer.nimm(_eingang(funktion="IZaehlung", ab=2))   # zwei Stücke hielt ein früherer Träger schon fest

    assert [(ab, fertig) for _, _, ab, fertig, _ in proxy.ergebnisse] == [(2, False), (3, True)]
    assert MeldeDto().parse(proxy.ergebnisse[0][1][0][1].daten).n == 2


class MerkmalBasis(FunktionsBasis):
    """Eine Funktion, die einen Hinweis hört (Plan §2.9) — die generierte Basis trägt hinweis=True."""
    FUNKTION = "ISchaetzung"
    EINGAENGE = {DateiDto: Eingang("auf_datei", {MeldeDto: Ausgabe.COMMAND, SkalierteDto: Ausgabe.HINWEIS}, strom=True, hinweis=True)}


class Schaetzer(MerkmalBasis):
    """Ein Zustand im Worker (A8): er sieht jeden Hinweis seiner Adresse — hier nur gezählt."""
    def __init__(self):
        super().__init__()
        self.gesehen = 0

    async def auf_datei(self, e, h):
        self.gesehen += 1
        yield MeldeDto(n=self.gesehen)
        yield SkalierteDto(pfad=e.pfad)
        yield MeldeDto(n=99)


@sync
async def test_ein_hinweis_als_eingang_antwortet_einmal_als_ganzes_auch_als_strom():
    f = Schaetzer()
    laeufer, proxy = _laeufer(f)
    auftrag = _eingang(funktion="ISchaetzung")
    auftrag.hinweis = True
    await laeufer.nimm(auftrag)

    assert f.gesehen == 1
    [(vorgang, ausgaben, ab, fertig, fehler)] = proxy.ergebnisse
    assert (vorgang, ab, fertig, fehler) == ("v1", 0, True, ""), "nichts ist festgehalten — keine Stücke, keine Stelle"
    assert [art for art, _ in ausgaben] == [Ausgabe.COMMAND, Ausgabe.HINWEIS, Ausgabe.COMMAND], "Commands und Hinweise in ihrer Reihenfolge"


@sync
async def test_die_angabe_hinweis_der_leitung_genuegt_auch_ohne_generierte_angabe():
    class OhneAngabe(StromBasis):
        async def auf_datei(self, e, h):
            yield MeldeDto(n=1)
            yield MeldeDto(n=2)
    laeufer, proxy = _laeufer(OhneAngabe())
    auftrag = _eingang(funktion="IZaehlung")
    auftrag.hinweis = True
    await laeufer.nimm(auftrag)
    assert [(ab, fertig, len(aus)) for _, aus, ab, fertig, _ in proxy.ergebnisse] == [(0, True, 2)]


@sync
async def test_hoechstens_slots_gleichzeitig_und_derselbe_eingang_laeuft_nur_einmal():
    f = Skalierer(slots=2, dauer=0.03)
    laeufer, proxy = _laeufer(f)

    tasks = [laeufer.nimm(_eingang(f"v{i}")) for i in range(5)]
    assert laeufer.nimm(_eingang("v0")) is tasks[0], "erneut zugeteilt → kein zweiter Lauf"
    await laeufer.warte_auf_alle()

    assert f.max_gleichzeitig == 2
    assert sorted(v for v, *_ in proxy.ergebnisse) == [f"v{i}" for i in range(5)]


@sync
async def test_lebenszeichen_waehrend_langer_laeufe_und_danach_keins_mehr():
    laeufer, proxy = _laeufer(Skalierer(dauer=0.08), lebenszeichen_s=0.02)
    await laeufer.nimm(_eingang("lang"))
    n = len(proxy.lebt)
    assert n >= 2 and set(proxy.lebt) == {"lang"}
    await asyncio.sleep(0.06)
    assert len(proxy.lebt) == n, "nach der Antwort kein Lebenszeichen mehr"


@sync
async def test_eingang_fuer_nicht_angebotene_funktion_wird_nicht_gerechnet():
    f = Skalierer()
    laeufer, proxy = _laeufer(f)
    assert laeufer.nimm(_eingang(funktion="IAndere")) is None
    await asyncio.sleep(0)
    assert f.gesehen == [] and proxy.ergebnisse == []


def test_nur_generierte_basen_und_keine_doppelten():
    with pytest.raises(TypeError):
        FunktionsLaeufer([object()], FakeProxy(), PayloadMapper(GEN))
    with pytest.raises(ValueError):
        FunktionsLaeufer([Skalierer(), Skalierer()], FakeProxy(), PayloadMapper(GEN))


@sync
async def test_read_loop_und_verarbeitungsschleife_reichen_den_eingang_an_den_laeufer():
    f = Skalierer()
    client = CqrsClient(registry=SimpleNamespace(), nutzlast=GEN, funktionen=[f])
    proxy = client._proxy
    client._funktionen._proxy = fake = FakeProxy()
    assert client.funktionen.angebote == [("ISkalierung", 2)]

    nachrichten = [ServerMessage(arbeits_auftrag=_eingang("v9")), None]

    class Strom:
        async def recv_message(self):
            await asyncio.sleep(0)
            return nachrichten.pop(0)

    proxy._stream = Strom()
    proxy._connected = True
    lesen = asyncio.ensure_future(proxy.read_loop())
    await asyncio.sleep(0.01)
    proxy._connected = True   # read_loop beendet sich beim Stream-Ende; die Schleife soll die Queue noch leeren
    verarbeiten = asyncio.ensure_future(MessageRouter().process_loop(
        proxy, client.handle, client, None, client._mapper, SimpleNamespace(), None, on_arbeit=client.funktionen.nimm))
    for _ in range(100):
        if fake.ergebnisse:
            break
        await asyncio.sleep(0.01)
    proxy._connected = False
    verarbeiten.cancel()
    await asyncio.gather(lesen, verarbeiten, return_exceptions=True)

    assert [v for v, _, _, _, fehler in fake.ergebnisse if not fehler] == ["v9"]
    assert f.gesehen[0][0].pfad == "/roh/a.tiff"
