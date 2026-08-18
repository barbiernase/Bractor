# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/router.py
"""
Verbindet PayloadMapper, Dispatch und GrpcProxy.

Routet eingehende Nachrichten an Handler und
ausgehende Yields zurück über den gRPC-Stream.

Kernschleife:
    event_queue → PayloadMapper → Dispatch → Handler → yield → route_output → GrpcProxy
"""

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass, field
from typing import Any
from uuid import UUID, uuid4

from .dispatch import HandleDescriptor
from .mapper import PayloadMapper
from .proxy import GrpcProxy
from .registry import CategoryRegistry, MessageCategory
from .versioning import VersionTracker

log = logging.getLogger(__name__)


@dataclass
class MessageContext:
    """
    Kontext der mit jeder eingehenden Nachricht an den Handler übergeben wird.

    Enthält Metadaten aus dem Envelope und Zugriff auf Framework-Services.
    """
    aggregate_id: UUID | None = None
    aggregate_type: str = ""
    aggregate_version: int = 0
    correlation_id: str = ""
    session_id: str = ""

    # Framework-Services (werden vom Router gesetzt)
    _proxy: GrpcProxy | None = field(default=None, repr=False)
    _mapper: PayloadMapper | None = field(default=None, repr=False)
    _version_tracker: VersionTracker | None = field(default=None, repr=False)

    def get_expected_version(self, aggregate_id: UUID) -> int:
        """Shortcut für VersionTracker-Zugriff."""
        if self._version_tracker:
            return self._version_tracker.get_expected_version(aggregate_id)
        return -1


