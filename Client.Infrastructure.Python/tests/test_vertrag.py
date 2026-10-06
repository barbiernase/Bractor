"""
Akteur-Verträge (docs/konzept-akteure.md §3) — die Basis, von der die generierten Vertragsklassen erben.

Ohne Server: eine Vertragsklasse wird hier so deklariert, wie Cqrs.Codegen sie erzeugt (ZUSAGEN + abstrakte
auf_…-Methoden), dagegen ein Worker. Bewiesen wird: Dispatch aus dem Vertrag, fehlende Methode = kein Start,
jede Ausgabe gegen den Vertrag geprüft, Handshake mit Vertrag + Hash, Kausalität je Ausgabe im CommandEnvelope.
"""

import asyncio
from abc import abstractmethod
from dataclasses import dataclass
from types import SimpleNamespace
from typing import ClassVar, TypeVar

import betterproto
import pytest

from cqrs_client import AkteurVertragBasis, Zusage, VertragsVerletzung
from cqrs_client.registry import CategoryRegistry
from cqrs_client.router import MessageContext, MessageRouter


# ── Fake-Generat (wie betterproto es aus domain.proto erzeugt) ──

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
class CapabilitiesRequest(betterproto.Message):
    event_types: list = betterproto.string_field(1)
    message_types: list = betterproto.string_field(2)
    handle_triggers: list = betterproto.string_field(3)
    handle_queries: list = betterproto.string_field(4)
    vertrag: str = betterproto.string_field(5)
    vertrag_hash: str = betterproto.string_field(6)


@dataclass(eq=False, repr=False)
class CommandEnvelopeDto(betterproto.Message):
    command_id: str = betterproto.string_field(1)
    aggregate_id: str = betterproto.string_field(2)
    aggregate_type: str = betterproto.string_field(3)
    expected_version: int = betterproto.int32_field(4)
    created_at_utc: int = betterproto.int64_field(5)
    correlation_id: str = betterproto.string_field(6)
    user_id: str = betterproto.string_field(7)
    origin_session_id: str = betterproto.string_field(8)
    causation_stream_id: str = betterproto.string_field(9)
    causation_version: int = betterproto.int32_field(10)
    causation_type: str = betterproto.string_field(11)
    causation_index: int = betterproto.int32_field(12)
    buche: BucheDto = betterproto.message_field(20, group="payload")
    storniere: StorniereDto = betterproto.message_field(21, group="payload")


@dataclass(eq=False, repr=False)
class EventEnvelopeDto(betterproto.Message):
    event_id: str = betterproto.string_field(1)
    aggregate_id: str = betterproto.string_field(2)
    aggregate_type: str = betterproto.string_field(3)
    aggregate_version: int = betterproto.int32_field(4)
    correlation_id: str = betterproto.string_field(7)
    gebucht: GebuchtDto = betterproto.message_field(20, group="payload")
    hinweis: HinweisDto = betterproto.message_field(21, group="payload")


GEN = SimpleNamespace(ClientMessage=object, ServerMessage=object, CapabilitiesRequest=CapabilitiesRequest,
                      CommandEnvelopeDto=CommandEnvelopeDto, EventEnvelopeDto=EventEnvelopeDto)

S = TypeVar("S")


# ── so sieht ein generierter Vertrag aus (vgl. domain_client/generated/vertraege.py) ──

class KassiererBasis(AkteurVertragBasis[S]):
    AKTEUR: ClassVar[str] = "Kassierer"
    VERTRAG: ClassVar[str] = "IKassierer"
    VERTRAG_HASH: ClassVar[str] = "abc123"
    ZUSAGEN: ClassVar[dict] = {
        GebuchtDto: Zusage("auf_gebucht", (StorniereDto,)),
        HinweisDto: Zusage("auf_hinweis", (BucheDto, StorniereDto), strom=True),
    }
    SPONTAN: ClassVar[tuple] = (BucheDto,)

    @abstractmethod
    async def auf_gebucht(self, e, ctx, state): ...

    @abstractmethod
    async def auf_hinweis(self, e, ctx, state): ...


@dataclass
class KassenState:
    gesehen: int = 0


class Kasse(KassiererBasis[KassenState]):
    antwort = None   # pro Test: was auf_gebucht ausgibt

    async def auf_gebucht(self, e, ctx, state):
        state.gesehen += 1
        for x in self.antwort or []:
            yield x

    async def auf_hinweis(self, e, ctx, state):
        for i in range(3):
            yield StorniereDto(aggregate_id=f"00000000-0000-0000-0000-00000000000{i}")


def _kasse():
    return Kasse(registry=CategoryRegistry(), generated_module=GEN)


async def _sammle(k, payload):
    return [x async for x in k.handle.receive(k, payload, MessageContext(), k.state)]


# ── Tests ──

def test_dispatch_kommt_aus_dem_vertrag():
    k = _kasse()
    assert k.handle.registered_types == {GebuchtDto, HinweisDto}
    k.antwort = [StorniereDto(aggregate_id="1")]
    aus = asyncio.run(_sammle(k, GebuchtDto(betrag=5)))
    assert [type(x) for x in aus] == [StorniereDto]
    assert k.state.gesehen == 1, "der State-Typ wird auch über die Vertragsbasis gefunden"


def test_fehlende_zusage_verhindert_den_start():
    class Halb(KassiererBasis[KassenState]):
        async def auf_gebucht(self, e, ctx, state):
            return None

    with pytest.raises(TypeError, match="auf_hinweis"):
        Halb(registry=CategoryRegistry(), generated_module=GEN)


