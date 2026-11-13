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
import os
import logging
from typing import Any, ClassVar, Generic, TypeVar, get_args

from . import leitung
from .connection import ConnectionManager
from .dispatch import HandlerBase, handle
from .mapper import PayloadMapper
from .proto_sync import ensure_types_current, verify_hash_at_connect
from .proxy import GrpcProxy
from .registry import CategoryRegistry, MessageCategory
from .reaktionen import DateiReaktionsStand, ReaktionsLaeufer, ReaktionUnterbrochen
from .router import MessageRouter
from .versioning import VersionTracker

log = logging.getLogger(__name__)

S = TypeVar("S")


class CqrsClient(HandlerBase, Generic[S]):
    """
    Basisklasse für Python First-Citizen Clients.

    Lifecycle:
        run() → _build_capabilities_request() → connect() → je Verbindung eine Sitzung, parallel:
            - read_loop (GrpcProxy)
            - process_loop (MessageRouter)
          endet die Sitzung (Verbindung weg) → connect_with_retry (Backoff) → nächste Sitzung
    """

    # Subklassen überschreiben diese mit ihren Command-Typen
    _declared_command_types: ClassVar[list[type]] = []

    def __init__(
        self,
        registry: CategoryRegistry,
        nutzlast,
        config: dict[str, Any] | None = None,
    ):
        """
        Args:
            registry: CategoryRegistry mit allen Domain-Typen
            nutzlast: Die generierte Nutzlast-Tabelle typ → DTO-Klasse (domain_client.generated.nutzlast_tabelle.TABELLE);
                      die Leitung kommt aus cqrs_client.leitung
            config: Optionale Konfiguration für die Subklasse
        Reaktionen (Schritt 6): nennt die Konfig `reaktion_stand` (ein Verzeichnis, auch `CQRS_REAKTION_STAND`), reagieren die Handler dauerhaft —
        der Client liest das Log ab seinem gesicherten Stand, die Command-Ids leitet er ab (cqrs_client.reaktionen); `reaktion_name` (Standard:
        der Klassenname) ist der Schlüssel von Stand und Ids. Ohne sie reagieren die Handler nur auf Pushes, solange sie verbunden sind.
        """
        self._registry = registry
        self._config = config or {}

        self._proxy = GrpcProxy(
            akteur_token=self._config.get("akteur_token") or os.environ.get("CQRS_AKTEUR_TOKEN"),
        )
        self._mapper = PayloadMapper(nutzlast)
        self._router = MessageRouter()
        self._connection = ConnectionManager(self._proxy)
        self._version_tracker = VersionTracker()
        self._state: S = self._create_initial_state()
        stand = self._config.get("reaktion_stand") or os.environ.get("CQRS_REAKTION_STAND")
        self._reaktionen: ReaktionsLaeufer | None = None if not stand else ReaktionsLaeufer(
            self._proxy, self._mapper, self._registry, self.handle, self, self._state, self._version_tracker, DateiReaktionsStand(stand),
            self._config.get("reaktion_name") or type(self).__name__)

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
    # ASK-SEITE — Queries stellen (docs/09 §9.4)
    # ═══════════════════════════════════════════════════

    async def query(self, query_dto):
        """
        Stellt eine Query und liefert die TYPISIERTE Antwort — die öffentliche Ask-API, die
        bisher fehlte (docs/09 §9.4). Spiegelbild zum send_command-Pfad:

          1. wrap_query      → Nutzlast(typ, daten)
          2. proxy.send_query → Korrelation via Future, 30s Timeout
          3. extract_query_response → konkrete Antwort aus QueryResponse.payload (Nutzlast)

        Fehler vom Server (QueryFailed) kommen als proxy.ServerError-Exception (Korrelation im
        Read-Loop). Für read-only Datensatz-/Trainings-Queries ist KEIN Deps/Versions-Tracking
        nötig — Python schreibt nichts versionsabhängig.

        Beispiel (im TrainingWorker):
            antwort = await self.query(
                HoleDatensatzSamplesDto(datensatz_id=str(ds_id), version=1, seite=1))
        """
        nutzlast = self._mapper.wrap_query(query_dto)
        raw = await self._proxy.send_query(nutzlast)
        return self._mapper.extract_query_response(raw)

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

        try:
            # Je Verbindung EINE Sitzung (Lesen + Verarbeiten). Beide Schleifen enden, wenn die Verbindung abbricht —
            # dann neu verbinden und eine neue Sitzung starten. (Früher lief nur der Monitor weiter: er verband neu, aber niemand
            # las mehr — der Client war nach dem ersten Abbruch taub.)
            while True:
                log.info("Connected. Starting processing loops...")
                await self._sitzung()
                log.info("Connection lost, starting reconnect...")
                await self._proxy.disconnect()
                await self._connection.connect_with_retry(host, port, capabilities)
        except asyncio.CancelledError:
            log.info("Client shutting down...")
        finally:
            await self._proxy.disconnect()
            log.info("Client stopped")

    async def _sitzung(self) -> None:
        """Eine Verbindung lang: Server-Nachrichten lesen und verarbeiten — endet, wenn die Verbindung abbricht."""
        if self._reaktionen is not None:
            await self._sitzung_mit_reaktionen()
            return
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
        )

    async def _sitzung_mit_reaktionen(self) -> None:
        """Eine Verbindung lang mit dauerhaften Reaktionen: der Läufer liest das Log, die Pushes wecken ihn und bringen die Antworten."""
        laeufer = self._reaktionen
        self._proxy.fehler_hoerer = laeufer.fehler

        async def verteile() -> None:
            while self._proxy.is_connected:
                try:
                    art, nachricht = await asyncio.wait_for(self._proxy.event_queue.get(), timeout=1.0)
                except asyncio.TimeoutError:
                    continue
                if art == "event":
                    laeufer.angekommen(nachricht)

        ergebnisse = await asyncio.gather(self._proxy.read_loop(), laeufer.lauf(), verteile(), return_exceptions=True)
        for e in ergebnisse:
            if isinstance(e, Exception) and not isinstance(e, ReaktionUnterbrochen):
                log.error("Reaktionen: %s", e, exc_info=e)

    # ═══════════════════════════════════════════════════
    # CAPABILITIES
    # ═══════════════════════════════════════════════════

    def _build_capabilities_request(self):
        """
        Scannt handle.registered_types und klassifiziert
        via CategoryRegistry.

        Baut den CapabilitiesRequest mit message_types: Events (empfangen) + Commands (senden). Queries beantwortet ein Client
        nie — eine Query hat genau ein Ziel, einen Reader oder ein Aggregat (docs/10 §10.5).
        """
        subscribe_events: list[str] = []

        for registered_type in self.handle.registered_types:
            try:
                category = self._registry.classify(registered_type)
            except TypeError:
                log.warning("Registered type %s not in CategoryRegistry", registered_type.__name__)
                continue
            name = self._mapper.tabelle.typ(registered_type)   # nicht auf der Leitung → TypeError (laut, nie still)

            if category == MessageCategory.EVENT:
                subscribe_events.append(name)
            elif category == MessageCategory.QUERY:
                raise TypeError(f"{registered_type.__name__} ist eine Query — ein Client beantwortet keine Queries (docs/10 §10.5)")

        # Deklarierte Command-Typen
        send_commands: list[str] = []
        for cmd_type in self._declared_command_types:
            send_commands.append(self._mapper.tabelle.typ(cmd_type))

        # CapabilitiesRequest bauen
        request = leitung.CapabilitiesRequest(
            message_types=subscribe_events + send_commands,
        )

        log.info("Capabilities: events=%s, commands=%s", subscribe_events, send_commands)

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
        """
        Extrahiert den State-Typ aus CqrsClient[S] — auch über Zwischenklassen (z. B. eine generierte
        Vertragsbasis: class Worker(KlassifikationsAppBasis[MeinState])): die erste konkrete Typ-Angabe in der MRO.
        """
        for klasse in type(self).__mro__:
            for base in klasse.__dict__.get('__orig_bases__', ()):
                origin = getattr(base, "__origin__", None)
                if isinstance(origin, type) and issubclass(origin, CqrsClient):
                    args = get_args(base)
                    if args and not isinstance(args[0], TypeVar):
                        return args[0]
        return None