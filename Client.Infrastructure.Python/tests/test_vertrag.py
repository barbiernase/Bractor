"""
Client-Verträge (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5) — die Basis, von der die generierten Client-Basen erben.

Ohne Server: eine Client-Basis wird hier so deklariert, wie Cqrs.Codegen sie erzeugt (TRAEGT + HOERT + abstrakte Methoden), dagegen ein Client.
Bewiesen wird: Kenntnis-Dispatch aus HOERT, eine Kenntnis gibt nichts aus, fehlende Methode = kein Start, getragene Funktionen werden mit dem
Zustand des Clients gerufen und angeboten, Handshake mit Client + Hash, Queries nur aus dem Vertrag.
"""

import asyncio
from abc import abstractmethod
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import ClassVar, TypeVar

import betterproto
import pytest

from cqrs_client import Ausgabe, ClientVertragBasis, Eingang, Herkunft, VertragsVerletzung
from cqrs_client.registry import CategoryRegistry
from cqrs_client.router import MessageContext


# ── Fake-Generat (wie betterproto es aus dem Domänen-Proto erzeugt: nur DTOs) ──

@dataclass(eq=False, repr=False)
class GebuchtDto(betterproto.Message):
    betrag: int = betterproto.int32_field(1)


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
GEN = {"Gebucht": GebuchtDto, "Hinweis": HinweisDto, "Buche": BucheDto, "Storniere": StorniereDto, "Konto": KontoDto}

S = TypeVar("S")


# ── so sieht ein generierter Client aus (vgl. domain_client/generated/vertraege.py) ──

class KassenplatzBasis(ClientVertragBasis[S]):
    CLIENT: ClassVar[str] = "Kassenplatz"
    AKTEURE: ClassVar[tuple] = ("Kassierer", "Revisor")
    VERTRAG: ClassVar[str] = "IKassenplatz"
    VERTRAG_HASH: ClassVar[str] = "def456"
    TRAEGT: ClassVar[dict] = {
        "IStornoPruefung": {GebuchtDto: Eingang("storno_pruefung", {StorniereDto: Ausgabe.COMMAND})},
    }
    HOERT: ClassVar[dict] = {HinweisDto: "auf_hinweis"}
    SPONTAN: ClassVar[tuple] = (BucheDto,)
    FRAGT: ClassVar[tuple] = (KontoDto,)

    @abstractmethod
    async def storno_pruefung(self, e, h, state): ...

    @abstractmethod
    async def auf_hinweis(self, e, ctx, state): ...


@dataclass
class KassenState:
    gesehen: int = 0
    hinweise: int = 0


class Kassenplatz(KassenplatzBasis[KassenState]):
    antwort = None   # pro Test: was auf_hinweis ausgibt (eine Kenntnis darf nichts)

    async def storno_pruefung(self, e, h, state):
        state.gesehen += 1
        return StorniereDto(aggregate_id=h.stream) if e.betrag > 100 else None

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

    with pytest.raises(TypeError, match="storno_pruefung"):
        Halb(registry=CategoryRegistry(), nutzlast=GEN)


def test_getragene_funktion_wird_angeboten_und_mit_dem_zustand_gerufen():
    k = _platz()
    assert k.funktionen.angebote == [("IStornoPruefung", 1)]
    [f] = k.funktionen._funktionen.values()
    h = Herkunft(stream="s-1", version=4, zeit=datetime.now(timezone.utc))
    assert asyncio.run(f.rufe(GebuchtDto(betrag=500), h)) == StorniereDto(aggregate_id="s-1")
    assert asyncio.run(f.rufe(GebuchtDto(betrag=5), h)) is None
    assert k.state.gesehen == 2
    assert f.pruefe(f.eingang(GebuchtDto), StorniereDto()) == Ausgabe.COMMAND


def test_handshake_nennt_client_und_hash():
    req = _platz()._build_capabilities_request()
    assert req.vertrag == "Kassenplatz", "am Handshake steht der Client — der Server leitet seine Akteure ab"
    assert req.vertrag_hash == "def456"


def test_ein_client_fragt_nur_was_sein_vertrag_nennt():
    k = _platz()
    with pytest.raises(VertragsVerletzung, match="fragt HinweisDto"):
        asyncio.run(k.query(HinweisDto()))
