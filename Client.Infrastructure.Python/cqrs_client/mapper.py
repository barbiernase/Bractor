# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/mapper.py
"""
Nutzlast und Hüllen: packt Domänen-DTOs in die Leitung des Frameworks und wieder aus.

Die Leitung (Cqrs.Leitung/cqrs.proto → cqrs_client.leitung) kennt keinen Domänen-Typ. Jede Domänen-Nachricht
(Command, Event, Query, Antwort, Trigger, Auftrag) reist als `Nutzlast`:

    typ   = Proto-Name des DTO ohne „Dto“ (z. B. "ErstelleDatensatz")
    daten = die {typ}Dto-Message des Anwendungs-Protos, protobuf-kodiert (betterproto: bytes(dto))

Die Tabelle typ → DTO-Klasse entsteht EINMAL aus dem generierten Domänen-Modul (domain.proto der Anwendung:
nur die DTOs). Gegenstück zum Nutzlast-Mapping auf der C#-Seite.
"""

from __future__ import annotations

import time
from typing import Any
from uuid import UUID, uuid4

import betterproto

from . import leitung
from .leitung import CommandEnvelopeDto, EventEnvelopeDto, Nutzlast

DTO_SUFFIX = "Dto"


def typ_von(klasse: type) -> str:
    """Der Nutzlast-Typ einer DTO-Klasse: ihr Proto-Name ohne „Dto“ (BildVerfuegbarDto → "BildVerfuegbar")."""
    name = klasse.__name__
    return name[: -len(DTO_SUFFIX)] if name.endswith(DTO_SUFFIX) else name


class UnbekannteNutzlast(ValueError):
    """Eine Nutzlast, deren Typ das Domänen-Modul nicht kennt (oder die leer ist)."""


class NutzlastTabelle:
    """
    typ → DTO-Klasse, einmal aus dem generierten Domänen-Modul gebaut: jede betterproto-Message, deren Name auf „Dto“
    endet. `packe` und `entpacke` sind die beiden einzigen Wege zwischen DTO und Leitung.
    """

    def __init__(self, generated_module: Any):
        self._klassen: dict[str, type] = {}
        for wert in vars(generated_module).values():
            if (isinstance(wert, type) and issubclass(wert, betterproto.Message)
                    and wert.__name__.endswith(DTO_SUFFIX)):
                typ = typ_von(wert)
                vorhanden = self._klassen.get(typ)
                if vorhanden is not None and vorhanden is not wert:
                    raise ValueError(f"Nutzlast-Typ {typ} doppelt: {vorhanden!r} und {wert!r}")
                self._klassen[typ] = wert

    def __contains__(self, typ: str) -> bool:
        return typ in self._klassen

    def __len__(self) -> int:
        return len(self._klassen)

    def klasse(self, typ: str) -> type:
        """Die DTO-Klasse zu einem Nutzlast-Typ — unbekannt → UnbekannteNutzlast."""
        if not typ:
            raise UnbekannteNutzlast("Leere Nutzlast (kein Typ gesetzt)")
        klasse = self._klassen.get(typ)
        if klasse is None:
            raise UnbekannteNutzlast(
                f"Unbekannter Nutzlast-Typ '{typ}' — nicht im Domänen-Modul ({len(self._klassen)} DTO-Typen); "
                f"Generat veraltet? (build_python.sh)")
        return klasse

    def packe(self, dto: betterproto.Message) -> Nutzlast:
        """DTO → Nutzlast(typ, daten). Nur Typen des Domänen-Moduls — sonst TypeError."""
        typ = typ_von(type(dto))
        if self._klassen.get(typ) is not type(dto):
            raise TypeError(
                f"{type(dto).__name__} ist kein DTO des Domänen-Moduls — als Nutzlast nicht verpackbar")
        return Nutzlast(typ=typ, daten=bytes(dto))

    def entpacke(self, nutzlast: Nutzlast | None) -> betterproto.Message:
        """Nutzlast → DTO-Instanz (Klasse nach `typ`, Inhalt aus `daten`)."""
        if nutzlast is None:
            raise UnbekannteNutzlast("Keine Nutzlast")
        return self.klasse(nutzlast.typ)().parse(nutzlast.daten)


