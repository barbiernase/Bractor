"""
M7 — die öffentliche Ask-API: CqrsClient.query() (Konzept §7.3).

Beweist die Komposition wrap_query → proxy.send_query → extract_query_response, ohne echten
Server: der Proxy-Send wird durch einen Fake ersetzt, der einen QueryResponse zurückgibt.
"""

import asyncio
from dataclasses import dataclass
from types import SimpleNamespace

import betterproto

from cqrs_client.client import CqrsClient


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


class FakeClient(CqrsClient):   # kein [State] → State bleibt None
    pass


def _gen():
    return SimpleNamespace(
        ClientMessage=object,
        ServerMessage=object,
        QueryPayloadDto=FakeQueryPayloadDto,
        QueryResponsePayloadDto=FakeQueryResponsePayloadDto,
    )


def test_query_liefert_typisierte_antwort():
    client = FakeClient(registry=SimpleNamespace(), generated_module=_gen())

    empfangen = {}

    async def fake_send_query(payload, correlation_id=None):
        # Der Client hat die Query korrekt ins oneof verpackt?
        field_name, inner = betterproto.which_one_of(payload, "payload")
        empfangen["field"] = field_name
        empfangen["q"] = inner.q
        # Server antwortet
        return FakeQueryResponse(
            correlation_id="x",
            payload=FakeQueryResponsePayloadDto(fake_antwort=FakeAntwortDto(text="ok")),
        )

    client._proxy.send_query = fake_send_query

    antwort = asyncio.run(client.query(FakeQueryDto(q="hallo")))

    assert empfangen["field"] == "fake_query"   # richtig verpackt gesendet
    assert empfangen["q"] == "hallo"
    assert isinstance(antwort, FakeAntwortDto)  # typisiert zurück
    assert antwort.text == "ok"
