"""
Grundgerüst für die Python-SDK-Tests.

Beweist die Query-Antwort: PayloadMapper.wrap_query_response verpackt die Antwort als Nutzlast (typ = Proto-Name
ohne „Dto“, daten = protobuf-Bytes) für QueryResponseFromClient.payload.

Nutzt minimale betterproto-Fake-Typen statt des vollen Generats — der Mapper ist domänenfrei und baut die Tabelle
typ → DTO-Klasse allein aus dem übergebenen Modul (jede betterproto-Message, deren Name auf „Dto“ endet).

Ausführen:  pip install -r requirements-dev.txt && pytest   (aus Client.Infrastructure.Python/)
"""

from dataclasses import dataclass

import betterproto
import pytest

from cqrs_client.leitung import Nutzlast
from cqrs_client.mapper import PayloadMapper


@dataclass
class FakeAntwortDto(betterproto.Message):
    text: str = betterproto.string_field(1)


@pytest.fixture
def gen():
    # Minimale „generierte Nutzlast-Tabelle“.
    return {"FakeAntwort": FakeAntwortDto}


def test_wrap_query_response_verpackt_typ_und_bytes(gen):
    mapper = PayloadMapper(gen)

    result = mapper.wrap_query_response(FakeAntwortDto(text="hallo"))

    assert isinstance(result, Nutzlast)
    assert result.typ == "FakeAntwort"                       # Proto-Name ohne „Dto“
    assert FakeAntwortDto().parse(result.daten).text == "hallo"   # und trägt die echte Response


def test_nutzlast_uebersteht_den_draht(gen):
    mapper = PayloadMapper(gen)
    gesendet = bytes(mapper.wrap_query_response(FakeAntwortDto(text="draht")))
    assert mapper.entpacke(Nutzlast().parse(gesendet)) == FakeAntwortDto(text="draht")


def test_wrap_query_response_unbekannter_typ_wirft(gen):
    @dataclass
    class UnbekanntDto(betterproto.Message):
        x: int = betterproto.int32_field(1)

    mapper = PayloadMapper(gen)

    with pytest.raises(TypeError, match="UnbekanntDto"):
        mapper.wrap_query_response(UnbekanntDto(x=1))