class PayloadMapper:
    """
    Zwischen Domänen-DTOs und der Leitung.

    Auspacken (Server → Client): Event-Hülle, TriggerForward, QueryForward, ArbeitsAuftrag, QueryResponse → DTO.
    Verpacken (Client → Server): Command → CommandEnvelopeDto, Event → EventEnvelopeDto (Transient / Funktions-Ergebnis),
    Query, Query-Antwort, Trigger → Nutzlast.
    """

    def __init__(self, generated_module):
        """
        Args:
            generated_module: Das betterproto-generierte Domänen-Modul (nur DTOs, z. B. domain_client.generated)
        """
        self.tabelle = NutzlastTabelle(generated_module)

    def packe(self, dto: betterproto.Message) -> Nutzlast:
        return self.tabelle.packe(dto)

    def entpacke(self, nutzlast: Nutzlast | None) -> betterproto.Message:
        return self.tabelle.entpacke(nutzlast)

    # ═══════════════════════════════════════════════════
    # AUSPACKEN (eingehend)
    # ═══════════════════════════════════════════════════

    def extract_event_payload(self, envelope: EventEnvelopeDto) -> betterproto.Message:
        """Das Event aus der Hülle (EventEnvelopeDto.payload)."""
        return self.entpacke(envelope.payload)

    def extract_trigger_payload(self, nutzlast: Nutzlast) -> betterproto.Message:
        """Der Trigger aus TriggerForward.payload."""
        return self.entpacke(nutzlast)

    def extract_query_payload(self, nutzlast: Nutzlast) -> betterproto.Message:
        """Die Query aus QueryForward.payload."""
        return self.entpacke(nutzlast)

    def extract_auftrag_payload(self, nutzlast: Nutzlast) -> betterproto.Message:
        """Der Auftrag einer Katalog-Funktion aus ArbeitsAuftrag.auftrag."""
        return self.entpacke(nutzlast)

    def extract_query_response(self, query_response: leitung.QueryResponse) -> betterproto.Message:
        """Die TYPISIERTE Antwort aus einem QueryResponse (Server → Client) — die Ask-Seite (docs/09 §9.4)."""
        return self.entpacke(query_response.payload)

    # ═══════════════════════════════════════════════════
    # VERPACKEN (ausgehend)
    # ═══════════════════════════════════════════════════

    def wrap_command(
        self,
        command: betterproto.Message,
        aggregate_id: UUID,
        expected_version: int,
        aggregate_type: str = "",
        correlation_id: str = "",
        session_id: str = "",
        causation=None,
    ) -> CommandEnvelopeDto:
        """
        Ein Command in einer CommandEnvelopeDto-Hülle. `causation` (router.Kausalitaet): die Zusage antwortet auf ein
        Event — Stream + Version + Typ + Index reisen mit, der Server leitet daraus die CommandId deterministisch ab
        (doppelt zugestellt ≠ doppelt wirksam, docs/konzept-akteure.md §5.3).
        """
        envelope = CommandEnvelopeDto(
            command_id=str(uuid4()),
            aggregate_id=str(aggregate_id),
            aggregate_type=aggregate_type,
            expected_version=expected_version,
            created_at_utc=int(time.time() * 1_000),  # Millisekunden (C# erwartet FromUnixTimeMilliseconds)
            correlation_id=correlation_id or str(uuid4()),
            origin_session_id=session_id,
            payload=self.packe(command),
        )
        if causation is not None:
            envelope.causation_stream_id = causation.stream_id
            envelope.causation_version = causation.version
            envelope.causation_type = causation.typ
            envelope.causation_index = causation.index
        return envelope

    def wrap_transient_event(
        self,
        event: betterproto.Message,
        aggregate_id: UUID | None = None,
        correlation_id: str = "",
    ) -> EventEnvelopeDto:
        """Ein TransientEvent in einer EventEnvelopeDto-Hülle (TransientEventRequest → BrokerPublisher)."""
        return EventEnvelopeDto(
            event_id=str(uuid4()),
            aggregate_id=str(aggregate_id) if aggregate_id else "",
            created_at_utc=int(time.time() * 1_000),
            correlation_id=correlation_id or str(uuid4()),
            payload=self.packe(event),
        )

    def wrap_ergebnis(self, ergebnis: betterproto.Message, vorgang: str, korrelation: str = "") -> EventEnvelopeDto:
        """
        Das Ergebnis einer Katalog-Funktion (einer ihrer OneOf-Fälle, ein Event) als EventEnvelopeDto für
        ArbeitsErgebnis.ergebnis. Der Server liest nur die Nutzlast und stempelt Stream/Korrelation/Akteur selbst;
        Stream = Vorgang (Ausführungs-Stream) steht nur zur Lesbarkeit drin.
        """
        return EventEnvelopeDto(
            event_id=str(uuid4()),
            aggregate_id=vorgang,
            created_at_utc=int(time.time() * 1_000),
            causation_id=vorgang,
            correlation_id=korrelation,
            payload=self.packe(ergebnis),
        )

    def wrap_query(self, query: betterproto.Message) -> Nutzlast:
        """Eine Query als Nutzlast für QueryRequest.payload — der Proxy hängt Korrelation + 30 s Timeout drum."""
        return self.packe(query)

    def wrap_query_response(self, response: betterproto.Message) -> Nutzlast:
        """Eine Query-Antwort als Nutzlast für QueryResponseFromClient.payload."""
        return self.packe(response)

    def wrap_trigger(self, trigger: betterproto.Message) -> Nutzlast:
        """Ein Trigger als Nutzlast für TriggerRequest.payload (Client→Client, der Server forwarded ihn)."""
        return self.packe(trigger)


__all__ = ["DTO_SUFFIX", "NutzlastTabelle", "PayloadMapper", "UnbekannteNutzlast", "typ_von"]
