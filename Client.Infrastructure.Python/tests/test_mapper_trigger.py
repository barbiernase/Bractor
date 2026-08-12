"""
Client→Client-Trigger — PayloadMapper.wrap_trigger setzt das oneof-Feld im TriggerPayloadDto,
damit ein Handler-yieldeter Trigger tatsächlich gesendet wird (vorher: nur Warnung).
Struktur wie test_mapper_query_response (minimale betterproto-Fakes, domänenfreier Mapper).
"""

from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client.mapper import PayloadMapper


@dataclass
class FakeTriggerDto(betterproto.Message):
    ref: str = betterproto.string_field(1)


@dataclass
class FakeTriggerPayloadDto(betterproto.Message):
    fake_trigger: FakeTriggerDto = betterproto.message_field(1, group="payload")


@pytest.fixture
def gen():
    return SimpleNamespace(
        TriggerPayloadDto=FakeTriggerPayloadDto,
        FakeTriggerDto=FakeTriggerDto,
    )


def test_wrap_trigger_setzt_das_oneof_feld(gen):
    mapper = PayloadMapper(gen)

    result = mapper.wrap_trigger(FakeTriggerDto(ref="abc"))

    field_name, payload = betterproto.which_one_of(result, "payload")
    assert field_name == "fake_trigger"
    assert payload.ref == "abc"


def test_wrap_trigger_unbekannter_typ_wirft(gen):
    @dataclass
    class UnbekanntDto(betterproto.Message):
        x: int = betterproto.int32_field(1)

    mapper = PayloadMapper(gen)

    with pytest.raises(TypeError):
        mapper.wrap_trigger(UnbekanntDto(x=1))
