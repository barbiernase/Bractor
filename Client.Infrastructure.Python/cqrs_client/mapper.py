# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/mapper.py
"""
Extrahiert konkrete Payloads aus Envelope/Wrapper-Messages
und verpackt ausgehende Payloads in Envelopes.

Deutlich vereinfacht durch betterproto:
  - Kein Konverter-Code (betterproto-Objekte SIND die Payloads)
  - Kein Feld-Mapping (betterproto macht das nativ)
  - Nur oneof-Extraktion und Envelope-Konstruktion

Gegenstück zu ProtoMessageMapper in C# — aber wesentlich schlanker,
weil betterproto die Serialisierung übernimmt.
"""

from __future__ import annotations

import time
from dataclasses import fields
from typing import Any
from uuid import UUID, uuid4

import betterproto


def extract_oneof_payload(
    message: betterproto.Message,
    group_name: str = "payload",
) -> tuple[str, betterproto.Message]:
    """
    Extrahiert den konkreten Typ aus einem betterproto-oneof.

    Returns:
        (field_name, payload) — z.B. ("bild_verfuegbar", BildVerfuegbarDto(...))

    Raises:
        ValueError: Wenn kein oneof-Feld gesetzt ist.
    """
    field_name, payload = betterproto.which_one_of(message, group_name)
    if not field_name:
        raise ValueError(
            f"Kein {group_name}-oneof gesetzt in {type(message).__name__}"
        )
    return field_name, payload


