#!/usr/bin/env python3
# REPO-PFAD: Domain.Client.Worker.Python.ML/run_stub.py
"""
Stub-Classifier — testet ob Nachrichten rein und raus gehen.
Kein Modell, kein torch, kein Bildladen.

Konfiguration: appsettings.json (gleiche Konvention wie C#)

    python run_stub.py
"""

import importlib
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path
from uuid import UUID

sys.path.insert(0, str(Path(__file__).parent.parent / "Client.Infrastructure.Python"))

import logging
from domain_client.domain_registry import create_registry
from domain_client.generated import (
    BildVerfuegbarDto,
    ImagePairKomplettDto,
    KlassifiziereBildPaarDurchKiDto,
    ModellAktiviertDto,
)
from domain_client.generated.vertraege import KlassifiziererBasis

log = logging.getLogger("stub")
VERSION_NAMES = {0: "dc0", 1: "dc2"}


def load_config() -> dict:
    """Lädt appsettings.json aus dem Projektverzeichnis."""
    path = Path(__file__).parent / "appsettings.json"
    if not path.exists():
        log.warning("appsettings.json nicht gefunden, verwende Defaults")
        return {}
    with open(path) as f:
        return json.load(f)


@dataclass
class StubState:
    bilder: dict[UUID, dict[int, str]] = field(default_factory=dict)
    events: int = 0


class StubClassifier(KlassifiziererBasis[StubState]):
    """Derselbe Vertrag wie der echte Classifier (IKlassifizierer) — nur ohne Modell."""

    async def auf_bild_verfuegbar(self, event: BildVerfuegbarDto, ctx, state: StubState):
        state.events += 1
        agg = ctx.aggregate_id
        v = VERSION_NAMES.get(event.version, "?")
        if agg not in state.bilder:
            state.bilder[agg] = {}
        state.bilder[agg][event.version] = event.pfad
        log.info("📥 BildVerfuegbar #%d: %s %s → %s", state.events, str(agg)[:8], v, event.pfad)

    async def auf_image_pair_komplett(self, event: ImagePairKomplettDto, ctx, state: StubState):
        state.events += 1
        agg = ctx.aggregate_id
        pfade = state.bilder.get(agg, {})
        log.info("📥 ImagePairKomplett #%d: %s", state.events, agg)
        for v, pfad in pfade.items():
            log.info("   %s: %s", VERSION_NAMES.get(v, "?"), pfad)
        log.info("📤 → KlassifiziereBildPaarDurchKi(label=0=KeineAnomalie)")

        yield KlassifiziereBildPaarDurchKiDto(aggregate_id=str(agg), label=0)
        log.info("✅ Gesendet!")

    async def auf_modell_aktiviert(self, event: ModellAktiviertDto, ctx, state: StubState):
        log.info("📥 ModellAktiviert: %s (%s) — Stub lädt nichts", event.name, event.pfad)


if __name__ == "__main__":
    cfg = load_config()
    grpc = cfg.get("GrpcServer", {})
    host = grpc.get("Host", "localhost")
    port = grpc.get("Port", 5001)

    generated = importlib.import_module("domain_client.generated")
    registry = create_registry()

    print(f"\n  Stub-Classifier: {host}:{port}")
    print(f"  Kein Modell, kein Bildladen — nur Nachrichtenfluss testen\n")

    client = StubClassifier(registry=registry, generated_module=generated)
    client.run(host, port)
