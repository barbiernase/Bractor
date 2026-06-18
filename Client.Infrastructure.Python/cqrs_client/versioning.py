# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/versioning.py
"""
Trackt Aggregate-Versionen aus empfangenen Events.

Wird vom MessageRouter nach jedem empfangenen Event aktualisiert.
Wird vom Framework beim Senden von Commands als expected_version verwendet.

Gegenstück zum C# VersioningModule im Blazor-Client.
"""

from __future__ import annotations

from uuid import UUID


class VersionTracker:
    """
    In-Memory Tracker für Aggregate-Versionen.

    Thread-Safety: Nicht nötig — läuft im asyncio Event-Loop (single-threaded).
    """

    def __init__(self):
        self._versions: dict[UUID, int] = {}

    def track(self, aggregate_id: UUID, version: int) -> None:
        """
        Aktualisiert die Version eines Aggregats.

        Nur hochzählen — Events können out-of-order ankommen,
        wir merken uns immer die höchste gesehene Version.
        """
        current = self._versions.get(aggregate_id, -1)
        if version > current:
            self._versions[aggregate_id] = version

    def get_expected_version(self, aggregate_id: UUID) -> int:
        """
        Gibt die erwartete Version für einen Command zurück.

        -1 wenn das Aggregat noch nie gesehen wurde.
        """
        return self._versions.get(aggregate_id, -1)

    def reset(self) -> None:
        """Setzt alle getrackten Versionen zurück (z.B. bei Reconnect)."""
        self._versions.clear()

    @property
    def tracked_count(self) -> int:
        """Anzahl der getrackten Aggregate."""
        return len(self._versions)