def test_falscher_typ_ist_eine_vertragsverletzung():
    k = _kasse()
    k.antwort = [BucheDto(aggregate_id="1")]   # BucheDto darf er spontan — aber nicht als Antwort auf Gebucht
    with pytest.raises(VertragsVerletzung, match="BucheDto"):
        asyncio.run(_sammle(k, GebuchtDto()))


def test_ohne_strom_hoechstens_eine_ausgabe_mit_strom_beliebig_viele():
    k = _kasse()
    k.antwort = [StorniereDto(aggregate_id="1"), StorniereDto(aggregate_id="2")]
    with pytest.raises(VertragsVerletzung, match="kein Strom"):
        asyncio.run(_sammle(k, GebuchtDto()))
    assert len(asyncio.run(_sammle(k, HinweisDto()))) == 3


def test_handshake_nennt_vertrag_hash_und_vertragstypen():
    req = _kasse()._build_capabilities_request()
    assert req.vertrag == "Kassierer" and req.vertrag_hash == "abc123"
    assert set(req.message_types) == {"Gebucht", "Hinweis", "Storniere", "Buche"}


def test_freie_registrierung_derselben_zusage_ist_ein_fehler():
    from cqrs_client import handle
    with pytest.raises(TypeError, match="Doppelte Registrierung"):
        class Doppelt(KassiererBasis[KassenState]):
            async def auf_gebucht(self, e, ctx, state): ...
            async def auf_hinweis(self, e, ctx, state): ...

            @handle.register
            async def extra(self, e: GebuchtDto, ctx, state): ...


def test_jede_ausgabe_traegt_ihre_kausalitaet():
    k = _kasse()
    gesendet = []

    class FakeProxy:
        session_id = "session-0001"

        async def send_command(self, env):
            gesendet.append(env)

    env = EventEnvelopeDto(aggregate_id="11111111-1111-1111-1111-111111111111", aggregate_version=7,
                           correlation_id="22222222-2222-2222-2222-222222222222", hinweis=HinweisDto(text="x"))
    asyncio.run(MessageRouter()._handle_event(SimpleNamespace(envelope=env), FakeProxy(), k.handle, k, k.state,
                                              k._mapper, k._registry, k._version_tracker))
    assert [e.causation_index for e in gesendet] == [0, 1, 2]
    assert {(e.causation_stream_id, e.causation_version, e.causation_type) for e in gesendet} == \
        {("11111111-1111-1111-1111-111111111111", 7, "Hinweis")}
    assert all(e.correlation_id == "22222222-2222-2222-2222-222222222222" for e in gesendet), "die Korrelation reist mit"
    assert all(e.expected_version == -1 for e in gesendet), "Zusagen gehen im Emittiert-Modus"


# ── Client-Vertrag (docs/konzept-akteure.md §4): EIN Client, ZWEI Akteure, eine Verbindung ──

class KassenplatzBasis(AkteurVertragBasis[S]):
    """So erzeugt Cqrs.Codegen einen Client: CLIENT + AKTEURE, je Zusage der Akteur."""
    CLIENT: ClassVar[str] = "Kassenplatz"
    AKTEURE: ClassVar[tuple] = ("Kassierer", "Revisor")
    VERTRAG: ClassVar[str] = "IKassenplatz"
    VERTRAG_HASH: ClassVar[str] = "def456"
    ZUSAGEN: ClassVar[dict] = {
        GebuchtDto: Zusage("auf_gebucht", (StorniereDto,), akteur="Revisor"),
        HinweisDto: Zusage("auf_hinweis", (BucheDto,), akteur="Kassierer"),
    }
    SPONTAN: ClassVar[tuple] = ()
    FRAGT: ClassVar[tuple] = (GebuchtDto,)   # hier nur als Typ-Platzhalter für die Query-Prüfung

    @abstractmethod
    async def auf_gebucht(self, e, ctx, state): ...

    @abstractmethod
    async def auf_hinweis(self, e, ctx, state): ...


class Kassenplatz(KassenplatzBasis[KassenState]):
    async def auf_gebucht(self, e, ctx, state):
        yield StorniereDto(aggregate_id="1")

    async def auf_hinweis(self, e, ctx, state):
        yield BucheDto(aggregate_id="2")


def test_ein_client_traegt_zwei_akteure_ueber_eine_verbindung():
    k = Kassenplatz(registry=CategoryRegistry(), generated_module=GEN)
    req = k._build_capabilities_request()
    assert req.vertrag == "Kassenplatz", "am Handshake steht der Client — der Server leitet seine Akteure ab"
    assert req.vertrag_hash == "def456"
    assert set(req.message_types) == {"Gebucht", "Hinweis", "Storniere", "Buche"}
    assert [type(x) for x in asyncio.run(_sammle(k, GebuchtDto()))] == [StorniereDto]
    assert [type(x) for x in asyncio.run(_sammle(k, HinweisDto()))] == [BucheDto]
    assert {r.akteur for r in k.ZUSAGEN_ALLE.values()} == {"Kassierer", "Revisor"}


def test_ein_client_fragt_nur_was_sein_vertrag_nennt():
    k = Kassenplatz(registry=CategoryRegistry(), generated_module=GEN)
    with pytest.raises(VertragsVerletzung, match="fragt HinweisDto"):
        asyncio.run(k.query(HinweisDto()))
