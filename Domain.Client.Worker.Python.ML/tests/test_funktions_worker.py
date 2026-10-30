"""
Der generische Funktions-Worker (docs/09 §9.5) gegen das ECHTE Generat: die Tabelle (domain_client/generated/funktions_tabelle.py) nennt je
Funktion mit Python-Basis Basis, Modul und Klasse; der Worker lädt die Implementierung daraus — ohne Domänenwissen — und gibt ihr Slots und
Konfig. Dazu die Implementierungen der Bildaufbereitung mit einem echten Bild.
"""

import asyncio
from pathlib import Path

import cv2
import numpy as np
import pytest

from datetime import datetime, timezone

import domain_client.generated as g
import funktions_worker as worker
from cqrs_client import Herkunft
from domain_client.generated import funktionen as basen
from domain_client.generated.funktions_tabelle import TABELLE

H = Herkunft(stream="s-1", version=1, zeit=datetime.now(timezone.utc), schluessel="a-1")


def test_tabelle_nennt_je_funktion_eine_generierte_basis():
    assert TABELLE, "der Codegen erzeugt eine Zeile je Funktion mit Python-Basis"
    for funktion, (basis, modul, klasse) in TABELLE.items():
        b = getattr(basen, basis)
        assert b.FUNKTION == funktion
        assert modul == "funktionen." + modul.split(".")[1] and klasse[0].isupper()


def test_worker_laedt_die_implementierung_aus_der_tabelle_mit_slots_und_konfig(tmp_path):
    f = worker.lade("IBildVerkleinerung", 3, {"AusgabePfad": str(tmp_path)})
    assert isinstance(f, basen.BildVerkleinerungBasis)
    assert (f.FUNKTION, f.slots, f.konfig) == ("IBildVerkleinerung", 3, {"AusgabePfad": str(tmp_path)})


def test_unbekannte_funktion_und_fehlende_implementierung_brechen_den_start():
    with pytest.raises(SystemExit, match="Unbekannte Funktion"):
        worker.lade("IGibtEsNicht", 1, {})
    fehlt = next((n for n, (_, modul, _) in TABELLE.items() if not (Path(worker.PROJEKT) / (modul.replace(".", "/") + ".py")).exists()), None)
    if fehlt:
        with pytest.raises(SystemExit, match="keine Implementierung"):
            worker.lade(fehlt, 1, {})


def test_konfig_paare():
    assert worker.werte(["AusgabePfad=/x", "Modus=schnell"]) == {"AusgabePfad": "/x", "Modus": "schnell"}
    with pytest.raises(SystemExit):
        worker.werte(["kaputt"])


def test_die_kette_der_bildaufbereitung_rechnet_ein_echtes_bild(tmp_path):
    """Einlesen → Verkleinern → Kontrast ausgleichen: jeder Schritt hört das Ergebnis des vorigen, die Datei der Quelle reist mit."""
    quelle = tmp_path / "2026_10_13_10_00_00_000_DC0.png"
    cv2.imwrite(str(quelle), (np.random.default_rng(1).random((1024, 1536, 3)) * 255).astype(np.uint8))
    konfig = {"AusgabePfad": str(tmp_path / "aus")}
    einlesen, verkleinern, ausgleichen = (worker.lade(f, 1, konfig) for f in ("IBildEinlesen", "IBildVerkleinerung", "IKontrastausgleich"))

    gelesen = asyncio.run(einlesen.auf_datei_erkannt(g.DateiErkanntDto(name=quelle.name, pfad=str(quelle), groesse=7, erkannt_am=1), H))
    assert isinstance(gelesen, g.BildEingelesenDto) and (gelesen.breite_pixel, gelesen.hoehe_pixel) == (1536, 1024)
    klein = asyncio.run(verkleinern.auf_bild_eingelesen(gelesen, H))
    assert (klein.breite_pixel, klein.hoehe_pixel) == (768, 512) and Path(klein.verkleinert).exists()
    vorschau = asyncio.run(ausgleichen.auf_bild_verkleinert(klein, H))
    assert isinstance(vorschau, g.VorschauErstelltDto)
    assert (vorschau.breite_pixel, vorschau.hoehe_pixel) == (768, 512)
    assert Path(vorschau.vorschau).name == quelle.stem + "_vorschau.png" and Path(vorschau.vorschau).exists()
    assert (vorschau.dateiname, vorschau.pfad, vorschau.groesse, vorschau.erkannt_am) == (quelle.name, str(quelle), 7, 1)

    nicht = asyncio.run(einlesen.auf_datei_erkannt(g.DateiErkanntDto(name="fehlt.png", pfad=str(tmp_path / "fehlt.png")), H))
    assert isinstance(nicht, g.BildNichtLesbarDto)
