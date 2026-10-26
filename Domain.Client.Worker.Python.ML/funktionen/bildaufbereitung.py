"""IBildaufbereitung — Python-Implementierung (Laufort Extern), das Gegenstück zu OpenCvBildaufbereitung im Host: verkleinern auf die
Vorschau-Höhe, Kontrast ausgleichen (Histogramm des Helligkeitskanals), als PNG ablegen."""
from __future__ import annotations

import asyncio
import time
from pathlib import Path

import cv2

from cqrs_client.funktion import Herkunft
from domain_client.generated import BildAufbereitetDto, BildNichtLesbarDto, DateiErkanntDto
from domain_client.generated.funktionen import BildaufbereitungBasis

VORSCHAU_HOEHE = 512


class Bildaufbereitung(BildaufbereitungBasis):
    async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft) -> BildAufbereitetDto | BildNichtLesbarDto:
        bild = await asyncio.to_thread(cv2.imread, e.pfad, cv2.IMREAD_COLOR)
        if bild is None:
            return BildNichtLesbarDto(pfad=e.pfad, grund="Datei nicht lesbar (Python)")
        hoehe_alt, breite_alt = bild.shape[:2]
        breite = max(1, round(breite_alt * VORSCHAU_HOEHE / hoehe_alt))
        klein = await asyncio.to_thread(cv2.resize, bild, (breite, VORSCHAU_HOEHE), interpolation=cv2.INTER_AREA)
        ycrcb = cv2.cvtColor(klein, cv2.COLOR_BGR2YCrCb)
        ycrcb[:, :, 0] = cv2.equalizeHist(ycrcb[:, :, 0])
        aus = cv2.cvtColor(ycrcb, cv2.COLOR_YCrCb2BGR)
        # Wie Zielpfad.In im Host: {AusgabePfad}/{Quelle ohne Endung}_vorschau.png — eine Wiederholung überschreibt dieselbe Datei.
        ausgabe = Path(self.konfig.get("AusgabePfad", ".")).expanduser()
        ausgabe.mkdir(parents=True, exist_ok=True)
        ziel = ausgabe / f"{Path(e.pfad).stem}_vorschau.png"
        await asyncio.to_thread(cv2.imwrite, str(ziel), aus)
        print(f"[Bildaufbereitung] {e.pfad} → {ziel} ({breite}×{VORSCHAU_HOEHE})", flush=True)
        return BildAufbereitetDto(dateiname=e.name, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, vorschau=str(ziel),
                                  breite_pixel=breite, hoehe_pixel=VORSCHAU_HOEHE, erstellt_am=int(time.time() * 1000))