class PayloadMapper:
    """
    Mapper zwischen betterproto-Messages und dem Framework.

    Extraktion (Server → Client):
      - EventEnvelopeDto → konkreter Event-Typ
      - TriggerPayloadDto → konkreter Trigger-Typ
      - QueryPayloadDto → konkreter Query-Typ

    Verpackung (Client → Server):
      - Command → CommandEnvelopeDto
      - Event → EventEnvelopeDto (für TransientEvents)
    """

    def __init__(self, generated_module):
        """
        Args:
            generated_module: Das betterproto-generierte Modul
                (enthält CommandEnvelopeDto, EventEnvelopeDto, etc.)
        """
        self._gen = generated_module
        self._command_field_map = self._build_type_to_field_map("CommandEnvelopeDto")
        self._event_field_map = self._build_type_to_field_map("EventEnvelopeDto")
        self._query_response_field_map = self._build_type_to_field_map("QueryResponsePayloadDto")
        self._trigger_field_map = self._build_type_to_field_map("TriggerPayloadDto")

    def _build_type_to_field_map(self, envelope_class_name: str) -> dict[type, str]:
        """
        Baut eine Zuordnung: payload-Typ → oneof-Feldname im Envelope.

        Beispiel: KlassifiziereBildPaarDurchKiDto → "klassifiziere_bild_paar_durch_ki"

        Nutzt betterproto's Dataclass-Fields um die Zuordnung zu ermitteln.
        """
        envelope_class = getattr(self._gen, envelope_class_name, None)
        if envelope_class is None:
            return {}

        type_to_field: dict[type, str] = {}
        for f in fields(envelope_class):
            # betterproto oneof-Felder haben den Typ der Message als Annotation
            field_type = f.type
            if isinstance(field_type, str):
                # Forward reference — resolve from module
                resolved = getattr(self._gen, field_type, None)
                if resolved is not None:
                    type_to_field[resolved] = f.name
            elif isinstance(field_type, type) and issubclass(field_type, betterproto.Message):
                type_to_field[field_type] = f.name

        return type_to_field

    # ═══════════════════════════════════════════════════
    # EXTRAKTION (eingehend)
    # ═══════════════════════════════════════════════════

    def extract_event_payload(self, envelope) -> betterproto.Message:
        """Extrahiert den konkreten Event-Typ aus EventEnvelopeDto."""
        _, payload = extract_oneof_payload(envelope, "payload")
        return payload

    def extract_trigger_payload(self, wrapper) -> betterproto.Message:
        """Extrahiert den konkreten Trigger-Typ aus TriggerPayloadDto."""
        _, payload = extract_oneof_payload(wrapper, "payload")
        return payload

    def extract_query_payload(self, wrapper) -> betterproto.Message:
        """Extrahiert den konkreten Query-Typ aus QueryPayloadDto."""
        _, payload = extract_oneof_payload(wrapper, "payload")
        return payload

    # ═══════════════════════════════════════════════════
    # VERPACKUNG (ausgehend)
    # ═══════════════════════════════════════════════════

    def wrap_command(
        self,
        command: betterproto.Message,
        aggregate_id: UUID,
        expected_version: int,
        aggregate_type: str = "",
        correlation_id: str = "",
        session_id: str = "",
    ):
        """
        Verpackt einen Command in einen CommandEnvelopeDto.

        Setzt das richtige oneof-Feld basierend auf dem Typ.
        """
        envelope_cls = getattr(self._gen, "CommandEnvelopeDto")
        envelope = envelope_cls(
            command_id=str(uuid4()),
            aggregate_id=str(aggregate_id),
            aggregate_type=aggregate_type,
            expected_version=expected_version,
            created_at_utc=int(time.time() * 1_000),  # Milliseconds (C# erwartet FromUnixTimeMilliseconds)
            correlation_id=correlation_id or str(uuid4()),
            origin_session_id=session_id,
        )

        # Oneof-Feld setzen
        field_name = self._get_oneof_field_name(command, self._command_field_map)
        if field_name:
            setattr(envelope, field_name, command)
        else:
            raise TypeError(
                f"Kein oneof-Feld für {type(command).__name__} "
                f"in CommandEnvelopeDto gefunden"
            )

        return envelope

    def wrap_transient_event(
        self,
        event: betterproto.Message,
        aggregate_id: UUID | None = None,
        correlation_id: str = "",
    ):
        """
        Verpackt ein TransientEvent in einen EventEnvelopeDto.

        TransientEvents werden über den TransientEventRequest-Kanal
        an den Server gesendet und dort via BrokerPublisher verteilt.
        """
        envelope_cls = getattr(self._gen, "EventEnvelopeDto")
        envelope = envelope_cls(
            event_id=str(uuid4()),
            aggregate_id=str(aggregate_id) if aggregate_id else "",
            created_at_utc=int(time.time() * 1_000),
            correlation_id=correlation_id or str(uuid4()),
        )

        field_name = self._get_oneof_field_name(event, self._event_field_map)
        if field_name:
            setattr(envelope, field_name, event)

        return envelope

    def wrap_query_response(self, response: betterproto.Message):
        """
        Verpackt eine Query-Response in einen QueryResponsePayloadDto.

        Setzt das richtige oneof-Feld basierend auf dem Response-Typ — dieselbe Logik wie
        wrap_command/wrap_transient_event (nutzt die aus QueryResponsePayloadDto abgeleitete
        type→oneof-Feld-Map). Damit kann ein Python-Client Queries VOLLSTÄNDIG beantworten
        (vorher: leerer Payload, oneof nie gesetzt).
        """
        payload_cls = getattr(self._gen, "QueryResponsePayloadDto")
        payload = payload_cls()

        field_name = self._get_oneof_field_name(response, self._query_response_field_map)
        if not field_name:
            raise TypeError(
                f"Kein oneof-Feld für {type(response).__name__} "
                f"in QueryResponsePayloadDto gefunden"
            )
        setattr(payload, field_name, response)

        return payload

    def wrap_trigger(self, trigger: betterproto.Message):
        """
        Verpackt einen Trigger in einen TriggerPayloadDto (setzt das oneof-Feld).

        Für Client→Client-Trigger: yieldet ein Handler einen Trigger, verpackt der Router ihn hiermit
        und sendet ihn via GrpcProxy.send_trigger — der Server forwarded ihn an den registrierten
        Client-Handler (TriggerHandlerRegistry). Gegenstück zu extract_trigger_payload.
        """
        payload_cls = getattr(self._gen, "TriggerPayloadDto")
        payload = payload_cls()

        field_name = self._get_oneof_field_name(trigger, self._trigger_field_map)
        if not field_name:
            raise TypeError(
                f"Kein oneof-Feld für {type(trigger).__name__} "
                f"in TriggerPayloadDto gefunden"
            )
        setattr(payload, field_name, trigger)

        return payload

    def _get_oneof_field_name(
        self,
        payload: betterproto.Message,
        field_map: dict[type, str],
    ) -> str | None:
        """Findet den oneof-Feldnamen für einen Payload-Typ."""
        return field_map.get(type(payload))
