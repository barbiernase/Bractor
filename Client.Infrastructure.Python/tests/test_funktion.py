"""
Katalog-Funktionen in Python anbieten (docs/konzept-editor-pipelines.md §14.5) — domänenfrei, mit minimalen betterproto-Fakes.

Bewiesen wird der FunktionsLaeufer des SDK: Anmelden (Funktion + Slots), Auftrag auspacken, `rufe` höchstens SLOTS-mal
gleichzeitig, Lebenszeichen während langer Läufe, Ergebnis-Vertrag (falscher Typ oder Ausnahme → Fehler statt Ergebnis),
Ergebnis als EventEnvelopeDto mit gesetztem oneof — und der Weg vom Read-Loop über die Verarbeitungsschleife zum Läufer.
"""

import asyncio
from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client import Ausfuehrung, FunktionsBasis, FunktionsLaeufer
from cqrs_client.client import CqrsClient
from cqrs_client.mapper import PayloadMapper
from cqrs_client.router import MessageRouter


@dataclass
class SkaliereDto(betterproto.Message):
    pfad: str = betterproto.string_field(1)
    hoehe: int = betterproto.int32_field(2)


@dataclass
class SkaliertDto(betterproto.Message):
    pfad: str = betterproto.string_field(1)


@dataclass
class UnlesbarDto(betterproto.Message):
    grund: str = betterproto.string_field(1)


@dataclass
class FremdDto(betterproto.Message):
    x: int = betterproto.int32_field(1)


@dataclass
class AuftragPayloadDto(betterproto.Message):
    skaliere: SkaliereDto = betterproto.message_field(20, group="payload")


@dataclass
class EventEnvelopeDto(betterproto.Message):
    event_id: str = betterproto.string_field(1)
    aggregate_id: str = betterproto.string_field(2)
    created_at_utc: int = betterproto.int64_field(5)
    causation_id: str = betterproto.string_field(6)
    correlation_id: str = betterproto.string_field(7)
    skaliert: SkaliertDto = betterproto.message_field(20, group="payload")
    unlesbar: UnlesbarDto = betterproto.message_field(21, group="payload")
    fremd: FremdDto = betterproto.message_field(22, group="payload")


@dataclass
class ArbeitsAuftrag(betterproto.Message):
    vorgang: str = betterproto.string_field(1)
    korrelation: str = betterproto.string_field(2)
    funktion: str = betterproto.string_field(3)
    auftrag: AuftragPayloadDto = betterproto.message_field(4)
    akteur: str = betterproto.string_field(5)


@dataclass
class ServerMessage(betterproto.Message):
    arbeits_auftrag: ArbeitsAuftrag = betterproto.message_field(11, group="message")


GEN = SimpleNamespace(
    AuftragPayloadDto=AuftragPayloadDto,
    EventEnvelopeDto=EventEnvelopeDto,
    ClientMessage=object,
    ServerMessage=ServerMessage,
)


class SkalierungBasis(FunktionsBasis):
    """So sieht die generierte Basis aus (domain_client/generated/funktionen.py)."""
    FUNKTION = "ISkalierung"
    AUFTRAG = SkaliereDto
    ERGEBNISSE = (SkaliertDto, UnlesbarDto)


class Skalierer(SkalierungBasis):
    SLOTS = 2

    def __init__(self, slots=None, dauer=0.0, liefere=None):
        super().__init__(slots)
        self.dauer = dauer
        self.liefere = liefere
        self.gleichzeitig = 0
        self.max_gleichzeitig = 0
        self.gesehen: list[tuple[SkaliereDto, Ausfuehrung]] = []

    async def rufe(self, auftrag, x):
        self.gesehen.append((auftrag, x))
        self.gleichzeitig += 1
        self.max_gleichzeitig = max(self.max_gleichzeitig, self.gleichzeitig)
        try:
            await asyncio.sleep(self.dauer)
            if self.liefere is not None:
                return self.liefere(auftrag)
            return SkaliertDto(pfad=auftrag.pfad + ".klein")
        finally:
            self.gleichzeitig -= 1


class FakeProxy:
    def __init__(self):
        self.angebote = []
        self.ergebnisse = []
        self.lebt = []

    async def send_funktionen_anbieten(self, angebote):
        self.angebote.append(list(angebote))

    async def send_arbeits_ergebnis(self, vorgang, ergebnis_envelope=None, fehler=""):
        self.ergebnisse.append((vorgang, ergebnis_envelope, fehler))

    async def send_arbeit_lebt(self, vorgang):
        self.lebt.append(vorgang)


