# REPO-PFAD: Domain.Client.Worker.Python.ML/domain_client/training_worker.py
"""
TrainingWorker — der out-of-process Trainings-Job.

Der Client TrainingsWorker (verkörpert den Akteur TrainingsSystem) reagiert auf TrainingAngefordert (ein Fakt, Schritt 6): er zieht die
eingefrorene Sample-Liste über den typisierten Query-Kanal (`self.query(HoleDatensatzSamples…)`), fährt das Training in `asyncio.to_thread`
(Event-Loop bleibt frei) und **yieldet über die Zeit mehrfach** Commands — Stücke: jedes geht hinaus, bevor das nächste kommt (genau einmal über
den Stand der Reaktionen, die Ids leitet der Läufer aus Position und Stelle ab):

    MeldeTrainingBegonnen                         → TrainingBegonnen
    MeldeFortschritt(epoche, loss, genauigkeit) ×N → TrainingFortschritt   (Live-Kurve)
    MeldeTrainingAbgeschlossen(modellPfad, …)     → TrainingAbgeschlossen
  bei Exception:
    MeldeTrainingGescheitert(grund)               → TrainingGescheitert

Der Trainings-Thread schiebt Fortschritt über eine `asyncio.Queue` (thread-sicher via
`call_soon_threadsafe`); der Handler drainiert sie und yieldet. Kooperativer Abbruch: nach jeder
Epoche liest der Handler den Lauf (`HoleTrainingslauf`); steht er auf „abgebrochen“, setzt er ein
`threading.Event`, das der Thread zwischen Epochen prüft.

Diese Klasse enthält einen **Stub-Trainer** (`_trainiere`) — fake, aber realitätsnah
(sinkender Loss, steigende Genauigkeit). Für echtes Torch die Methode überschreiben; der
gesamte Nachrichten-/Fortschritts-Fluss bleibt identisch.
"""

from __future__ import annotations

import asyncio
import logging
import threading
import time
from dataclasses import dataclass, field
from typing import Callable

from domain_client.generated import (
    TrainingAngefordertDto,
    HoleDatensatzSamplesDto,
    HoleTrainingslaufDto,
    TrainingslaufAntwortDto,
    DatensatzSampleDto,
    EpochenMetrikDto,
    EndmetrikenDto,
    MeldeTrainingBegonnenDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingGescheitertDto,
)
from domain_client.generated.vertraege import TrainingsWorkerBasis

log = logging.getLogger(__name__)

# TrainingsStatus (Domain/Trainingslauf/Enums.cs) auf der Leitung: die Stelle im Enum.
STATUS_ABGEBROCHEN = 4


@dataclass
class TrainingState:
    laeufe: int = 0
    # Abbruch-Flag je laufendem Training (gesetzt, wenn der Lauf auf „abgebrochen“ steht).
    abbruch: dict[str, threading.Event] = field(default_factory=dict)


