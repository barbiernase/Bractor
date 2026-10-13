"""IHistogrammAusgleich — Python-Implementierung (Laufort Extern), das Gegenstück zu OpenCvHistogrammAusgleich im Host."""
from __future__ import annotations

import asyncio
import time
from pathlib import Path

import cv2

from cqrs_client.funktion import Ausfuehrung
from domain_client.generated import BildNichtLesbarDto, GleicheHistogrammAusDto, HistogrammAusgeglichenDto
from domain_client.generated.funktionen import HistogrammAusgleichBasis


class HistogrammAusgleich(HistogrammAusgleichBasis):
    async def rufe(self, auftrag: GleicheHistogrammAusDto, x: Ausfuehrung) -> HistogrammAusgeglichenDto | BildNichtLesbarDto:
        bild = await asyncio.to_thread(cv2.imread, auftrag.quell_pfad, cv2.IMREAD_COLOR)
        if bild is None:
            return BildNichtLesbarDto(pfad=auftrag.quell_pfad, grund="Datei nicht lesbar (Python)")
        ycrcb = cv2.cvtColor(bild, cv2.COLOR_BGR2YCrCb)
        ycrcb[:, :, 0] = cv2.equalizeHist(ycrcb[:, :, 0])
        aus = cv2.cvtColor(ycrcb, cv2.COLOR_YCrCb2BGR)
        ausgabe = Path(self.konfig.get("OutputPath", ".")).expanduser()
        ausgabe.mkdir(parents=True, exist_ok=True)
        ziel = ausgabe / f"{Path(auftrag.quell_pfad).stem}_ausgeglichen.png"
        await asyncio.to_thread(cv2.imwrite, str(ziel), aus)
        h, w = aus.shape[:2]
        print(f"[HistogrammAusgleich] {auftrag.quell_pfad} → {ziel}", flush=True)
        return HistogrammAusgeglichenDto(pfad=str(ziel), breite_pixel=w, hoehe_pixel=h, erstellt_am=int(time.time() * 1000))
