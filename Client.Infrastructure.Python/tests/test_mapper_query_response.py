"""
T3 — Grundgerüst für die Python-SDK-Tests.

Beweist die geschlossene Query-Antwort-Lücke: PayloadMapper.wrap_query_response setzt das
richtige oneof-Feld im QueryResponsePayloadDto (vorher: leerer Payload, oneof nie gesetzt).

Nutzt minimale betterproto-Fake-Typen statt des vollen Generats — der Mapper ist domänenfrei
und leitet die type→oneof-Feld-Map allein aus der Struktur des Envelope-Typs ab.

Ausführen:  pip install -r requirements-dev.txt && pytest   (aus Client.Infrastructure.Python/)
"""

from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client.mapper import PayloadMapper


# ── Minimale betterproto-Fakes (unquoted Annotations → f.type ist der echte Typ) ──

@dataclass
class FakeAntwortDto(betterproto.Message):
    text: str = betterproto.string_field(1)


@dataclass
class FakeQueryResponsePayloadDto(betterproto.Message):
    fake_antwort: FakeAntwortDto = betterproto.message_field(1, group="payload")


@pytest.fixture
def gen():
    # Minimales "generiertes Modul": nur die für die Query-Response nötigen Typen.
    # Command-/Event-Envelope fehlen bewusst → der Mapper degradiert dort zu {} (getattr-None).
    return SimpleNamespace(
        QueryResponsePayloadDto=FakeQueryResponsePayloadDto,
        FakeAntwortDto=FakeAntwortDto,
    )


def test_wrap_query_response_setzt_das_oneof_feld(gen):
    mapper = PayloadMapper(gen)

    result = mapper.wrap_query_response(FakeAntwortDto(text="hallo"))

    field_name, payload = betterproto.which_one_of(result, "payload")
    assert field_name == "fake_antwort"      # oneof IST gesetzt (die geschlossene Lücke)
    assert payload.text == "hallo"           # und trägt die echte Response


def test_wrap_query_response_unbekannter_typ_wirft(gen):
    @dataclass
    class UnbekanntDto(betterproto.Message):
        x: int = betterproto.int32_field(1)

    mapper = PayloadMapper(gen)

    with pytest.raises(TypeError):
        mapper.wrap_query_response(UnbekanntDto(x=1))
