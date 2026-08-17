"""
M7 — die Ask-Seite: eine Query STELLEN und die typisierte Antwort ENTPACKEN (Konzept §7).

Beweist die zwei neuen Mapper-Bausteine:
  - wrap_query            → setzt das oneof-Feld im QueryPayloadDto (Spiegel zu wrap_command)
  - extract_query_response → entpackt die konkrete Antwort aus QueryResponse.payload

Wie test_mapper_query_response.py: minimale betterproto-Fakes statt des vollen Generats — der
Mapper ist domänenfrei und leitet die type→oneof-Feld-Map allein aus der Envelope-Struktur ab.

Ausführen:  pip install -r requirements-dev.txt && pytest   (aus Client.Infrastructure.Python/)
"""

from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client.mapper import PayloadMapper


# ── Minimale betterproto-Fakes (unquoted Annotations → f.type ist der echte Typ) ──
# Reihenfolge wichtig: referenzierte Typen zuerst definieren.

@dataclass
class FakeQueryDto(betterproto.Message):
    q: str = betterproto.string_field(1)


@dataclass
class FakeAntwortDto(betterproto.Message):
    text: str = betterproto.string_field(1)


@dataclass
class FakeQueryPayloadDto(betterproto.Message):
    fake_query: FakeQueryDto = betterproto.message_field(1, group="payload")


@dataclass
class FakeQueryResponsePayloadDto(betterproto.Message):
    fake_antwort: FakeAntwortDto = betterproto.message_field(1, group="payload")


@dataclass
class FakeQueryResponse(betterproto.Message):
    correlation_id: str = betterproto.string_field(1)
    payload: FakeQueryResponsePayloadDto = betterproto.message_field(2)


@pytest.fixture
def gen():
    return SimpleNamespace(
        QueryPayloadDto=FakeQueryPayloadDto,
        QueryResponsePayloadDto=FakeQueryResponsePayloadDto,
        FakeQueryDto=FakeQueryDto,
        FakeAntwortDto=FakeAntwortDto,
    )


# ═══════════════════════════════════════════════════
# wrap_query — die Query verpacken
# ═══════════════════════════════════════════════════

def test_wrap_query_setzt_das_oneof_feld(gen):
    mapper = PayloadMapper(gen)

    result = mapper.wrap_query(FakeQueryDto(q="hallo"))

    field_name, payload = betterproto.which_one_of(result, "payload")
    assert field_name == "fake_query"     # oneof IST gesetzt (die Ask-Seite)
    assert payload.q == "hallo"


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

    qr = FakeQueryResponse(
        correlation_id="abc",
        payload=FakeQueryResponsePayloadDto(fake_antwort=FakeAntwortDto(text="welt")),
    )

    antwort = mapper.extract_query_response(qr)

    assert isinstance(antwort, FakeAntwortDto)
    assert antwort.text == "welt"


def test_extract_query_response_ohne_oneof_wirft(gen):
    mapper = PayloadMapper(gen)
    qr = FakeQueryResponse(correlation_id="abc", payload=FakeQueryResponsePayloadDto())
    with pytest.raises(ValueError):
        mapper.extract_query_response(qr)


# ═══════════════════════════════════════════════════
# Round-trip: wrap_query_response → extract_query_response (Symmetrie)
# ═══════════════════════════════════════════════════

def test_roundtrip_response_symmetrie(gen):
    mapper = PayloadMapper(gen)

    payload = mapper.wrap_query_response(FakeAntwortDto(text="rund"))
    qr = FakeQueryResponse(correlation_id="x", payload=payload)

    zurueck = mapper.extract_query_response(qr)
    assert isinstance(zurueck, FakeAntwortDto)
    assert zurueck.text == "rund"
