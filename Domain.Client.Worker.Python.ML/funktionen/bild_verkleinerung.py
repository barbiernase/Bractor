"""IBildVerkleinerung — Python-Implementierung (Laufort Extern), das Gegenstück zu OpenCvBildVerkleinerung im Host: Schritt 2 der
Bildaufbereitung — die Quelle auf die Vorschau-Höhe verkleinern und als Zwischenbild (PNG) ablegen."""
from __future__ import annotations

import asyncio
from pathlib import Path

import cv2

from cqrs_client.funktion import Herkunft
from domain_client.generated import BildEingelesenDto, BildVerkleinertDto
from domain_client.generated.funktionen import BildVerkleinerungBasis

VORSCHAU_HOEHE = 512


def zielpfad(konfig: dict, quelle: str, zusatz: str) -> Path:
    """Wie Zielpfad.In im Host: {AusgabePfad}/{Quelle ohne Endung}_{zusatz}.png — eine Wiederholung überschreibt dieselbe Datei."""
    ausgabe = Path(konfig.get("AusgabePfad", ".")).expanduser()
    ausgabe.mkdir(parents=True, exist_ok=True)
    return ausgabe / f"{Path(quelle).stem}_{zusatz}.png"


class BildVerkleinerung(BildVerkleinerungBasis):
    async def auf_bild_eingelesen(self, e: BildEingelesenDto, h: Herkunft) -> BildVerkleinertDto:
        bild = await asyncio.to_thread(cv2.imread, e.pfad, cv2.IMREAD_COLOR)
        if bild is None:
            raise OSError(f"{e.pfad} ist nicht mehr lesbar")
        hoehe_alt, breite_alt = bild.shape[:2]
        breite = max(1, round(breite_alt * VORSCHAU_HOEHE / hoehe_alt))
        klein = await asyncio.to_thread(cv2.resize, bild, (breite, VORSCHAU_HOEHE), interpolation=cv2.INTER_AREA)
        ziel = zielpfad(self.konfig, e.pfad, "klein")
        await asyncio.to_thread(cv2.imwrite, str(ziel), klein)
        print(f"[BildVerkleinerung] {e.pfad} → {ziel} ({breite}×{VORSCHAU_HOEHE})", flush=True)
        return BildVerkleinertDto(dateiname=e.dateiname, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, verkleinert=str(ziel),
                                  breite_pixel=breite, hoehe_pixel=VORSCHAU_HOEHE)
