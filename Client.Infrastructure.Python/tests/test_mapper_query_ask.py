"""
Die Ask-Seite: eine Query STELLEN und die typisierte Antwort ENTPACKEN (docs/09 §9.4).

Beweist die zwei Mapper-Bausteine:
  - wrap_query            → Nutzlast(typ, daten) für QueryRequest.payload (Spiegel zu wrap_command)
  - extract_query_response → entpackt die konkrete Antwort aus QueryResponse.payload (Nutzlast → DTO nach typ)

Wie test_mapper_query_response.py: minimale betterproto-Fakes statt des vollen Generats — die Leitung
(QueryResponse, Nutzlast) ist die echte aus cqrs_client.leitung.

Ausführen:  pip install -r requirements-dev.txt && pytest   (aus Client.Infrastructure.Python/)
"""

from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client.leitung import Nutzlast, QueryResponse
from cqrs_client.mapper import PayloadMapper, UnbekannteNutzlast


@dataclass
class FakeQueryDto(betterproto.Message):
    q: str = betterproto.string_field(1)


@dataclass
class FakeAntwortDto(betterproto.Message):
    text: str = betterproto.string_field(1)


@pytest.fixture
def gen():
    return SimpleNamespace(FakeQueryDto=FakeQueryDto, FakeAntwortDto=FakeAntwortDto)


# ═══════════════════════════════════════════════════
# wrap_query — die Query verpacken
# ═══════════════════════════════════════════════════

def test_wrap_query_verpackt_typ_und_bytes(gen):
    mapper = PayloadMapper(gen)

    result = mapper.wrap_query(FakeQueryDto(q="hallo"))

    assert result.typ == "FakeQuery"
    assert FakeQueryDto().parse(result.daten).q == "hallo"


def test_wrap_query_unbekannter_typ_wirft(gen):
    @dataclass
    class UnbekanntDto(betterproto.Message):
        x: int = betterproto.int32_field(1)

    mapper = PayloadMapper(gen)
    with pytest.raises(TypeError):
        mapper.wrap_query(UnbekanntDto(x=1))


# ═══════════════════════════════════════════════════
# extract_query_response — die Antwort entpacken
# ═══════════════════════════════════════════════════

def test_extract_query_response_entpackt_die_typisierte_antwort(gen):
    mapper = PayloadMapper(gen)

    qr = QueryResponse(
        correlation_id="abc",
        payload=Nutzlast(typ="FakeAntwort", daten=bytes(FakeAntwortDto(text="welt"))),
    )

    antwort = mapper.extract_query_response(qr)

    assert isinstance(antwort, FakeAntwortDto)
    assert antwort.text == "welt"


def test_extract_query_response_ohne_nutzlast_wirft(gen):
    mapper = PayloadMapper(gen)
    qr = QueryResponse(correlation_id="abc")
    with pytest.raises(ValueError):
        mapper.extract_query_response(qr)


def test_extract_query_response_unbekannter_typ_wirft_klar(gen):
    mapper = PayloadMapper(gen)
    qr = QueryResponse(correlation_id="abc", payload=Nutzlast(typ="GibtEsNicht", daten=b""))
    with pytest.raises(UnbekannteNutzlast, match="GibtEsNicht"):
        mapper.extract_query_response(qr)


# ═══════════════════════════════════════════════════
# Round-trip: wrap_query_response → extract_query_response (Symmetrie)
# ═══════════════════════════════════════════════════

def test_roundtrip_response_symmetrie(gen):
    mapper = PayloadMapper(gen)

    nutzlast = mapper.wrap_query_response(FakeAntwortDto(text="rund"))
    qr = QueryResponse().parse(bytes(QueryResponse(correlation_id="x", payload=nutzlast)))   # über den Draht

    zurueck = mapper.extract_query_response(qr)
    assert isinstance(zurueck, FakeAntwortDto)
    assert zurueck.text == "rund"
