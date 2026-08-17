# REPO-PFAD: Domain.Client.Worker.Python.ML/domain_client/training_worker.py
"""
TrainingWorker — der out-of-process Trainings-Job (Konzept §4.2).

Derselbe Event→Command-Reaktor wie der Classifier, nur LANGLAUFEND mit Fortschritt:
subscribed `TrainingAngefordert`, zieht die eingefrorene Sample-Liste über den typisierten
Query-Kanal (`self.query(HoleDatensatzSamples…)`, M7), fährt das Training in
`asyncio.to_thread` (Event-Loop bleibt frei) und **yieldet über die Zeit mehrfach** Commands
zurück:

    MeldeTrainingBegonnen                         → TrainingBegonnen
    MeldeFortschritt(epoche, loss, genauigkeit) ×N → TrainingFortschritt   (Live-Kurve)
    MeldeTrainingAbgeschlossen(modellPfad, …)     → TrainingAbgeschlossen
  bei Exception:
    MeldeTrainingGescheitert(grund)               → TrainingGescheitert

Der Trainings-Thread schiebt Fortschritt über eine `asyncio.Queue` (thread-sicher via
`call_soon_threadsafe`); der Handler drainiert sie und yieldet. Kooperativer Abbruch:
`TrainingAbgebrochen` setzt ein `threading.Event`, das der Thread zwischen Epochen prüft.

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

from cqrs_client import CqrsClient, handle

from domain_client.generated import (
    TrainingAngefordertDto,
    TrainingAbgebrochenDto,
    HoleDatensatzSamplesDto,
    DatensatzSampleDto,
    EpochenMetrikDto,
    EndmetrikenDto,
    MeldeTrainingBegonnenDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingGescheitertDto,
)

log = logging.getLogger(__name__)


@dataclass
class TrainingState:
    laeufe: int = 0
    # Abbruch-Flag je laufendem Training (gesetzt vom TrainingAbgebrochen-Handler).
    abbruch: dict[str, threading.Event] = field(default_factory=dict)


class TrainingWorker(CqrsClient[TrainingState]):

    _declared_command_types = [
        MeldeTrainingBegonnenDto,
        MeldeFortschrittDto,
        MeldeTrainingAbgeschlossenDto,
        MeldeTrainingGescheitertDto,
    ]

    # Seiten-Größe beim Ziehen der Samples (paginiert, wie SucheImagePairs).
    SEITEN_GROESSE = 500

    def __init__(self, registry, generated_module, config: dict | None = None):
        super().__init__(registry, generated_module, config)
        cfg = config or {}
        # Stub-Trainer: Sekunden je Epoche (0 in Tests → sofort).
        self._epoch_seconds: float = float(cfg.get("epoch_seconds", 0.3))
        self._modell_verzeichnis: str = cfg.get("modell_verzeichnis", "modelle")

    # ═══════════════════════════════════════════════════
    # HAUPT-HANDLER — der langlaufende Trainings-Job
    # ═══════════════════════════════════════════════════

    @handle.register
    async def on_training_angefordert(
        self, event: TrainingAngefordertDto, ctx, state: TrainingState
    ):
        training_id = str(ctx.aggregate_id)
        state.laeufe += 1
        log.info(
            "Training %s angefordert: Datensatz %s v%d, %d Epochen",
            training_id[:8], str(event.datensatz_id)[:8],
            event.datensatz_version, event.hyperparameter.epochen,
        )

        # ── 1. Samples über den Query-Kanal ziehen (paginiert, M7) ──
        try:
            samples = await self._hole_alle_samples(
                event.datensatz_id, event.datensatz_version
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
                self._trainiere(training_id, event.hyperparameter, samples, abbruch, emit)
            except Exception as e:                    # noqa: BLE001 — Fehler an den Lauf melden
                emit(("gescheitert", str(e)))

        train_task = asyncio.create_task(asyncio.to_thread(train))

        # ── 4. Queue drainen und yielden, bis ein terminales Item kommt ──
        try:
            while True:
                art, *rest = await queue.get()
                if art == "fortschritt":
                    yield MeldeFortschrittDto(aggregate_id=training_id, metrik=rest[0])
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
    # ABBRUCH — kooperativ (Konzept §4.3)
    # ═══════════════════════════════════════════════════

    @handle.register
    async def on_training_abgebrochen(
        self, event: TrainingAbgebrochenDto, ctx, state: TrainingState
    ):
        ev = state.abbruch.get(str(ctx.aggregate_id))
        if ev is not None:
            ev.set()   # der Trainings-Thread prüft das zwischen Epochen

    # ═══════════════════════════════════════════════════
    # SAMPLES ZIEHEN — der Query-Kanal (M7), paginiert
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
