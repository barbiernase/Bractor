"""IKontrastausgleich — Python-Implementierung (Laufort Extern), das Gegenstück zu OpenCvKontrastausgleich im Host: Schritt 3 der
Bildaufbereitung — das verkleinerte Bild im Kontrast ausgleichen (Histogramm des Helligkeitskanals) und als Vorschau (PNG) ablegen."""
from __future__ import annotations

import asyncio
import time

import cv2

from cqrs_client.funktion import Herkunft
from domain_client.generated import BildVerkleinertDto, VorschauErstelltDto
from domain_client.generated.funktionen import KontrastausgleichBasis
from funktionen.bild_verkleinerung import zielpfad


class Kontrastausgleich(KontrastausgleichBasis):
    async def auf_bild_verkleinert(self, e: BildVerkleinertDto, h: Herkunft) -> VorschauErstelltDto:
        klein = await asyncio.to_thread(cv2.imread, e.verkleinert, cv2.IMREAD_COLOR)
        if klein is None:
            raise OSError(f"{e.verkleinert} ist nicht mehr lesbar")
        ycrcb = cv2.cvtColor(klein, cv2.COLOR_BGR2YCrCb)
        ycrcb[:, :, 0] = cv2.equalizeHist(ycrcb[:, :, 0])
        aus = cv2.cvtColor(ycrcb, cv2.COLOR_YCrCb2BGR)
        ziel = zielpfad(self.konfig, e.pfad, "vorschau")
        await asyncio.to_thread(cv2.imwrite, str(ziel), aus)
        hoehe, breite = aus.shape[:2]
        print(f"[Kontrastausgleich] {e.verkleinert} → {ziel}", flush=True)
        return VorschauErstelltDto(dateiname=e.dateiname, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, vorschau=str(ziel),
                                   breite_pixel=breite, hoehe_pixel=hoehe, erstellt_am=int(time.time() * 1000))
