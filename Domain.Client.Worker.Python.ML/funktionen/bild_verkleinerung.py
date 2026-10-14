"""IBildVerkleinerung — Python-Implementierung (Laufort Extern), das Gegenstück zu OpenCvBildVerkleinerung im Host."""
from __future__ import annotations

import asyncio
from pathlib import Path

import cv2

from cqrs_client.funktion import Ausfuehrung
from domain_client.generated import BildNichtLesbarDto, BildVerkleinertDto, VerkleinereBildDto
from domain_client.generated.funktionen import BildVerkleinerungBasis


class BildVerkleinerung(BildVerkleinerungBasis):
    async def rufe(self, auftrag: VerkleinereBildDto, x: Ausfuehrung) -> BildVerkleinertDto | BildNichtLesbarDto:
        bild = await asyncio.to_thread(cv2.imread, auftrag.quell_pfad, cv2.IMREAD_COLOR)
        if bild is None:
            return BildNichtLesbarDto(pfad=auftrag.quell_pfad, grund="Datei nicht lesbar (Python)")
        h, w = bild.shape[:2]
        hoehe = max(1, auftrag.hoehe)
        breite = max(1, round(w * hoehe / h))
        klein = await asyncio.to_thread(cv2.resize, bild, (breite, hoehe), interpolation=cv2.INTER_AREA)
        # Wie Zielpfad.In im Host: {AusgabePfad}/{Quelle ohne Endung}_klein.png — eine Wiederholung überschreibt dieselbe Datei.
        ausgabe = Path(self.konfig.get("AusgabePfad", ".")).expanduser()
        ausgabe.mkdir(parents=True, exist_ok=True)
        ziel = ausgabe / f"{Path(auftrag.quell_pfad).stem}_klein.png"
        await asyncio.to_thread(cv2.imwrite, str(ziel), klein)
        print(f"[BildVerkleinerung] {auftrag.quell_pfad} → {ziel} ({breite}×{hoehe})", flush=True)
        return BildVerkleinertDto(pfad=str(ziel), breite_pixel=breite, hoehe_pixel=hoehe)
