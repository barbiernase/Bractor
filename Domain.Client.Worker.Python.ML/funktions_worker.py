#!/usr/bin/env python3
# REPO-PFAD: Domain.Client.Worker.Python.ML/funktions_worker.py
"""
Der GENERISCHE Funktions-Worker (konzept-werkbank §12.3 E13): bietet Katalog-Funktionen mit Laufort Extern über gRPC an. Er kennt keine Domäne — welche
Funktionen es gibt und wo ihre Implementierung liegt, sagt die generierte Tabelle (domain_client/generated/funktions_tabelle.py:
Funktion → Basis, Modul, Klasse); die Implementierungen stehen in funktionen/<modul>.py (Klasse erbt die generierte Basis, füllt `rufe`).
Gestartet von der Werkbank („▶ Worker starten" an der Funktions-Karte, Umgebung: Funktionen:<F>:Worker) oder von Hand:

    python funktions_worker.py --funktionen IBildVerkleinerung,IHistogrammAusgleich [--slots 2] [--konfig AusgabePfad=<verz> …] [--pfad <verz>]

Adresse und Akteur kommen aus CQRS_GRPC_HOST / CQRS_GRPC_PORT / CQRS_AKTEUR_TOKEN (die Werkbank setzt sie). Der Akteur des Tokens braucht
IDarf<F> für jede angebotene Funktion. `--konfig NAME=WERT` sind die Werte der Implementierungen (self.konfig), `--pfad` legt ein Verzeichnis
mit eigenen Implementierungen (ein Paket `funktionen`) vor das Projekt — z. B. Test-Implementierungen.
"""

from __future__ import annotations

import argparse
import importlib
import os
import sys
from dataclasses import dataclass
from pathlib import Path

PROJEKT = Path(__file__).resolve().parent
sys.path.insert(0, str(PROJEKT.parent / "Client.Infrastructure.Python"))
sys.path.insert(0, str(PROJEKT))

import domain_client.generated as gen  # noqa: E402
from cqrs_client import CqrsClient  # noqa: E402
from cqrs_client.funktion import FunktionsBasis  # noqa: E402
from domain_client.domain_registry import create_registry  # noqa: E402
from domain_client.generated import funktionen as basen  # noqa: E402
from domain_client.generated.funktions_tabelle import TABELLE  # noqa: E402


def lade(funktion: str, slots: int, konfig: dict[str, str]) -> FunktionsBasis:
    """Die Implementierung einer Funktion nach der Tabelle — sie muss die generierte Basis erben (sonst wäre sie nicht diese Funktion)."""
    if funktion not in TABELLE:
        raise SystemExit(f"Unbekannte Funktion {funktion} — mit Python-Basis bekannt: {', '.join(TABELLE) or 'keine'}")
    basis_name, modul, klasse = TABELLE[funktion]
    try:
        impl = getattr(importlib.import_module(modul), klasse)
    except (ModuleNotFoundError, AttributeError) as e:
        raise SystemExit(f"{funktion}: keine Implementierung {modul}.{klasse} ({e}) — die Werkbank legt sie mit ⟳ an (Laufort Extern)")
    basis = getattr(basen, basis_name)
    if not (isinstance(impl, type) and issubclass(impl, basis)):
        raise SystemExit(f"{funktion}: {modul}.{klasse} erbt nicht {basis_name}")
    return impl(slots=slots, konfig=konfig)


def werte(paare: list[str]) -> dict[str, str]:
    aus: dict[str, str] = {}
    for p in paare:
        name, _, wert = p.partition("=")
        if not name or not _:
            raise SystemExit(f"--konfig erwartet NAME=WERT, nicht '{p}'")
        aus[name.strip()] = os.path.expanduser(wert)
    return aus


@dataclass
class Leer:
    pass


class FunktionsWorker(CqrsClient[Leer]):
    """Kein Handler, keine Commands — nur ein Anbieter von Katalog-Funktionen."""


def main(argv: list[str] | None = None) -> None:
    a = argparse.ArgumentParser(description="Generischer Worker für Katalog-Funktionen (Laufort Extern)")
    a.add_argument("--funktionen", required=True, help="kommagetrennt, z. B. IBildVerkleinerung,IHistogrammAusgleich")
    a.add_argument("--slots", type=int, default=2)
    a.add_argument("--konfig", action="append", default=[], help="NAME=WERT (mehrfach) — die Werte der Implementierungen")
    a.add_argument("--pfad", action="append", default=[], help="Verzeichnis mit eigenen Implementierungen (Paket funktionen), vor dem Projekt")
    args = a.parse_args(argv)

    for p in reversed(args.pfad):
        sys.path.insert(0, str(Path(p).expanduser().resolve()))
    konfig = werte(args.konfig)
    gewaehlt = [n.strip() for n in args.funktionen.split(",") if n.strip()]
    funktionen = [lade(n, args.slots, konfig) for n in gewaehlt]

    host = os.environ.get("CQRS_GRPC_HOST", "127.0.0.1")
    port = int(os.environ.get("CQRS_GRPC_PORT", "5101"))
    print(f"START {host}:{port} · {', '.join(gewaehlt)} · Slots {args.slots}", flush=True)
    FunktionsWorker(create_registry(), gen, funktionen=funktionen).run(host, port)


if __name__ == "__main__":
    main()
