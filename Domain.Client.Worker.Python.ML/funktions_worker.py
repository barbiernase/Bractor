#!/usr/bin/env python3
# REPO-PFAD: Domain.Client.Worker.Python.ML/funktions_worker.py
"""
Die Katalog-Funktionen der Bildaufbereitung in Python (Laufort Extern) — das Gegenstück zu den OpenCV-Implementierungen im Host
(Domain.Pipeline/ImageProcessing/OpenCvBildaufbereitung.cs). Gestartet von der Werkbank („▶ Worker starten" an der Funktions-Karte,
Umgebung: Funktionen:<F>:Worker) oder von Hand:

    python funktions_worker.py --ausgabe <verzeichnis> [--slots 2] [--funktionen IBildVerkleinerung,IHistogrammAusgleich]

Adresse und Akteur kommen aus CQRS_GRPC_HOST / CQRS_GRPC_PORT / CQRS_AKTEUR_TOKEN (die Werkbank setzt sie). Der Akteur des Tokens braucht
IDarf<F> für jede angebotene Funktion (Domain.Akteure.Bildrechner).
"""

import argparse
import asyncio
import os
import sys
import time
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / "Client.Infrastructure.Python"))
sys.path.insert(0, str(Path(__file__).parent))

import cv2  # noqa: E402

import domain_client.generated as gen  # noqa: E402
from cqrs_client import CqrsClient  # noqa: E402
from domain_client.domain_registry import create_registry  # noqa: E402
from domain_client.generated.funktionen import BildVerkleinerungBasis, HistogrammAusgleichBasis  # noqa: E402


def zielpfad(ausgabe: Path, quelle: str, zusatz: str) -> Path:
    """{ausgabe}/{Quelle ohne Endung}_{zusatz}.png — wie Zielpfad.In im Host: eine Wiederholung überschreibt dieselbe Datei."""
    ausgabe.mkdir(parents=True, exist_ok=True)
    return ausgabe / f"{Path(quelle).stem}_{zusatz}.png"


class Verkleinerung(BildVerkleinerungBasis):
    def __init__(self, ausgabe: Path, slots: int):
        super().__init__(slots=slots)
        self.ausgabe = ausgabe

    async def rufe(self, auftrag, x):
        bild = await asyncio.to_thread(cv2.imread, auftrag.quell_pfad, cv2.IMREAD_COLOR)
        if bild is None:
            return gen.BildNichtLesbarDto(pfad=auftrag.quell_pfad, grund="Datei nicht lesbar (Python)")
        h, w = bild.shape[:2]
        hoehe = max(1, auftrag.hoehe)
        breite = max(1, round(w * hoehe / h))
        klein = await asyncio.to_thread(cv2.resize, bild, (breite, hoehe), interpolation=cv2.INTER_AREA)
        ziel = zielpfad(self.ausgabe, auftrag.quell_pfad, "klein")
        await asyncio.to_thread(cv2.imwrite, str(ziel), klein)
        print(f"[Verkleinerung] {auftrag.quell_pfad} → {ziel} ({breite}×{hoehe})", flush=True)
        return gen.BildVerkleinertDto(pfad=str(ziel), breite_pixel=breite, hoehe_pixel=hoehe)


class HistogrammAusgleich(HistogrammAusgleichBasis):
    def __init__(self, ausgabe: Path, slots: int):
        super().__init__(slots=slots)
        self.ausgabe = ausgabe

    async def rufe(self, auftrag, x):
        bild = await asyncio.to_thread(cv2.imread, auftrag.quell_pfad, cv2.IMREAD_COLOR)
        if bild is None:
            return gen.BildNichtLesbarDto(pfad=auftrag.quell_pfad, grund="Datei nicht lesbar (Python)")
        ycrcb = cv2.cvtColor(bild, cv2.COLOR_BGR2YCrCb)
        ycrcb[:, :, 0] = cv2.equalizeHist(ycrcb[:, :, 0])
        aus = cv2.cvtColor(ycrcb, cv2.COLOR_YCrCb2BGR)
        ziel = zielpfad(self.ausgabe, auftrag.quell_pfad, "ausgeglichen")
        await asyncio.to_thread(cv2.imwrite, str(ziel), aus)
        h, w = aus.shape[:2]
        print(f"[HistogrammAusgleich] {auftrag.quell_pfad} → {ziel}", flush=True)
        return gen.HistogrammAusgeglichenDto(pfad=str(ziel), breite_pixel=w, hoehe_pixel=h, erstellt_am=int(time.time() * 1000))


@dataclass
class Leer:
    pass


class FunktionsWorker(CqrsClient[Leer]):
    """Kein Handler, keine Commands — nur ein Anbieter von Katalog-Funktionen."""


if __name__ == "__main__":
    a = argparse.ArgumentParser(description="Bildaufbereitung als externe Katalog-Funktionen")
    a.add_argument("--ausgabe", required=True, help="Verzeichnis für die erzeugten Bilder (wie Konfig:PreprocessingConfig:OutputPath)")
    a.add_argument("--slots", type=int, default=2)
    a.add_argument("--funktionen", default="IBildVerkleinerung,IHistogrammAusgleich", help="kommagetrennt")
    args = a.parse_args()

    ausgabe = Path(os.path.expanduser(args.ausgabe))
    katalog = {"IBildVerkleinerung": Verkleinerung, "IHistogrammAusgleich": HistogrammAusgleich}
    gewaehlt = [n.strip() for n in args.funktionen.split(",") if n.strip()]
    unbekannt = [n for n in gewaehlt if n not in katalog]
    if unbekannt:
        sys.exit(f"Unbekannte Funktion(en): {', '.join(unbekannt)} — bekannt: {', '.join(katalog)}")

    host = os.environ.get("CQRS_GRPC_HOST", "127.0.0.1")
    port = int(os.environ.get("CQRS_GRPC_PORT", "5101"))
    print(f"START {host}:{port} · {', '.join(gewaehlt)} · Slots {args.slots} · Ausgabe {ausgabe}", flush=True)
    FunktionsWorker(create_registry(), gen, funktionen=[katalog[n](ausgabe, args.slots) for n in gewaehlt]).run(host, port)