class MessageRouter:
    """
    Zentrale Verarbeitungsschleife für eingehende Nachrichten.

    Liest aus der event_queue des GrpcProxy, extrahiert Payloads,
    dispatcht an Handler, routet yielded Outputs.
    """

    async def process_loop(
        self,
        proxy: GrpcProxy,
        handle: HandleDescriptor,
        instance: Any,
        state: Any,
        mapper: PayloadMapper,
        registry: CategoryRegistry,
        version_tracker: VersionTracker,
    ) -> None:
        """
        Hauptschleife — läuft bis die Verbindung abbricht.

        Verarbeitet drei Arten von Messages aus der event_queue:
        - "event": EventNotification → Payload extrahieren → Handler → yield Outputs
        - "trigger_forward": TriggerForward → Payload extrahieren → Handler → TriggerResult
        - "query_forward": QueryForward → Payload extrahieren → Handler → QueryResponse
        """
        while proxy.is_connected:
            try:
                msg_type, msg = await asyncio.wait_for(
                    proxy.event_queue.get(), timeout=1.0
                )
            except asyncio.TimeoutError:
                continue
            except asyncio.CancelledError:
                break

            try:
                if msg_type == "event":
                    await self._handle_event(
                        msg, proxy, handle, instance, state,
                        mapper, registry, version_tracker
                    )
                elif msg_type == "trigger_forward":
                    await self._handle_trigger_forward(
                        msg, proxy, handle, instance, state,
                        mapper, registry
                    )
                elif msg_type == "query_forward":
                    await self._handle_query_forward(
                        msg, proxy, handle, instance, state,
                        mapper, registry
                    )
                else:
                    log.warning("Unknown message type in queue: %s", msg_type)
            except Exception as e:
                log.error("Error processing %s: %s", msg_type, e, exc_info=True)

    # ═══════════════════════════════════════════════════
    # EVENT HANDLING
    # ═══════════════════════════════════════════════════

    async def _handle_event(
        self, notification, proxy, handle, instance, state,
        mapper, registry, version_tracker
    ):
        """Verarbeitet eine EventNotification."""
        envelope = notification.envelope

        # Payload extrahieren
        payload = mapper.extract_event_payload(envelope)

        # Version tracken
        agg_id = _parse_uuid(envelope.aggregate_id)
        if agg_id and envelope.aggregate_version > 0:
            version_tracker.track(agg_id, envelope.aggregate_version)

        # Context aufbauen
        ctx = MessageContext(
            aggregate_id=agg_id,
            aggregate_type=envelope.aggregate_type,
            aggregate_version=envelope.aggregate_version,
            correlation_id=envelope.correlation_id,
            session_id=proxy.session_id,
            _proxy=proxy,
            _mapper=mapper,
            _version_tracker=version_tracker,
        )

        log.info(
            "Event: %s (v%d, agg=%s)",
            type(payload).__name__,
            envelope.aggregate_version,
            envelope.aggregate_id[:8] if envelope.aggregate_id else "?"
        )

        # Dispatch + Output-Routing
        async for output in handle.receive(instance, payload, ctx, state):
            await self._route_output(output, ctx, proxy, mapper, registry)

    # ═══════════════════════════════════════════════════
    # TRIGGER FORWARD HANDLING
    # ═══════════════════════════════════════════════════

    async def _handle_trigger_forward(
        self, trigger_fwd, proxy, handle, instance, state,
        mapper, registry
    ):
        """Verarbeitet einen TriggerForward vom Server."""
        correlation_id = trigger_fwd.correlation_id

        try:
            payload = mapper.extract_trigger_payload(trigger_fwd.payload)

            ctx = MessageContext(
                correlation_id=correlation_id,
                session_id=proxy.session_id,
                _proxy=proxy,
                _mapper=mapper,
            )

            log.info("TriggerForward: %s", type(payload).__name__)

            # Dispatch — Outputs werden geroutet
            async for output in handle.receive(instance, payload, ctx, state):
                await self._route_output(output, ctx, proxy, mapper, registry)

            # Trigger accepted
            await proxy.send_trigger_result(correlation_id, accepted=True)

        except Exception as e:
            log.error("Trigger handler failed: %s", e, exc_info=True)
            await proxy.send_trigger_result(
                correlation_id, accepted=False, error_message=str(e)
            )

    # ═══════════════════════════════════════════════════
    # QUERY FORWARD HANDLING
    # ═══════════════════════════════════════════════════

    async def _handle_query_forward(
        self, query_fwd, proxy, handle, instance, state,
        mapper, registry
    ):
        """Verarbeitet einen QueryForward vom Server."""
        correlation_id = query_fwd.correlation_id

        try:
            payload = mapper.extract_query_payload(query_fwd.payload)

            ctx = MessageContext(
                correlation_id=correlation_id,
                session_id=proxy.session_id,
                _proxy=proxy,
                _mapper=mapper,
            )

            log.info("QueryForward: %s", type(payload).__name__)

            # Dispatch — der erste yield ist die Query-Response
            response = None
            async for output in handle.receive(instance, payload, ctx, state):
                if registry.is_query_response(type(output)):
                    response = output
                    break
                else:
                    # Andere Outputs (Commands, Events) normal routen
                    await self._route_output(output, ctx, proxy, mapper, registry)

            if response is not None:
                # Response-Payload in QueryResponsePayloadDto verpacken — das oneof-Feld wird über
                # die aus QueryResponsePayloadDto abgeleitete type→Feld-Map gesetzt (wie wrap_command).
                resp_payload = mapper.wrap_query_response(response)
                await proxy.send_query_response(correlation_id, resp_payload)
            else:
                await proxy.send_query_response(
                    correlation_id, None,
                    error_code="NO_RESPONSE",
                    error_message="Handler did not yield a query response"
                )

        except Exception as e:
            log.error("Query handler failed: %s", e, exc_info=True)
            await proxy.send_query_response(
                correlation_id, None,
                error_code="HANDLER_ERROR",
                error_message=str(e)
            )

    # ═══════════════════════════════════════════════════
    # OUTPUT ROUTING
    # ═══════════════════════════════════════════════════

    async def _route_output(
        self,
        output,
        ctx: MessageContext,
        proxy: GrpcProxy,
        mapper: PayloadMapper,
        registry: CategoryRegistry,
    ) -> None:
        """
        Routet einen yielded Output basierend auf CategoryRegistry.

        Command   → CommandEnvelope → GrpcProxy.send_command
        Trigger   → TriggerPayload → GrpcProxy.send_trigger
        Event     → EventEnvelope → GrpcProxy.publish_transient (TransientEvent)
        """
        category = registry.classify(type(output))

        if category == MessageCategory.COMMAND:
            # AggregateId kommt entweder vom Output selbst oder vom Context
            agg_id = getattr(output, "aggregate_id", None)
            if isinstance(agg_id, str):
                agg_id = UUID(agg_id)
            elif agg_id is None:
                agg_id = ctx.aggregate_id

            if agg_id is None:
                log.error("Command %s has no aggregate_id", type(output).__name__)
                return

            # Aus einem Event-Handler gelieferte Commands sind REAKTIONEN, keine Client-Commands
            # mit behaupteter Version: sie werden im Emittiert-Modus (§4.2) gesendet — keine OCC,
            # die Empfaenger-Inbox dedupliziert. Sonst scheitern sie am co-committeten
            # KommandoVerarbeitet-Marker (der Stream steht eine Version hoeher als das Event, auf
            # das reagiert wurde). Sentinel: expected_version < 0 -> der Server mappt auf Emittiert.
            envelope = mapper.wrap_command(
                output,
                aggregate_id=agg_id,
                expected_version=-1,
                session_id=proxy.session_id,
            )
            await proxy.send_command(envelope)
            log.info("→ Command: %s (agg=%s)", type(output).__name__, str(agg_id)[:8])

        elif category == MessageCategory.EVENT:
            # TransientEvent
            envelope = mapper.wrap_transient_event(
                output,
                aggregate_id=ctx.aggregate_id,
                correlation_id=ctx.correlation_id,
            )
            await proxy.publish_transient(envelope)
            log.info("→ TransientEvent: %s", type(output).__name__)

        elif category == MessageCategory.TRIGGER:
            # Client→Client-Trigger: der Server forwarded ihn an den registrierten Client-Handler
            # (TriggerHandlerRegistry) und quittiert per TriggerAck. Fire-and-forget aus Router-Sicht.
            payload = mapper.wrap_trigger(output)
            await proxy.send_trigger(payload, correlation_id=ctx.correlation_id)
            log.info("→ Trigger: %s", type(output).__name__)

        elif category == MessageCategory.QUERY_RESPONSE:
            # Query-Responses werden in _handle_query_forward direkt gesendet
            pass

        else:
            log.warning("Unbekannter Output-Typ: %s (%s)", type(output).__name__, category)


def _parse_uuid(value: str) -> UUID | None:
    """Parst einen UUID-String, gibt None bei leerem/ungültigem Input."""
    if not value:
        return None
    try:
        return UUID(value)
    except ValueError:
        return None
