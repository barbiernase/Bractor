"""
Der generische Funktions-Worker (konzept-werkbank §12.3 E13) gegen das ECHTE Generat: die Tabelle (domain_client/generated/funktions_tabelle.py) nennt je
Funktion mit Python-Basis Basis, Modul und Klasse; der Worker lädt die Implementierung daraus — ohne Domänenwissen — und gibt ihr Slots und
Konfig. Dazu die Implementierungen der Bildaufbereitung mit einem echten Bild.
"""

import asyncio
from pathlib import Path

import cv2
import numpy as np
import pytest

import domain_client.generated as g
import funktions_worker as worker
from cqrs_client import Ausfuehrung
from domain_client.generated import funktionen as basen
from domain_client.generated.funktions_tabelle import TABELLE

X = Ausfuehrung(ausfuehrungs_id="a-1", korrelation="k-1")


def test_tabelle_nennt_je_funktion_eine_generierte_basis():
    assert TABELLE, "der Codegen erzeugt eine Zeile je Funktion mit Python-Basis"
    for funktion, (basis, modul, klasse) in TABELLE.items():
        b = getattr(basen, basis)
        assert b.FUNKTION == funktion
        assert modul == "funktionen." + modul.split(".")[1] and klasse[0].isupper()


def test_worker_laedt_die_implementierung_aus_der_tabelle_mit_slots_und_konfig(tmp_path):
    f = worker.lade("IBildVerkleinerung", 3, {"OutputPath": str(tmp_path)})
    assert isinstance(f, basen.BildVerkleinerungBasis)
    assert (f.FUNKTION, f.slots, f.konfig) == ("IBildVerkleinerung", 3, {"OutputPath": str(tmp_path)})


def test_unbekannte_funktion_und_fehlende_implementierung_brechen_den_start():
    with pytest.raises(SystemExit, match="Unbekannte Funktion"):
        worker.lade("IGibtEsNicht", 1, {})
    fehlt = next((n for n, (_, modul, _) in TABELLE.items() if not (Path(worker.PROJEKT) / (modul.replace(".", "/") + ".py")).exists()), None)
    if fehlt:
        with pytest.raises(SystemExit, match="keine Implementierung"):
            worker.lade(fehlt, 1, {})


def test_konfig_paare():
    assert worker.werte(["OutputPath=/x", "Modus=schnell"]) == {"OutputPath": "/x", "Modus": "schnell"}
    with pytest.raises(SystemExit):
        worker.werte(["kaputt"])


def test_bild_verkleinerung_und_histogramm_ausgleich_rechnen_ein_echtes_bild(tmp_path):
    quelle = tmp_path / "2026_10_13_10_00_00_000_DC0.png"
    cv2.imwrite(str(quelle), (np.random.default_rng(1).random((400, 600, 3)) * 255).astype(np.uint8))
    ausgabe = tmp_path / "aus"

    klein = asyncio.run(worker.lade("IBildVerkleinerung", 1, {"OutputPath": str(ausgabe)})
                        .rufe(g.VerkleinereBildDto(quell_pfad=str(quelle), hoehe=100), X))
    assert isinstance(klein, g.BildVerkleinertDto)
    assert (klein.breite_pixel, klein.hoehe_pixel) == (150, 100)
    assert Path(klein.pfad).name == quelle.stem + "_klein.png" and Path(klein.pfad).exists()

    aus = asyncio.run(worker.lade("IHistogrammAusgleich", 1, {"OutputPath": str(ausgabe)})
                      .rufe(g.GleicheHistogrammAusDto(quell_pfad=str(quelle)), X))
    assert isinstance(aus, g.HistogrammAusgeglichenDto) and Path(aus.pfad).exists()

    nicht = asyncio.run(worker.lade("IBildVerkleinerung", 1, {"OutputPath": str(ausgabe)})
                        .rufe(g.VerkleinereBildDto(quell_pfad=str(tmp_path / "fehlt.png"), hoehe=100), X))
    assert isinstance(nicht, g.BildNichtLesbarDto)
