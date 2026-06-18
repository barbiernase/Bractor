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

log = logging.getLogger(__name__)


class GrpcProxy:
    """
    Verwaltet die bidirektionale gRPC-Verbindung.

    Verwendet grpclib.client.Channel.request() für den
    STREAM_STREAM-RPC — das gibt uns ein Stream-Objekt
    auf dem wir unabhängig senden und empfangen können.
    """

    def __init__(self, generated_module):
        self._gen = generated_module
        self._channel: Channel | None = None
        self._stream: Any = None
        self._session_id: str = ""
        self._connected = False

        # Eingehende Messages → MessageRouter
        self._event_queue: asyncio.Queue = asyncio.Queue()

        # Response-Korrelation
        self._pending_queries: dict[str, asyncio.Future] = {}
        self._pending_triggers: dict[str, asyncio.Future] = {}

        # Concurrency: nur ein send_message() gleichzeitig
        self._send_lock = asyncio.Lock()

        # Signal für Verbindungsstatus
        self._disconnected = asyncio.Event()

        # Proto-Metadaten für den Stream
        self._client_msg_cls = getattr(generated_module, "ClientMessage")
        self._server_msg_cls = getattr(generated_module, "ServerMessage")

        # gRPC Route (package.service/method aus dem Proto)
        # CqrsSolution ist der package-Name aus domain.proto
        self._route = self._detect_route()

    def _detect_route(self) -> str:
        """Erkennt die gRPC-Route aus dem generierten Modul."""
        # betterproto generiert den Stub mit der Route
        stub_cls = getattr(self._gen, "CqrsClientServiceStub", None)
        if stub_cls:
            # Versuche Route aus dem Stub zu extrahieren
            for attr in dir(stub_cls):
                if attr == "connect":
                    # Die Route folgt dem Pattern /{package}.{service}/{method}
                    pass
        # Fallback: aus dem Proto-Package ableiten
        # domain.proto hat: package CqrsSolution; service CqrsClientService
        return "/CqrsSolution.CqrsClientService/Connect"

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
            self._route,
            Cardinality.STREAM_STREAM,
            self._client_msg_cls,
            self._server_msg_cls,
        )
        # Context betreten (öffnet den HTTP/2-Stream)
        await self._stream.__aenter__()

        # Capabilities-Handshake
        cap_msg = self._client_msg_cls(capabilities=capabilities_request)
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

        for f in self._pending_triggers.values():
            if not f.done():
                f.cancel()
        self._pending_triggers.clear()

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
        cmd_req_cls = getattr(self._gen, "CommandRequest")
        msg = self._client_msg_cls(
            command=cmd_req_cls(envelope=envelope_dto)
        )
        await self._send(msg)
        log.debug("Command sent")

    async def send_trigger(
        self, trigger_payload_dto, correlation_id: str | None = None
    ):
        """Trigger senden, auf TriggerAck warten."""
        cid = correlation_id or str(uuid4())
        trigger_req_cls = getattr(self._gen, "TriggerRequest")

        loop = asyncio.get_event_loop()
        future: asyncio.Future = loop.create_future()
        self._pending_triggers[cid] = future

        try:
            msg = self._client_msg_cls(
                trigger=trigger_req_cls(
                    payload=trigger_payload_dto,
                    correlation_id=cid,
                )
            )
            await self._send(msg)
            return await asyncio.wait_for(future, timeout=30.0)
        finally:
            self._pending_triggers.pop(cid, None)

    async def send_query(
        self, query_payload_dto, correlation_id: str | None = None
    ):
        """Query senden, auf QueryResponse warten."""
        cid = correlation_id or str(uuid4())
        query_req_cls = getattr(self._gen, "QueryRequest")

        loop = asyncio.get_event_loop()
        future: asyncio.Future = loop.create_future()
        self._pending_queries[cid] = future

        try:
            msg = self._client_msg_cls(
                query=query_req_cls(
                    correlation_id=cid,
                    payload=query_payload_dto,
                )
            )
            await self._send(msg)
            return await asyncio.wait_for(future, timeout=30.0)
        finally:
            self._pending_queries.pop(cid, None)

    async def publish_transient(self, envelope_dto) -> None:
        """TransientEvent publishen."""
        te_req_cls = getattr(self._gen, "TransientEventRequest")
        msg = self._client_msg_cls(
            transient_event=te_req_cls(envelope=envelope_dto)
        )
        await self._send(msg)

    async def send_trigger_result(
        self, correlation_id: str, accepted: bool, error_message: str = ""
    ) -> None:
        """Antwort auf empfangenen TriggerForward."""
        result_cls = getattr(self._gen, "TriggerResult")
        msg = self._client_msg_cls(
            trigger_result=result_cls(
                correlation_id=correlation_id,
                accepted=accepted,
                error_message=error_message,
            )
        )
        await self._send(msg)

    async def send_query_response(
        self, correlation_id: str, response_payload_dto,
        error_code: str = "", error_message: str = ""
    ) -> None:
        """Antwort auf empfangene QueryForward."""
        answer_cls = getattr(self._gen, "QueryResponseFromClient")
        msg = self._client_msg_cls(
            query_answer=answer_cls(
                correlation_id=correlation_id,
                payload=response_payload_dto,
                error_code=error_code,
                error_message=error_message,
            )
        )
        await self._send(msg)

    # ═══════════════════════════════════════════════════
    # READ-LOOP (Server → Client)
    # ═══════════════════════════════════════════════════

    async def read_loop(self) -> None:
        """
        Empfängt ServerMessages, dispatcht nach oneof-Feld.

        Events/TriggerForwards/QueryForwards → event_queue.
        QueryResponse/TriggerAck → pending Future completieren.
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

                elif field_name == "trigger_forward":
                    await self._event_queue.put(("trigger_forward", payload))

                elif field_name == "query_forward":
                    await self._event_queue.put(("query_forward", payload))

                elif field_name == "query_response":
                    self._complete_future(
                        self._pending_queries,
                        payload.correlation_id,
                        payload,
                        "QueryResponse",
                    )

                elif field_name == "trigger_ack":
                    self._complete_future(
                        self._pending_triggers,
                        payload.correlation_id,
                        payload,
                        "TriggerAck",
                    )

                elif field_name == "error":
                    self._handle_error(payload)

                elif field_name == "subscription_confirmed":
                    log.debug("Subscribed: %s", payload.event_type)

                elif field_name == "command_accepted":
                    log.debug("Command accepted: %s", payload.correlation_id)

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
