# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/registry.py
"""
Kategorisiert betterproto-generierte Typen nach ihrer Nachrichtenkategorie.

Die Klassifizierung ergibt sich aus der oneof-Zugehörigkeit im Proto:
  - EventEnvelopeDto.payload.oneof  → EVENT
  - CommandEnvelopeDto.payload.oneof → COMMAND
  - TriggerPayloadDto.payload.oneof  → TRIGGER
  - QueryPayloadDto.payload.oneof    → QUERY
  - QueryResponsePayloadDto.payload.oneof → QUERY_RESPONSE

Zwei Befüllungsoptionen:
  1. Manuell: frozensets mit den generierten Typen (einfache Auflistung)
  2. Automatisch: generate_registry.py liest Proto-Deskriptoren

Beide Varianten liefern dasselbe Ergebnis: type → category Zuordnung.
"""

from __future__ import annotations

from enum import Enum, auto
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    pass


class MessageCategory(Enum):
    EVENT = auto()
    COMMAND = auto()
    TRIGGER = auto()
    QUERY = auto()
    QUERY_RESPONSE = auto()


class CategoryRegistry:
    """
    Registry für die Zuordnung betterproto-Typ → MessageCategory.

    Wird beim Start einmalig befüllt (entweder manuell oder generiert).
    Danach nur noch O(1)-Lookups.
    """

    def __init__(
        self,
        events: frozenset[type] | None = None,
        commands: frozenset[type] | None = None,
        triggers: frozenset[type] | None = None,
        queries: frozenset[type] | None = None,
        query_responses: frozenset[type] | None = None,
    ):
        self._events = events or frozenset()
        self._commands = commands or frozenset()
        self._triggers = triggers or frozenset()
        self._queries = queries or frozenset()
        self._query_responses = query_responses or frozenset()

        # Invertierter Index: type → category (O(1)-Lookup)
        self._type_to_category: dict[type, MessageCategory] = {}
        for t in self._events:
            self._type_to_category[t] = MessageCategory.EVENT
        for t in self._commands:
            self._type_to_category[t] = MessageCategory.COMMAND
        for t in self._triggers:
            self._type_to_category[t] = MessageCategory.TRIGGER
        for t in self._queries:
            self._type_to_category[t] = MessageCategory.QUERY
        for t in self._query_responses:
            self._type_to_category[t] = MessageCategory.QUERY_RESPONSE

    def classify(self, t: type) -> MessageCategory:
        """Klassifiziert einen betterproto-generierten Typ."""
        category = self._type_to_category.get(t)
        if category is None:
            raise TypeError(
                f"Unbekannter Typ: {t.__name__}. "
                f"Nicht in der CategoryRegistry registriert."
            )
        return category

    def is_event(self, t: type) -> bool:
        return t in self._events

    def is_command(self, t: type) -> bool:
        return t in self._commands

    def is_trigger(self, t: type) -> bool:
        return t in self._triggers

    def is_query(self, t: type) -> bool:
        return t in self._queries

    def is_query_response(self, t: type) -> bool:
        return t in self._query_responses

    @staticmethod
    def capabilities_name(t: type) -> str:
        """
        BildVerfuegbarDto → 'BildVerfuegbar'

        Entfernt das 'Dto'-Suffix das betterproto aus dem Proto-Message-Namen
        generiert. Der C#-CapabilitiesHandler erwartet den Namen ohne Suffix.
        """
        name = t.__name__
        if name.endswith("Dto"):
            return name[:-3]
        return name

    @property
    def all_events(self) -> frozenset[type]:
        return self._events

    @property
    def all_commands(self) -> frozenset[type]:
        return self._commands

    @property
    def all_triggers(self) -> frozenset[type]:
        return self._triggers

    @property
    def all_queries(self) -> frozenset[type]:
        return self._queries

    def __repr__(self) -> str:
        return (
            f"CategoryRegistry("
            f"events={len(self._events)}, "
            f"commands={len(self._commands)}, "
            f"triggers={len(self._triggers)}, "
            f"queries={len(self._queries)}, "
            f"query_responses={len(self._query_responses)})"
        )
