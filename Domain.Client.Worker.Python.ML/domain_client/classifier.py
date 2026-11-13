# REPO-PFAD: Domain.Client.Worker.Python.ML/domain_client/classifier.py
"""
KI-Classifier für industrielle Bildinspektion — der Client KlassifikationsWorker (verkörpert den Akteur Klassifizierer).

Programmiert gegen seinen Client-Vertrag (Domain.Akteure.KlassifikationsWorker, docs/konzept-akteure.md §4): sein Akteur Klassifizierer
reagiert auf ImagePairKomplett (ein Fakt, Schritt 6) mit seinem Urteil; davor merkt er sich den Pfad jedes Bildes (BildVerfuegbar, im Log
davor) und lädt ein neu freigegebenes Modell (ModellAktiviert). Die generierte Basis `KlassifikationsAppBasis` verdrahtet den Dispatch, prüft
jede Reaktion gegen das IDarf des Akteurs und meldet Client + Hash am Handshake; genau einmal wirkt sie mit einem Stand (`reaktion_stand`).
Fehlt eine Methode, startet der Worker nicht.
"""

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass, field
from uuid import UUID

import torch

from domain_client.generated import (
    BildVerfuegbarDto,
    ImagePairKomplettDto,
    KlassifiziereBildPaarDurchKiDto,
    ModellAktiviertDto,
)
from domain_client.generated.vertraege import KlassifikationsAppBasis
from domain_client.image_loader import download_and_convert

log = logging.getLogger(__name__)

VERSION_NAMES = {0: "dc0", 1: "dc2"}
KEINE_ANOMALIE = 0
QUESTIONABLE = 1
ANOMALIE = 2


@dataclass
class BildInfo:
    version: int
    version_name: str
    pfad: str


@dataclass
class ClassifierState:
    bilder_empfangen: int = 0
    pairs_klassifiziert: int = 0
    downloads_ok: int = 0
    downloads_failed: int = 0
    bilder: dict[UUID, dict[int, BildInfo]] = field(default_factory=dict)
    aktives_modell: str = ""


class ImageClassifier(KlassifikationsAppBasis[ClassifierState]):

    def __init__(self, registry, nutzlast, config: dict):
        super().__init__(registry, nutzlast, config)
        self._file_base_url = config["file_base_url"]
        self._device = torch.device(
            config.get("device", "cuda" if torch.cuda.is_available() else "cpu")
        )
        self._model = self._load_model(config["model_path"])
        log.info("Model loaded on %s", self._device)

    def _load_model(self, path: str):
        log.info("Loading model: %s", path)
        model = torch.jit.load(path, map_location=self._device)
        model.eval()
        return model

    async def auf_bild_verfuegbar(
        self, event: BildVerfuegbarDto, ctx, state: ClassifierState
    ):
        state.bilder_empfangen += 1
        agg_id = ctx.aggregate_id
        if agg_id not in state.bilder:
            state.bilder[agg_id] = {}
        version_name = VERSION_NAMES.get(event.version, f"v{event.version}")
        state.bilder[agg_id][event.version] = BildInfo(
            version=event.version, version_name=version_name, pfad=event.pfad,
        )
        log.info("BildVerfuegbar: %s %s", str(agg_id)[:8], version_name)

    async def auf_image_pair_komplett(
        self, event: ImagePairKomplettDto, ctx, state: ClassifierState
    ) -> KlassifiziereBildPaarDurchKiDto | None:
        agg_id = ctx.aggregate_id
        pair_bilder = state.bilder.pop(agg_id, {})
        log.info("ImagePairKomplett: %s", agg_id)

        tensors: dict[str, torch.Tensor] = {}
        for version_int in [0, 1]:
            version_name = VERSION_NAMES[version_int]
            info = pair_bilder.get(version_int)
            if info is None:
                state.downloads_failed += 1
                continue
            img, tensor = await download_and_convert(
                self._file_base_url, info.pfad, self._device
            )
            if not img.success:
                log.error("  %s: %s", version_name, img.error)
                state.downloads_failed += 1
            else:
                state.downloads_ok += 1
                tensors[version_name] = tensor
                log.info("  %s: %dx%d, %.0f KB", version_name, img.width, img.height, img.size_bytes / 1024)

        if "dc0" in tensors and "dc2" in tensors:
            label = await self._classify_pair(tensors["dc0"], tensors["dc2"])
        elif tensors:
            label = KEINE_ANOMALIE
        else:
            # Ohne Bilder lässt sich nicht urteilen: das Paar bleibt unbeurteilt (ein Fehler hielte jede spätere Reaktion an).
            log.error("Paar %s: keine Bilder geladen — nicht beurteilt", agg_id)
            return None

        state.pairs_klassifiziert += 1
        log.info("  → Klassifikation: %d [#%d]", label, state.pairs_klassifiziert)
        return KlassifiziereBildPaarDurchKiDto(aggregate_id=str(agg_id), label=label)

    async def auf_modell_aktiviert(
        self, event: ModellAktiviertDto, ctx, state: ClassifierState
    ):
        """Ein neues Modell ist freigegeben → laden. Schlägt das fehl, bleibt das bisherige aktiv."""
        log.info("ModellAktiviert: %s (%s)", event.name, event.pfad)
        try:
            self._model = await asyncio.to_thread(self._load_model, event.pfad)
            state.aktives_modell = event.pfad
        except Exception as e:                        # noqa: BLE001 — altes Modell weiter benutzen
            log.error("Modell %s nicht geladen, bisheriges bleibt aktiv: %s", event.pfad, e)

    async def _classify_pair(self, dc0: torch.Tensor, dc2: torch.Tensor) -> int:
        def _infer():
            with torch.no_grad():
                batch = torch.stack([dc0, dc2]).unsqueeze(0)
                return int(self._model(batch).argmax(dim=1).item())
        return await asyncio.to_thread(_infer)
