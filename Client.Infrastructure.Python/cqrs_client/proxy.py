# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/proxy.py
"""
Verwaltet die bidirektionale gRPC-Verbindung zum C#-Server.

Nutzt grpclib direkt (nicht den betterproto-generierten Stub),
weil der Stub-API nicht für langlebige bidirektionale Streams
geeignet ist — er erwartet einen Request-Iterator, wir brauchen
aber unabhängiges Send und Receive.

grpclib Lowlevel-API gibt uns:
  - stream.send_message(msg)  — jederzeit, aus jedem Task
  - stream.recv_message()     — blockierend, im Read-Loop-Task
  - Concurrent Send/Receive   — grpclib erlaubt das explizit
"""

from __future__ import annotations

import asyncio
import logging
from typing import Any
from uuid import uuid4

import betterproto
from grpclib.client import Channel
from grpclib.const import Cardinality

from . import leitung as L

log = logging.getLogger(__name__)

# gRPC-Route aus der Leitung (Cqrs.Leitung/cqrs.proto: package cqrs; service CqrsClientService; rpc Connect).
# Das Python-Generat entsteht ohne package (flach, build_leitung.sh) — die Route steht deshalb hier.
ROUTE = "/cqrs.CqrsClientService/Connect"


class GrpcProxy:
    """
    Verwaltet die bidirektionale gRPC-Verbindung.

    Verwendet grpclib.client.Channel.request() für den
    STREAM_STREAM-RPC — das gibt uns ein Stream-Objekt
    auf dem wir unabhängig senden und empfangen können.
    Alle Nachrichten sind Typen der Leitung (cqrs_client.leitung); Domänen-DTOs reisen darin als Nutzlast.
    """

    def __init__(self, akteur_token: str | None = None):
        # Akteur (docs/konzept-akteure.md): Token im gRPC-Header "akteur-token" — der Server ordnet es einem
        # IAkteur zu und lässt nur dessen IDarf<…> durch. None = kein Header (Server ohne Akteure: alles offen).
        self._akteur_token = akteur_token
        self._channel: Channel | None = None
        self._stream: Any = None
        self._session_id: str = ""
        self._connected = False

        # Eingehende Messages → MessageRouter
        self._event_queue: asyncio.Queue = asyncio.Queue()

        # Response-Korrelation
        self._pending_queries: dict[str, asyncio.Future] = {}

        # Concurrency: nur ein send_message() gleichzeitig
        self._send_lock = asyncio.Lock()

        # Signal für Verbindungsstatus
        self._disconnected = asyncio.Event()

    @property
    def session_id(self) -> str:
        return self._session_id

    @property
    def is_connected(self) -> bool:
        return self._connected

    @property
    def event_queue(self) -> asyncio.Queue:
        return self._event_queue

    @property
    def disconnected(self) -> asyncio.Event:
        return self._disconnected

    # ═══════════════════════════════════════════════════
    # CONNECT / DISCONNECT
    # ═══════════════════════════════════════════════════

    async def connect(self, host: str, port: int, capabilities_request) -> Any:
        """
        Öffnet den bidirektionalen gRPC-Stream und führt den
        Capabilities-Handshake durch.

        Verwendet grpclib.Channel.request() mit STREAM_STREAM
        Cardinality — das gibt uns ein Stream-Objekt mit
        send_message() und recv_message().
        """
        log.info("Connecting to %s:%d ...", host, port)

        self._channel = Channel(host, port)

        # grpclib Lowlevel: Bidirektionalen Stream öffnen
        self._stream = self._channel.request(
            ROUTE,
            Cardinality.STREAM_STREAM,
            L.ClientMessage,
            L.ServerMessage,
            metadata={"akteur-token": self._akteur_token} if self._akteur_token else None,
        )
        # Context betreten (öffnet den HTTP/2-Stream)
        await self._stream.__aenter__()

        # Capabilities-Handshake
        cap_msg = L.ClientMessage(capabilities=capabilities_request)
        await self._stream.send_message(cap_msg)
        log.info("CapabilitiesRequest sent, waiting for response...")

        response = await self._stream.recv_message()
        if response is None:
            raise ConnectionError(
                "Server closed stream before CapabilitiesResponse"
            )

        field_name, cap_response = betterproto.which_one_of(response, "message")
        if field_name != "capabilities_response":
            raise ConnectionError(
                f"Expected CapabilitiesResponse, got {field_name}"
            )

        self._session_id = cap_response.session_id
        self._connected = True
        self._disconnected.clear()

        log.info(
            "Connected. Session: %s, Events: %s, Commands: %s",
            self._session_id,
            list(cap_response.subscribed_events),
            list(cap_response.allowed_commands),
        )
        return cap_response

    async def disconnect(self) -> None:
        """Trennt die Verbindung sauber."""
        if not self._connected:
            return

        log.info("Disconnecting...")
        self._connected = False
        self._disconnected.set()

        # Pending abbrechen
        for f in self._pending_queries.values():
            if not f.done():
                f.cancel()
        self._pending_queries.clear()

        # Stream schließen
        if self._stream:
            try:
                await self._stream.end()
                await self._stream.__aexit__(None, None, None)
            except Exception:
                pass
            self._stream = None

        if self._channel:
            self._channel.close()
            self._channel = None

        self._session_id = ""
        log.info("Disconnected")

    # ═══════════════════════════════════════════════════
    # SENDEN (Client → Server)
    # Lock weil grpclib send_message nicht concurrent-safe ist
    # ═══════════════════════════════════════════════════

    async def _send(self, msg) -> None:
        """Thread-safe send über den Stream."""
        async with self._send_lock:
            self._ensure_connected()
            await self._stream.send_message(msg)

    async def send_command(self, envelope_dto) -> None:
        """Fire-and-forget Command."""
        msg = L.ClientMessage(
            command=L.CommandRequest(envelope=envelope_dto)
        )
        await self._send(msg)
        log.debug("Command sent")

    async def send_query(
        self, nutzlast: L.Nutzlast, correlation_id: str | None = None
    ):
        """Query (als Nutzlast) senden, auf QueryResponse warten."""
        cid = correlation_id or str(uuid4())

        loop = asyncio.get_event_loop()
        future: asyncio.Future = loop.create_future()
        self._pending_queries[cid] = future

        try:
            msg = L.ClientMessage(
                query=L.QueryRequest(
                    correlation_id=cid,
                    payload=nutzlast,
                )
            )
            await self._send(msg)
            return await asyncio.wait_for(future, timeout=30.0)
        finally:
            self._pending_queries.pop(cid, None)

    async def publish_transient(self, envelope_dto) -> None:
        """TransientEvent publishen."""
        msg = L.ClientMessage(
            transient_event=L.TransientEventRequest(envelope=envelope_dto)
        )
        await self._send(msg)

    # ═══════════════════════════════════════════════════
    # FUNKTIONEN DRAUSSEN (ITraegt, Laufort Extern)
    # ═══════════════════════════════════════════════════

    async def send_funktionen_anbieten(self, angebote: list[tuple[str, int]]) -> None:
        """„Ich biete Funktion F mit n Slots“ — der Server holt dann für diese Session Eingänge beim Vermittler."""
        msg = L.ClientMessage(
            funktionen_anbieten=L.FunktionenAnbieten(
                angebote=[L.FunktionsAngebot(funktion=f, slots=s) for f, s in angebote]
            )
        )
        await self._send(msg)

    async def send_arbeits_ergebnis(self, vorgang: str, ausgaben: list[tuple[str, object]] | None = None, ab: int = 0,
                                    fertig: bool = True, fehler: str = "") -> None:
        """
        Die Antwort auf einen Eingang — ganz oder (Strom) als Stück: `ausgaben` = (Art, Nutzlast) in der Reihenfolge der Funktion, `ab` = Stelle der
        ersten Ausgabe dieses Stücks, `fertig` = der Lauf ist vollständig; ein nicht leerer `fehler` = gescheitert.
        """
        msg = L.ClientMessage(arbeits_ergebnis=L.ArbeitsErgebnis(
            vorgang=vorgang,
            ausgaben=[L.FunktionsAusgabe(**{art: nutzlast}) for art, nutzlast in (ausgaben or [])],
            ab=ab, fertig=fertig, fehler=fehler))
        await self._send(msg)

    async def send_arbeit_lebt(self, vorgang: str) -> None:
        """Lebenszeichen eines laufenden Eingangs — verlängert seine Lease beim Vermittler."""
        msg = L.ClientMessage(arbeit_lebt=L.ArbeitLebtMeldung(vorgang=vorgang))
        await self._send(msg)

    # ═══════════════════════════════════════════════════
    # READ-LOOP (Server → Client)
    # ═══════════════════════════════════════════════════

    async def read_loop(self) -> None:
        """
        Empfängt ServerMessages, dispatcht nach oneof-Feld.

        Events/ArbeitsAufträge → event_queue.
        QueryResponse → pending Future completieren.
        """
        try:
            while self._connected:
                server_msg = await self._stream.recv_message()
                if server_msg is None:
                    log.info("Server closed stream")
                    break

                field_name, payload = betterproto.which_one_of(
                    server_msg, "message"
                )

                if field_name == "event":
                    await self._event_queue.put(("event", payload))

                elif field_name == "arbeits_auftrag":
                    await self._event_queue.put(("arbeits_auftrag", payload))

                elif field_name == "query_response":
                    self._complete_future(
                        self._pending_queries,
                        payload.correlation_id,
                        payload,
                        "QueryResponse",
                    )

                elif field_name == "error":
                    self._handle_error(payload)

                elif field_name == "aktualisiert":
                    log.debug("Aktualisiert: %d Queries (ein Worker fragt nicht neu)", len(payload.queries))

                else:
                    log.warning("Unknown message: %s", field_name)

        except asyncio.CancelledError:
            log.info("Read loop cancelled")
        except Exception as e:
            log.error("Read loop error: %s", e, exc_info=True)
        finally:
            self._connected = False
            self._disconnected.set()

    # ═══════════════════════════════════════════════════
    # INTERNALS
    # ═══════════════════════════════════════════════════

    def _complete_future(
        self, pending: dict, cid: str, result, label: str
    ):
        future = pending.get(cid)
        if future and not future.done():
            future.set_result(result)
        else:
            log.warning("%s for unknown CorrelationId: %s", label, cid)

    def _handle_error(self, payload):
        cid = payload.correlation_id
        if cid:
            future = self._pending_queries.get(cid)
            if future and not future.done():
                future.set_exception(
                    ServerError(payload.code, payload.message)
                )
                return
        log.error("Server error: [%s] %s", payload.code, payload.message)

    def _ensure_connected(self) -> None:
        if not self._connected:
            raise ConnectionError("Not connected")


class ServerError(Exception):
    """Fehler vom Server (ErrorResponse)."""

    def __init__(self, code: str, message: str):
        self.code = code
        super().__init__(f"[{code}] {message}")
