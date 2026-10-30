"""IBildEinlesen — Python-Implementierung (Laufort Extern), das Gegenstück zu OpenCvBildEinlesen im Host: Schritt 1 der Bildaufbereitung —
die erkannte Datei als Farbbild lesen und ihre Maße nennen; eine unlesbare Datei beendet die Kette (BildNichtLesbar)."""
from __future__ import annotations

import asyncio

import cv2

from cqrs_client.funktion import Herkunft
from domain_client.generated import BildEingelesenDto, BildNichtLesbarDto, DateiErkanntDto
from domain_client.generated.funktionen import BildEinlesenBasis


class BildEinlesen(BildEinlesenBasis):
    async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft) -> BildEingelesenDto | BildNichtLesbarDto:
        bild = await asyncio.to_thread(cv2.imread, e.pfad, cv2.IMREAD_COLOR)
        if bild is None:
            return BildNichtLesbarDto(pfad=e.pfad, grund="Datei nicht lesbar (Python)")
        hoehe, breite = bild.shape[:2]
        print(f"[BildEinlesen] {e.pfad} ({breite}×{hoehe})", flush=True)
        return BildEingelesenDto(dateiname=e.name, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, breite_pixel=breite, hoehe_pixel=hoehe)
