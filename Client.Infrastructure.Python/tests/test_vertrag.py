"""
Client-Verträge (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5) — die Basis, von der die generierten Client-Basen erben.

Ohne Server: eine Client-Basis wird hier so deklariert, wie Cqrs.Codegen sie erzeugt (ARBEIT + HOERT + abstrakte Methoden), dagegen ein Client.
Bewiesen wird: Kenntnis-Dispatch aus HOERT, eine Kenntnis gibt nichts aus, fehlende Methode = kein Start, die Arbeit der Akteure wird mit dem
Zustand des Clients gerufen und angenommen (Plätze je Akteur), die Registry kennt die Typen des Vertrags, Handshake mit Client + Hash, Queries nur
aus dem Vertrag.
"""

import asyncio
from abc import abstractmethod
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import ClassVar, TypeVar

import betterproto
import pytest

from cqrs_client import Arbeit, ArbeitsVerletzung, ClientVertragBasis, Herkunft, VertragsVerletzung
from cqrs_client.registry import MessageCategory
from cqrs_client.registry import CategoryRegistry
from cqrs_client.router import MessageContext


# ── Fake-Generat (wie betterproto es aus dem Domänen-Proto erzeugt: nur DTOs) ──

@dataclass(eq=False, repr=False)
class PruefeStornoDto(betterproto.Message):
    buchung: str = betterproto.string_field(1)
    betrag: int = betterproto.int32_field(2)


@dataclass(eq=False, repr=False)
class HinweisDto(betterproto.Message):
    text: str = betterproto.string_field(1)


@dataclass(eq=False, repr=False)
class BucheDto(betterproto.Message):
    aggregate_id: str = betterproto.string_field(1)


@dataclass(eq=False, repr=False)
class StorniereDto(betterproto.Message):
    aggregate_id: str = betterproto.string_field(1)


@dataclass(eq=False, repr=False)
class KontoDto(betterproto.Message):
    nummer: str = betterproto.string_field(1)


# Die „generierte Nutzlast-Tabelle“. CapabilitiesRequest und Hüllen kommen aus der echten Leitung.
GEN = {"PruefeStorno": PruefeStornoDto, "Hinweis": HinweisDto, "Buche": BucheDto, "Storniere": StorniereDto, "Konto": KontoDto}

S = TypeVar("S")


# ── so sieht ein generierter Client aus (vgl. domain_client/generated/vertraege.py) ──

class KassenplatzBasis(ClientVertragBasis[S]):
    CLIENT: ClassVar[str] = "Kassenplatz"
    AKTEURE: ClassVar[tuple] = ("Kassierer", "Revisor")
    VERTRAG_HASH: ClassVar[str] = "def456"
    ARBEIT: ClassVar[dict] = {
        "Revisor": {PruefeStornoDto: Arbeit("auf_pruefe_storno", (StorniereDto,))},
    }
    HOERT: ClassVar[dict] = {HinweisDto: "auf_hinweis"}
    SPONTAN: ClassVar[tuple] = (BucheDto,)
    FRAGT: ClassVar[tuple] = (KontoDto,)

    @abstractmethod
    async def auf_pruefe_storno(self, c, h, state): ...

    @abstractmethod
    async def auf_hinweis(self, e, ctx, state): ...


@dataclass
class KassenState:
    gesehen: int = 0
    hinweise: int = 0


class Kassenplatz(KassenplatzBasis[KassenState]):
    antwort = None   # pro Test: was auf_hinweis ausgibt (eine Kenntnis darf nichts)

    async def auf_pruefe_storno(self, c, h, state):
        state.gesehen += 1
        return StorniereDto(aggregate_id=c.buchung) if c.betrag > 100 else None

    async def auf_hinweis(self, e, ctx, state):
        state.hinweise += 1
        for x in self.antwort or []:
            yield x


def _platz():
    return Kassenplatz(registry=CategoryRegistry(), nutzlast=GEN)


async def _sammle(k, payload):
    return [x async for x in k.handle.receive(k, payload, MessageContext(), k.state)]


# ── Tests ──

def test_kenntnis_dispatch_kommt_aus_dem_vertrag():
    k = _platz()
    assert k.handle.registered_types == {HinweisDto}
    assert asyncio.run(_sammle(k, HinweisDto(text="x"))) == []
    assert k.state.hinweise == 1, "der State-Typ wird auch über die Vertragsbasis gefunden"


def test_eine_kenntnis_gibt_nichts_aus():
    k = _platz()
    k.antwort = [BucheDto(aggregate_id="1")]
    with pytest.raises(VertragsVerletzung, match="nur Kenntnis"):
        asyncio.run(_sammle(k, HinweisDto()))


def test_fehlende_methode_verhindert_den_start():
    class Halb(KassenplatzBasis[KassenState]):
        async def auf_hinweis(self, e, ctx, state):
            return None

    with pytest.raises(TypeError, match="auf_pruefe_storno"):
        Halb(registry=CategoryRegistry(), nutzlast=GEN)


def test_arbeit_wird_angenommen_und_mit_dem_zustand_gerufen():
    k = _platz()
    assert k.arbeit.angebote == [("Revisor", 1)]
    [a] = k.arbeit._akteure.values()
    h = Herkunft(stream="s-1", version=4, zeit=datetime.now(timezone.utc))
    assert asyncio.run(a.rufe(PruefeStornoDto(buchung="b-1", betrag=500), h)) == StorniereDto(aggregate_id="b-1")
    assert asyncio.run(a.rufe(PruefeStornoDto(buchung="b-1", betrag=5), h)) is None
    assert k.state.gesehen == 2
    arbeit = a.arbeit_fuer(PruefeStornoDto)
    a.pruefe(arbeit, StorniereDto())
    with pytest.raises(ArbeitsVerletzung, match="darf: StorniereDto"):
        a.pruefe(arbeit, BucheDto())


def test_plaetze_je_akteur():
    class Grosser(Kassenplatz):
        PLAETZE = {"Revisor": 3}

    assert Grosser(registry=CategoryRegistry(), nutzlast=GEN).arbeit.angebote == [("Revisor", 3)]


def test_die_registry_kennt_die_typen_des_vertrags():
    r = _platz()._registry
    assert r.classify(HinweisDto) == MessageCategory.EVENT
    assert {r.classify(t) for t in (BucheDto, PruefeStornoDto, StorniereDto)} == {MessageCategory.COMMAND}, \
        "von sich aus, gehörte Arbeit und ihre Antworten"
    assert r.classify(KontoDto) == MessageCategory.QUERY


def test_handshake_nennt_client_und_hash():
    req = _platz()._build_capabilities_request()
    assert req.vertrag == "Kassenplatz", "am Handshake steht der Client — der Server leitet seine Akteure ab"
    assert req.vertrag_hash == "def456"


def test_ein_client_fragt_nur_was_sein_vertrag_nennt():
    k = _platz()
    with pytest.raises(VertragsVerletzung, match="fragt HinweisDto"):
        asyncio.run(k.query(HinweisDto()))
