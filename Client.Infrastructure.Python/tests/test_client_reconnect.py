"""
Reconnect: nach einem Verbindungsabbruch verbindet der Client neu, bietet seine Funktionen wieder an UND verarbeitet weiter.

Regression (docs/konzept-editor-pipelines.md §14.9): früher endeten Lese- und Verarbeitungsschleife mit der ersten Verbindung;
der Monitor verband zwar neu und meldete die Funktionen erneut an, aber niemand las mehr — der Worker war taub, seine Aufträge
liefen nur noch per Lease-Ablauf zu anderen. Ohne Server: der Proxy ist ein Fake, der je Verbindung genau einen Auftrag liefert
und dann die Verbindung verliert.
"""

import asyncio
from types import SimpleNamespace

from cqrs_client.client import CqrsClient


class FakeClient(CqrsClient):   # kein [State] → State bleibt None
    pass


class FakeProxy:
    """Je connect() eine Sitzung: ein ArbeitsAuftrag in die Queue, dann Verbindungsabbruch."""

    def __init__(self, sitzungen: int):
        self.event_queue: asyncio.Queue = asyncio.Queue()
        self.disconnected = asyncio.Event()
        self.verbindungen = 0
        self._sitzungen = sitzungen
        self._connected = False
        self.session_id = ""

    @property
    def is_connected(self) -> bool:
        return self._connected

    async def connect(self, host, port, capabilities_request):
        self.verbindungen += 1
        self._connected = True
        self.disconnected.clear()
        self.session_id = f"s{self.verbindungen}"
        return SimpleNamespace(session_id=self.session_id)

    async def read_loop(self):
        await self.event_queue.put(("arbeits_auftrag", SimpleNamespace(vorgang=f"v{self.verbindungen}")))
        await asyncio.sleep(0.05)
        if self.verbindungen >= self._sitzungen:
            await asyncio.Event().wait()   # letzte Sitzung bleibt offen, bis der Test abbricht
        self._connected = False            # Server schließt den Stream
        self.disconnected.set()

    async def disconnect(self):
        self._connected = False
        self.disconnected.set()


def test_nach_reconnect_wird_weiter_verarbeitet_und_neu_angeboten():
    client = FakeClient(registry=SimpleNamespace(), generated_module=SimpleNamespace(ClientMessage=object, ServerMessage=object))
    proxy = FakeProxy(sitzungen=3)
    client._proxy = proxy
    client._connection._proxy = proxy
    client._connection._backoff_base = 0.01

    angeboten, genommen = [], []

    async def biete_an(_antwort=None):
        angeboten.append(proxy.session_id)

    client._funktionen.biete_an = biete_an
    client._funktionen.nimm = lambda msg: genommen.append(msg.vorgang)
    client._build_capabilities_request = lambda: object()

    async def lauf():
        task = asyncio.ensure_future(client._run_async("localhost", 1))
        for _ in range(200):
            if len(genommen) >= 3:
                break
            await asyncio.sleep(0.02)
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass

    asyncio.run(lauf())

    assert proxy.verbindungen == 3, "nach jedem Abbruch neu verbunden"
    assert angeboten == ["s1", "s2", "s3"], "nach jedem Handshake die Funktionen neu angeboten"
    assert genommen == ["v1", "v2", "v3"], "auch nach dem Reconnect werden Aufträge verarbeitet"