class TrainingWorker(TrainingsWorkerBasis[TrainingState]):
    """Der Client TrainingsWorker — programmiert gegen seinen Vertrag (Domain.Akteure.TrainingsWorker: reagiert auf TrainingAngefordert)."""

    # Seiten-Größe beim Ziehen der Samples (paginiert, wie SucheImagePairs).
    SEITEN_GROESSE = 500

    def __init__(self, registry, nutzlast, config: dict | None = None):
        super().__init__(registry, nutzlast, config)
        cfg = config or {}
        # Stub-Trainer: Sekunden je Epoche (0 in Tests → sofort).
        self._epoch_seconds: float = float(cfg.get("epoch_seconds", 0.3))
        self._modell_verzeichnis: str = cfg.get("modell_verzeichnis", "modelle")

    # ═══════════════════════════════════════════════════
    # HAUPT-HANDLER — der langlaufende Trainings-Job
    # ═══════════════════════════════════════════════════

    async def auf_training_angefordert(
        self, c: TrainingAngefordertDto, ctx, state: TrainingState
    ):
        training_id = str(ctx.aggregate_id)
        state.laeufe += 1
        log.info(
            "Training %s angefordert: Datensatz %s v%d, %d Epochen",
            training_id[:8], str(c.datensatz_id)[:8],
            c.datensatz_version, c.hyperparameter.epochen,
        )

        # ── 1. Samples über den Query-Kanal ziehen (paginiert) ──
        try:
            samples = await self._hole_alle_samples(
                c.datensatz_id, c.datensatz_version
            )
        except Exception as e:                        # noqa: BLE001 — an den Lauf zurückmelden
            log.error("Samples laden fehlgeschlagen: %s", e)
            yield MeldeTrainingGescheitertDto(
                aggregate_id=training_id, grund=f"Samples laden: {e}")
            return

        if not samples:
            yield MeldeTrainingGescheitertDto(
                aggregate_id=training_id, grund="Datensatz-Version enthält keine Samples")
            return

        log.info("Training %s: %d Samples geladen", training_id[:8], len(samples))

        # ── 2. Beginn melden ──
        yield MeldeTrainingBegonnenDto(aggregate_id=training_id)

        # ── 3. Training im Thread, Fortschritt über eine Queue ──
        abbruch = threading.Event()
        state.abbruch[training_id] = abbruch
        queue: asyncio.Queue = asyncio.Queue()
        loop = asyncio.get_running_loop()

        def emit(item: tuple) -> None:
            loop.call_soon_threadsafe(queue.put_nowait, item)

        def train() -> None:
            try:
                self._trainiere(training_id, c.hyperparameter, samples, abbruch, emit)
            except Exception as e:                    # noqa: BLE001 — Fehler an den Lauf melden
                emit(("gescheitert", str(e)))

        train_task = asyncio.create_task(asyncio.to_thread(train))

        # ── 4. Queue drainen und yielden, bis ein terminales Item kommt ──
        try:
            while True:
                art, *rest = await queue.get()
                if art == "fortschritt":
                    yield MeldeFortschrittDto(aggregate_id=training_id, metrik=rest[0])
                    if await self._abgebrochen(training_id):
                        # Der Lauf steht schon auf „abgebrochen“ (per GUI): kooperativ aufhören, nichts mehr melden — der Thread prüft
                        # das Flag zwischen Epochen, `finally` wartet auf ihn.
                        log.info("Training %s abgebrochen", training_id[:8])
                        abbruch.set()
                        break
                elif art == "fertig":
                    modell_pfad, endmetriken = rest
                    yield MeldeTrainingAbgeschlossenDto(
                        aggregate_id=training_id,
                        modell_pfad=modell_pfad,
                        endmetriken=endmetriken,
                    )
                    break
                elif art == "gescheitert":
                    yield MeldeTrainingGescheitertDto(
                        aggregate_id=training_id, grund=rest[0])
                    break
                elif art == "abgebrochen":
                    # Das Aggregat wurde per GUI schon auf Abgebrochen gesetzt — der Worker
                    # stoppt nur kooperativ, ohne ein weiteres Event zu melden.
                    log.info("Training %s kooperativ abgebrochen", training_id[:8])
                    break
        finally:
            state.abbruch.pop(training_id, None)
            await train_task

    # ═══════════════════════════════════════════════════
    # ABBRUCH — kooperativ, über den Stand des Laufs
    # ═══════════════════════════════════════════════════

    async def _abgebrochen(self, training_id: str) -> bool:
        """Steht der Lauf auf „abgebrochen“ (per GUI)? Ein Fehler beim Lesen bricht nicht ab — die nächste Epoche fragt wieder."""
        try:
            antwort = await self.query(HoleTrainingslaufDto(trainingslauf_id=training_id))
        except Exception as e:                        # noqa: BLE001 — weiterrechnen
            log.warning("Training %s: Stand nicht gelesen: %s", training_id[:8], e)
            return False
        return isinstance(antwort, TrainingslaufAntwortDto) and antwort.status == STATUS_ABGEBROCHEN

    # ═══════════════════════════════════════════════════
    # SAMPLES ZIEHEN — der Query-Kanal, paginiert
    # ═══════════════════════════════════════════════════

    async def _hole_alle_samples(
        self, datensatz_id: str, version: int
    ) -> list[DatensatzSampleDto]:
        samples: list[DatensatzSampleDto] = []
        seite = 1
        while True:
            antwort = await self.query(HoleDatensatzSamplesDto(
                datensatz_id=str(datensatz_id),
                version=version,
                seite=seite,
                seiten_groesse=self.SEITEN_GROESSE,
            ))
            samples.extend(antwort.samples)
            if not antwort.samples or len(samples) >= antwort.gesamt_anzahl:
                break
            seite += 1
        return samples

    # ═══════════════════════════════════════════════════
    # STUB-TRAINER (im Thread) — für echtes Torch überschreiben
    # ═══════════════════════════════════════════════════

    def _trainiere(
        self,
        training_id: str,
        hyperparameter,
        samples: list[DatensatzSampleDto],
        abbruch: threading.Event,
        emit: Callable[[tuple], None],
    ) -> None:
        """
        Läuft im Worker-Thread (nicht auf dem Event-Loop). Simuliert ein Training:
        je Epoche ein Fortschritts-Punkt (sinkender Loss, steigende Genauigkeit), am Ende
        ein Modell-Pfad + Endmetriken. Prüft zwischen Epochen das Abbruch-Flag.
        """
        epochen = max(1, int(hyperparameter.epochen))

        for epoche in range(1, epochen + 1):
            if abbruch.is_set():
                emit(("abgebrochen",))
                return

            if self._epoch_seconds > 0:
                time.sleep(self._epoch_seconds)

            fortschritt = epoche / epochen
            loss = round(1.0 * (1.0 - fortschritt) + 0.05, 4)
            genauigkeit = round(0.5 + 0.49 * fortschritt, 4)
            emit(("fortschritt", EpochenMetrikDto(
                epoche=epoche, loss=loss, genauigkeit=genauigkeit)))

        modell_pfad = f"{self._modell_verzeichnis}/{training_id}.pt"
        endmetriken = EndmetrikenDto(loss=round(0.05, 4), genauigkeit=round(0.99, 4))
        emit(("fertig", modell_pfad, endmetriken))
