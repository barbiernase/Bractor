# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/connection.py
"""
Reconnect mit exponential Backoff.

Re-sendet CapabilitiesRequest nach Reconnect.
Überwacht die Verbindung und startet automatisch neu.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Any

from .proxy import GrpcProxy

log = logging.getLogger(__name__)


class ConnectionManager:
    """
    Verwaltet die Verbindung mit automatischem Reconnect.

    Exponential Backoff: 1s → 2s → 4s → ... → max 30s
    Reset nach erfolgreicher Verbindung.
    """

    def __init__(
        self,
        proxy: GrpcProxy,
        backoff_base: float = 1.0,
        backoff_max: float = 30.0,
        max_retries: int | None = None,
    ):
        self._proxy = proxy
        self._backoff_base = backoff_base
        self._backoff_max = backoff_max
        self._max_retries = max_retries

        self._host: str = ""
        self._port: int = 0
        self._capabilities_request: Any = None
        self._on_connected: Any = None
        self._retry_count = 0

    async def connect_with_retry(
        self,
        host: str,
        port: int,
        capabilities_request,
        on_connected=None,
    ) -> Any:
        """
        Verbindet mit Retry-Logik.

        Args:
            host: gRPC Server Host
            port: gRPC Server Port
            capabilities_request: CapabilitiesRequestDto
            on_connected: Optionaler Callback nach erfolgreicher Verbindung

        Returns:
            CapabilitiesResponseDto

        Raises:
            ConnectionError: Wenn max_retries erreicht
        """
        self._host = host
        self._port = port
        self._capabilities_request = capabilities_request
        self._on_connected = on_connected

        while True:
            try:
                response = await self._proxy.connect(host, port, capabilities_request)
                self._retry_count = 0
                log.info("Connected successfully")

                if on_connected:
                    await on_connected(response)

                return response

            except Exception as e:
                self._retry_count += 1

                if self._max_retries and self._retry_count >= self._max_retries:
                    log.error(
                        "Max retries (%d) reached. Last error: %s",
                        self._max_retries, e
                    )
                    raise ConnectionError(
                        f"Could not connect after {self._max_retries} attempts"
                    ) from e

                delay = min(
                    self._backoff_base * (2 ** (self._retry_count - 1)),
                    self._backoff_max,
                )
                log.warning(
                    "Connection failed (attempt %d): %s. Retrying in %.1fs",
                    self._retry_count, e, delay
                )
                await asyncio.sleep(delay)

    async def monitor(self) -> None:
        """
        Überwacht die Verbindung und reconnectet automatisch.

        Wartet auf das disconnected-Signal des GrpcProxy,
        dann startet ein neuer connect_with_retry-Zyklus.

        Läuft als paralleler asyncio-Task.
        """
        while True:
            try:
                # Warte bis die Verbindung abbricht
                await self._proxy.disconnected.wait()

                if not self._capabilities_request:
                    log.info("No capabilities request stored, stopping monitor")
                    break

                log.info("Connection lost, starting reconnect...")

                # Cleanup
                await self._proxy.disconnect()

                # Reconnect
                await self.connect_with_retry(
                    self._host,
                    self._port,
                    self._capabilities_request,
                    self._on_connected,
                )

            except asyncio.CancelledError:
                log.info("Connection monitor cancelled")
                break
            except Exception as e:
                log.error("Monitor error: %s", e, exc_info=True)
                break
