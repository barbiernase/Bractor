# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/client.py
"""
Basisklasse für Python First-Citizen Clients.

Der Entwickler:
    1. Erbt von CqrsClient[MyState]
    2. Registriert Handler per @handle.register mit Type-Hints
       auf betterproto-generierte Typen
    3. Ruft client.run("host", port) auf

Beispiel:
    @dataclass
    class ClassifierState:
        processed: int = 0

    class ImageClassifier(CqrsClient[ClassifierState]):

        _declared_command_types = [
            KlassifiziereBildPaarDurchKiDto,
        ]

        @handle.register
        async def on_pair(self, event: ImagePairKomplettDto, ctx, state):
            result = await self.model.classify_pair(...)
            state.processed += 1
            yield KlassifiziereBildPaarDurchKiDto(
                aggregate_id=str(ctx.aggregate_id),
                label=result.label,
            )

    if __name__ == "__main__":
        client = ImageClassifier(config={...})
        client.run("grpc-server", 5001)
"""

from __future__ import annotations

from pathlib import Path
import asyncio
import logging
from typing import Any, ClassVar, Generic, TypeVar, get_args

from .connection import ConnectionManager
from .dispatch import HandlerBase, handle
from .mapper import PayloadMapper
from .proto_sync import ensure_types_current, verify_hash_at_connect
from .proxy import GrpcProxy
from .registry import CategoryRegistry, MessageCategory
from .router import MessageRouter
from .versioning import VersionTracker

log = logging.getLogger(__name__)

S = TypeVar("S")


class CqrsClient(HandlerBase, Generic[S]):
    """
    Basisklasse für Python First-Citizen Clients.

    Lifecycle:
        run() → _build_capabilities_request() → connect() → parallel:
            - read_loop (GrpcProxy)
            - process_loop (MessageRouter)
            - monitor (ConnectionManager)
    """

    # Subklassen überschreiben diese mit ihren Command-Typen
    _declared_command_types: ClassVar[list[type]] = []

    def __init__(
        self,
        registry: CategoryRegistry,
        generated_module,
        config: dict[str, Any] | None = None,
    ):
        """
        Args:
            registry: CategoryRegistry mit allen Domain-Typen
            generated_module: Das betterproto-generierte Modul
            config: Optionale Konfiguration für die Subklasse
        """
        self._registry = registry
        self._gen = generated_module
        self._config = config or {}

        self._proxy = GrpcProxy(generated_module)
        self._mapper = PayloadMapper(generated_module)
        self._router = MessageRouter()
        self._connection = ConnectionManager(self._proxy)
        self._version_tracker = VersionTracker()
        self._state: S = self._create_initial_state()

    @property
    def state(self) -> S:
        """Zugriff auf den aktuellen State."""
        return self._state

    @property
    def session_id(self) -> str:
        """Aktuelle Session-ID (leer wenn nicht verbunden)."""
        return self._proxy.session_id

    @property
    def is_connected(self) -> bool:
        """Ob der Client aktuell verbunden ist."""
        return self._proxy.is_connected

    # ═══════════════════════════════════════════════════
    # LIFECYCLE
    # ═══════════════════════════════════════════════════

    def run(
        self,
        host: str,
        port: int = 5001,
        file_base_url: str = "",
        generated_dir: Path | None = None,
    ) -> None:
        """
        Blockierender Einstiegspunkt.

        Args:
            host: gRPC Server Host
            port: gRPC Server Port
            file_base_url: Blazor-Host URL für Proto-Sync + Dateidownloads
            generated_dir: Verzeichnis der generierten Typen (für Laufzeit-Sync)
        """
        logging.basicConfig(
            level=logging.INFO,
            format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
        )

        if file_base_url and generated_dir:
            if not ensure_types_current(file_base_url, generated_dir):
                log.error("Proto-Synchronisation fehlgeschlagen. Abbruch.")
                return

        log.info(
            "Starting %s (handlers: %s)",
            type(self).__name__,
            ", ".join(t.__name__ for t in self.handle.registered_types),
        )
        asyncio.run(self._run_async(host, port, file_base_url, generated_dir))

    async def _run_async(
        self, host: str, port: int,
        file_base_url: str = "",
        generated_dir: Path | None = None,
    ) -> None:
        """Asynchroner Einstiegspunkt."""

        if file_base_url and generated_dir:
            verify_hash_at_connect(file_base_url, generated_dir)

        capabilities = self._build_capabilities_request()
        await self._connection.connect_with_retry(host, port, capabilities)

        log.info("Connected. Starting processing loops...")

        try:
            await asyncio.gather(
                self._proxy.read_loop(),
                self._router.process_loop(
                    self._proxy,
                    self.handle,
                    self,
                    self._state,
                    self._mapper,
                    self._registry,
                    self._version_tracker,
                ),
                self._connection.monitor(),
            )
        except asyncio.CancelledError:
            log.info("Client shutting down...")
        finally:
            await self._proxy.disconnect()
            log.info("Client stopped")

    # ═══════════════════════════════════════════════════
    # CAPABILITIES
    # ═══════════════════════════════════════════════════

    def _build_capabilities_request(self):
        """
        Scannt handle.registered_types und klassifiziert
        via CategoryRegistry.

        Baut den CapabilitiesRequest mit:
        - message_types: Events (empfangen) + Commands (senden)
        - handle_triggers: Trigger-Typen die dieser Client verarbeitet
        - handle_queries: Query-Typen die dieser Client beantwortet
        """
        subscribe_events: list[str] = []
        handle_triggers: list[str] = []
        handle_queries: list[str] = []

        for registered_type in self.handle.registered_types:
            name = CategoryRegistry.capabilities_name(registered_type)
            try:
                category = self._registry.classify(registered_type)
            except TypeError:
                log.warning("Registered type %s not in CategoryRegistry", registered_type.__name__)
                continue

            if category == MessageCategory.EVENT:
                subscribe_events.append(name)
            elif category == MessageCategory.TRIGGER:
                handle_triggers.append(name)
            elif category == MessageCategory.QUERY:
                handle_queries.append(name)

        # Deklarierte Command-Typen
        send_commands: list[str] = []
        for cmd_type in self._declared_command_types:
            send_commands.append(CategoryRegistry.capabilities_name(cmd_type))

        # CapabilitiesRequest bauen
        cap_cls = getattr(self._gen, "CapabilitiesRequest")
        request = cap_cls(
            message_types=subscribe_events + send_commands,
            handle_triggers=handle_triggers,
            handle_queries=handle_queries,
        )

        log.info(
            "Capabilities: events=%s, commands=%s, triggers=%s, queries=%s",
            subscribe_events, send_commands, handle_triggers, handle_queries
        )

        return request

    # ═══════════════════════════════════════════════════
    # STATE
    # ═══════════════════════════════════════════════════

    def _create_initial_state(self) -> S:
        """
        Erstellt den initialen State.

        Extrahiert den State-Typ aus dem Generic-Parameter
        und instanziiert ihn mit dem Default-Konstruktor.
        """
        state_type = self._resolve_state_type()
        if state_type is None:
            return None  # type: ignore
        return state_type()

    def _resolve_state_type(self) -> type | None:
        """Extrahiert den State-Typ aus CqrsClient[S]."""
        for base in getattr(type(self), '__orig_bases__', ()):
            origin = getattr(base, "__origin__", None)
            if origin is CqrsClient:
                args = get_args(base)
                if args:
                    return args[0]
        return None