def _auftrag(vorgang="v1", funktion="ISkalierung", pfad="/roh/a.tiff"):
    return ArbeitsAuftrag(vorgang=vorgang, korrelation="k1", funktion=funktion,
                          auftrag=AuftragPayloadDto(skaliere=SkaliereDto(pfad=pfad, hoehe=512)), akteur="KameraSystem")


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
async def test_auftrag_wird_gerufen_und_das_ergebnis_geht_als_event_envelope_zurueck():
    f = Skalierer()
    laeufer, proxy = _laeufer(f)

    await laeufer.nimm(_auftrag())

    auftrag, x = f.gesehen[0]
    assert auftrag == SkaliereDto(pfad="/roh/a.tiff", hoehe=512)
    assert x == Ausfuehrung(ausfuehrungs_id="v1", korrelation="k1", akteur="KameraSystem")
    vorgang, envelope, fehler = proxy.ergebnisse[0]
    assert (vorgang, fehler) == ("v1", "")
    assert betterproto.which_one_of(envelope, "payload") == ("skaliert", SkaliertDto(pfad="/roh/a.tiff.klein"))
    assert envelope.aggregate_id == "v1" and envelope.correlation_id == "k1"
    assert laeufer.laufend == 0


@sync
async def test_ergebnis_ausserhalb_des_vertrags_und_ausnahme_werden_zum_fehler():
    falsch = Skalierer(liefere=lambda a: FremdDto(x=1))
    laeufer, proxy = _laeufer(falsch)
    await laeufer.nimm(_auftrag("v1"))

    def wirft(a):
        raise IOError("Datei gesperrt")
    laeufer2, proxy2 = _laeufer(Skalierer(liefere=wirft))
    await laeufer2.nimm(_auftrag("v2"))

    assert proxy.ergebnisse[0][1] is None and "FunktionsVerletzung" in proxy.ergebnisse[0][2] and "FremdDto" in proxy.ergebnisse[0][2]
    assert proxy2.ergebnisse[0][1] is None and "Datei gesperrt" in proxy2.ergebnisse[0][2]


@sync
async def test_hoechstens_slots_gleichzeitig_und_derselbe_vorgang_laeuft_nur_einmal():
    f = Skalierer(slots=2, dauer=0.03)
    laeufer, proxy = _laeufer(f)

    tasks = [laeufer.nimm(_auftrag(f"v{i}")) for i in range(5)]
    assert laeufer.nimm(_auftrag("v0")) is tasks[0], "erneut zugestellt → kein zweiter Lauf"
    await laeufer.warte_auf_alle()

    assert f.max_gleichzeitig == 2
    assert sorted(v for v, _, _ in proxy.ergebnisse) == [f"v{i}" for i in range(5)]


@sync
async def test_lebenszeichen_waehrend_langer_laeufe_und_danach_keins_mehr():
    laeufer, proxy = _laeufer(Skalierer(dauer=0.08), lebenszeichen_s=0.02)
    await laeufer.nimm(_auftrag("lang"))
    n = len(proxy.lebt)
    assert n >= 2 and set(proxy.lebt) == {"lang"}
    await asyncio.sleep(0.06)
    assert len(proxy.lebt) == n, "nach dem Ergebnis kein Lebenszeichen mehr"


@sync
async def test_auftrag_fuer_nicht_angebotene_funktion_wird_nicht_gerechnet():
    f = Skalierer()
    laeufer, proxy = _laeufer(f)
    assert laeufer.nimm(_auftrag(funktion="IAndere")) is None
    await asyncio.sleep(0)
    assert f.gesehen == [] and proxy.ergebnisse == []


def test_nur_generierte_basen_und_keine_doppelten():
    with pytest.raises(TypeError):
        FunktionsLaeufer([object()], FakeProxy(), PayloadMapper(GEN))
    with pytest.raises(ValueError):
        FunktionsLaeufer([Skalierer(), Skalierer()], FakeProxy(), PayloadMapper(GEN))
    with pytest.raises(TypeError):
        SkalierungBasis()   # abstrakt: ohne rufe nicht instanziierbar


@sync
async def test_read_loop_und_verarbeitungsschleife_reichen_den_auftrag_an_den_laeufer():
    f = Skalierer()
    client = CqrsClient(registry=SimpleNamespace(), generated_module=GEN, funktionen=[f])
    proxy = client._proxy
    client._funktionen._proxy = fake = FakeProxy()
    assert client.funktionen.angebote == [("ISkalierung", 2)]

    nachrichten = [ServerMessage(arbeits_auftrag=_auftrag("v9")), None]

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

    assert [v for v, _, fehler in fake.ergebnisse if not fehler] == ["v9"]
    assert f.gesehen[0][0].pfad == "/roh/a.tiff"
