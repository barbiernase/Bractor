"""
Client→Client-Trigger — PayloadMapper.wrap_trigger verpackt den Trigger als Nutzlast für TriggerRequest.payload,
damit ein Handler-yieldeter Trigger tatsächlich gesendet wird; extract_trigger_payload packt TriggerForward.payload aus.
Struktur wie test_mapper_query_response (minimale betterproto-Fakes, domänenfreier Mapper).
"""

from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client.leitung import TriggerForward
from cqrs_client.mapper import PayloadMapper


@dataclass
class FakeTriggerDto(betterproto.Message):
    ref: str = betterproto.string_field(1)


@pytest.fixture
def gen():
    return SimpleNamespace(FakeTriggerDto=FakeTriggerDto)


def test_wrap_trigger_verpackt_typ_und_bytes(gen):
    mapper = PayloadMapper(gen)

    result = mapper.wrap_trigger(FakeTriggerDto(ref="abc"))

    assert result.typ == "FakeTrigger"
    assert FakeTriggerDto().parse(result.daten).ref == "abc"


def test_trigger_forward_wird_ausgepackt(gen):
    mapper = PayloadMapper(gen)
    fwd = TriggerForward(payload=mapper.wrap_trigger(FakeTriggerDto(ref="hin")), correlation_id="c")
    fwd = TriggerForward().parse(bytes(fwd))
    assert mapper.extract_trigger_payload(fwd.payload) == FakeTriggerDto(ref="hin")


def test_wrap_trigger_unbekannter_typ_wirft(gen):
    @dataclass
    class UnbekanntDto(betterproto.Message):
        x: int = betterproto.int32_field(1)

    mapper = PayloadMapper(gen)

    with pytest.raises(TypeError):
        mapper.wrap_trigger(UnbekanntDto(x=1))
