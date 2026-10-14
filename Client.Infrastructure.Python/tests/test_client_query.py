"""
Die öffentliche Ask-API: CqrsClient.query() (docs/09 §9.4).

Beweist die Komposition wrap_query (Nutzlast) → proxy.send_query → extract_query_response, ohne echten
Server: der Proxy-Send wird durch einen Fake ersetzt, der einen QueryResponse zurückgibt.
"""

import asyncio
from dataclasses import dataclass
from types import SimpleNamespace

import betterproto

from cqrs_client.client import CqrsClient
from cqrs_client.leitung import Nutzlast, QueryResponse


@dataclass
class FakeQueryDto(betterproto.Message):
    q: str = betterproto.string_field(1)


@dataclass
class FakeAntwortDto(betterproto.Message):
    text: str = betterproto.string_field(1)


class FakeClient(CqrsClient):   # kein [State] → State bleibt None
    pass


def _gen():
    return SimpleNamespace(FakeQueryDto=FakeQueryDto, FakeAntwortDto=FakeAntwortDto)


def test_query_liefert_typisierte_antwort():
    client = FakeClient(registry=SimpleNamespace(), generated_module=_gen())

    empfangen = {}

    async def fake_send_query(nutzlast, correlation_id=None):
        # Der Client hat die Query korrekt als Nutzlast verpackt?
        empfangen["typ"] = nutzlast.typ
        empfangen["q"] = FakeQueryDto().parse(nutzlast.daten).q
        # Server antwortet
        return QueryResponse(
            correlation_id="x",
            payload=Nutzlast(typ="FakeAntwort", daten=bytes(FakeAntwortDto(text="ok"))),
        )

    client._proxy.send_query = fake_send_query

    antwort = asyncio.run(client.query(FakeQueryDto(q="hallo")))

    assert empfangen["typ"] == "FakeQuery"      # richtig verpackt gesendet
    assert empfangen["q"] == "hallo"
    assert isinstance(antwort, FakeAntwortDto)  # typisiert zurück
    assert antwort.text == "ok"
