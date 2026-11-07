"""
Client-Verträge (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5) — die Basis, von der die generierten Client-Basen erben.

Ohne Server: eine Client-Basis wird hier so deklariert, wie Cqrs.Codegen sie erzeugt (HOERT + abstrakte Methoden), dagegen ein Client.
Bewiesen wird: Dispatch aus HOERT, eine Reaktion gibt nur Commands des IDarf aus, fehlende Methode = kein Start, die Registry kennt die Typen des
Vertrags, Handshake mit Client + Hash, Queries nur aus dem Vertrag.
"""

import asyncio
from abc import abstractmethod
from dataclasses import dataclass
from typing import ClassVar, TypeVar

import betterproto
import pytest

from cqrs_client import ClientVertragBasis, VertragsVerletzung
from cqrs_client.registry import MessageCategory
from cqrs_client.registry import CategoryRegistry
from cqrs_client.router import MessageContext


# ── Fake-Generat (wie betterproto es aus dem Domänen-Proto erzeugt: nur DTOs) ──

@dataclass(eq=False, repr=False)
class StornoGemeldetDto(betterproto.Message):
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
GEN = {"StornoGemeldet": StornoGemeldetDto, "Hinweis": HinweisDto, "Buche": BucheDto, "Storniere": StorniereDto, "Konto": KontoDto}

S = TypeVar("S")


# ── so sieht ein generierter Client aus (vgl. domain_client/generated/vertraege.py) ──

class KassenplatzBasis(ClientVertragBasis[S]):
    CLIENT: ClassVar[str] = "Kassenplatz"
    AKTEURE: ClassVar[tuple] = ("Kassierer", "Revisor")
    VERTRAG_HASH: ClassVar[str] = "def456"
    HOERT: ClassVar[dict] = {HinweisDto: "auf_hinweis", StornoGemeldetDto: "auf_storno_gemeldet"}
    SPONTAN: ClassVar[tuple] = (BucheDto, StorniereDto)
    FRAGT: ClassVar[tuple] = (KontoDto,)

    @abstractmethod
    async def auf_storno_gemeldet(self, e, ctx, state): ...

    @abstractmethod
    async def auf_hinweis(self, e, ctx, state): ...


@dataclass
class KassenState:
    gesehen: int = 0
    hinweise: int = 0


class Kassenplatz(KassenplatzBasis[KassenState]):
    antwort = None   # pro Test: was auf_hinweis ausgibt (seine Reaktion: nur Commands aus SPONTAN)

    async def auf_storno_gemeldet(self, e, ctx, state):
        state.gesehen += 1
        return StorniereDto(aggregate_id=e.buchung) if e.betrag > 100 else None

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
    assert k.handle.registered_types == {HinweisDto, StornoGemeldetDto}
    assert asyncio.run(_sammle(k, HinweisDto(text="x"))) == []
    assert k.state.hinweise == 1, "der State-Typ wird auch über die Vertragsbasis gefunden"


def test_eine_reaktion_gibt_commands_des_idarf_aus():
    k = _platz()
    k.antwort = [BucheDto(aggregate_id="1"), BucheDto(aggregate_id="2")]
    assert asyncio.run(_sammle(k, HinweisDto())) == [BucheDto(aggregate_id="1"), BucheDto(aggregate_id="2")]


def test_eine_reaktion_gibt_nichts_ausserhalb_des_idarf_aus():
    k = _platz()
    k.antwort = [KontoDto(nummer="1")]
    with pytest.raises(VertragsVerletzung, match="nur mit Commands seiner Akteure"):
        asyncio.run(_sammle(k, HinweisDto()))


def test_fehlende_methode_verhindert_den_start():
    class Halb(KassenplatzBasis[KassenState]):
        async def auf_hinweis(self, e, ctx, state):
            return None

    with pytest.raises(TypeError, match="auf_storno_gemeldet"):
        Halb(registry=CategoryRegistry(), nutzlast=GEN)


def test_eine_reaktion_rechnet_mit_dem_zustand_des_clients():
    k = _platz()
    assert asyncio.run(_sammle(k, StornoGemeldetDto(buchung="b-1", betrag=500))) == [StorniereDto(aggregate_id="b-1")]
    assert asyncio.run(_sammle(k, StornoGemeldetDto(buchung="b-1", betrag=5))) == []
    assert k.state.gesehen == 2


def test_die_registry_kennt_die_typen_des_vertrags():
    r = _platz()._registry
    assert {r.classify(t) for t in (HinweisDto, StornoGemeldetDto)} == {MessageCategory.EVENT}
    assert {r.classify(t) for t in (BucheDto, StorniereDto)} == {MessageCategory.COMMAND}, "was die Akteure hineingeben"
    assert r.classify(KontoDto) == MessageCategory.QUERY


def test_handshake_nennt_client_und_hash():
    req = _platz()._build_capabilities_request()
    assert req.vertrag == "Kassenplatz", "am Handshake steht der Client — der Server leitet seine Akteure ab"
    assert req.vertrag_hash == "def456"


def test_ein_client_fragt_nur_was_sein_vertrag_nennt():
    k = _platz()
    with pytest.raises(VertragsVerletzung, match="fragt HinweisDto"):
        asyncio.run(k.query(HinweisDto()))
