# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/dispatch.py
"""
Type-hint-basierter Dispatch-Deskriptor.

Kein Naming-Convention-basiertes Routing — alles läuft über Python-Typen
und type()-Dispatch.

Verwendung:
    class MyClient(CqrsClient[MyState]):

        @handle.register
        async def on_bild(self, event: BildVerfuegbarDto, ctx, state):
            ...
            yield KlassifiziereEinzelBildDurchKiDto(...)

        @handle.register
        async def on_pair(self, event: ImagePairKomplettDto, ctx, state):
            ...
            yield KlassifiziereBildPaarDurchKiDto(...)

Introspection:
    client.handle.registered_types  →  {BildVerfuegbarDto, ImagePairKomplettDto}
"""

from __future__ import annotations

import inspect
from typing import Any, AsyncIterator, get_type_hints


class HandleDescriptor:
    """
    Deskriptor der Handler-Methoden per Type-Hint registriert.

    Lebt als Instanz-Attribut auf der Klasse. Jede Subklasse bekommt
    ihre eigene Registry (kein Leak zwischen Klassen).

    Dispatch: type(payload) → Handler-Methode (O(1) Dictionary-Lookup).
    """

    def __init__(self):
        self._handlers: dict[type, Any] = {}
        self._owner: type | None = None

    @staticmethod
    def register(method):
        """
        Dekorator für Handler-Methoden.

        Extrahiert den Payload-Typ aus dem Type-Hint des zweiten Parameters
        (nach self). Registriert die Methode im HandleDescriptor der Klasse.

        Unterstützt:
        - async def handler(self, event: SomeDto, ctx, state) → async generator (yield)
        - async def handler(self, event: SomeDto, ctx, state) → async coroutine (kein yield)
        """
        # Markiere die Methode für spätere Registrierung in __init_subclass__
        method._handle_register = True
        return method

    def _register_method(self, payload_type: type, method):
        """Registriert eine Handler-Methode für einen Payload-Typ."""
        if payload_type in self._handlers:
            existing = self._handlers[payload_type]
            raise TypeError(
                f"Doppelte Registrierung für {payload_type.__name__}: "
                f"{existing.__name__} und {method.__name__}"
            )
        self._handlers[payload_type] = method

    @property
    def registered_types(self) -> frozenset[type]:
        """Alle registrierten Payload-Typen (für CapabilitiesRequest)."""
        return frozenset(self._handlers.keys())

    def get_handler(self, payload_type: type):
        """Gibt die Handler-Methode für einen Payload-Typ zurück, oder None."""
        return self._handlers.get(payload_type)

    async def receive(
        self,
        instance: Any,
        payload: Any,
        ctx: Any,
        state: Any,
    ) -> AsyncIterator[Any]:
        """
        Dispatcht einen Payload an den registrierten Handler.

        Unterstützt:
        - async generators (yield-basiert → iteriert yields)
        - async coroutines (return-basiert → yield return-value falls nicht None)
        - sync generators (yield-basiert, sync)

        Yields alle Outputs des Handlers.
        """
        handler = self._handlers.get(type(payload))
        if handler is None:
            return

        result = handler(instance, payload, ctx, state)

        if inspect.isasyncgen(result):
            # async def handler(...): yield Command(...)
            async for output in result:
                yield output
        elif inspect.isawaitable(result):
            # async def handler(...): return Command(...) oder None
            value = await result
            if value is not None:
                yield value
        elif inspect.isgenerator(result):
            # def handler(...): yield Command(...)
            for output in result:
                yield output
        else:
            # Synchroner Return
            if result is not None:
                yield result


def _extract_payload_type(method) -> type | None:
    """
    Extrahiert den Payload-Typ aus dem Type-Hint des zweiten Parameters.

    Erwartet: def handler(self, event: SomeDto, ctx, state)
                                       ^^^^^^^^
    """
    try:
        hints = get_type_hints(method)
    except Exception:
        return None

    sig = inspect.signature(method)
    params = list(sig.parameters.keys())

    # Mindestens (self, payload, ...)
    if len(params) < 2:
        return None

    # Zweiter Parameter (nach self) ist der Payload
    payload_param = params[1]
    return hints.get(payload_param)


class HandlerMeta(type):
    """
    Metaklasse die @handle.register-Methoden bei Klassendefinition sammelt.

    Durchläuft alle Methoden der Klasse, findet markierte Handler
    (method._handle_register == True), extrahiert den Payload-Typ
    aus dem Type-Hint, und registriert sie im HandleDescriptor.
    """

    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)

        # HandleDescriptor erstellen (eigene Kopie pro Klasse)
        descriptor = HandleDescriptor()

        # Bestehende Handler aus Basis-Klassen übernehmen
        for base in bases:
            if hasattr(base, 'handle') and isinstance(base.handle, HandleDescriptor):
                for payload_type, method in base.handle._handlers.items():
                    descriptor._handlers[payload_type] = method

        # Eigene markierte Methoden registrieren
        for attr_name, attr_value in namespace.items():
            if callable(attr_value) and getattr(attr_value, '_handle_register', False):
                payload_type = _extract_payload_type(attr_value)
                if payload_type is None:
                    raise TypeError(
                        f"@handle.register auf {name}.{attr_name}: "
                        f"Kein Type-Hint für den Payload-Parameter gefunden. "
                        f"Erwartet: async def {attr_name}(self, event: SomeType, ctx, state)"
                    )
                descriptor._register_method(payload_type, attr_value)

        cls.handle = descriptor
        return cls


class HandlerBase(metaclass=HandlerMeta):
    """
    Basisklasse für Klassen die @handle.register verwenden.

    Stellt sicher dass jede Subklasse einen eigenen HandleDescriptor bekommt.
    """
    handle: HandleDescriptor


# Modul-Level Alias — ermöglicht @handle.register als Dekorator
handle = HandleDescriptor