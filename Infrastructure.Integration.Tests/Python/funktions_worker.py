#!/usr/bin/env python3
# REPO-PFAD: Infrastructure.Integration.Tests/Python/funktions_worker.py
"""
Ein Funktions-Worker für den Integrationstest (PipelineFlussE2ETests): bietet IBildVerkleinerung über gRPC an und rechnet nichts —
er hängt „_py" an den Pfad, damit der Test im Log sieht, dass DIESER Worker den Auftrag ausgeführt hat.

    python funktions_worker.py <port> [slots]
"""

import sys
from dataclasses import dataclass
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "Client.Infrastructure.Python"))
sys.path.insert(0, str(REPO / "Domain.Client.Worker.Python.ML"))

import domain_client.generated as gen  # noqa: E402
from cqrs_client import CqrsClient  # noqa: E402
from domain_client.domain_registry import create_registry  # noqa: E402
from domain_client.generated.funktionen import BildVerkleinerungBasis  # noqa: E402


class Verkleinerer(BildVerkleinerungBasis):
    SLOTS = 2

    async def rufe(self, auftrag, x):
        print(f"RUFE {auftrag.quell_pfad} {x.ausfuehrungs_id}", flush=True)
        return gen.BildVerkleinertDto(pfad=auftrag.quell_pfad + "_py.png", breite_pixel=7, hoehe_pixel=auftrag.hoehe)


@dataclass
class Leer:
    pass


class FunktionsWorker(CqrsClient[Leer]):
    """Kein Handler, keine Commands — nur ein Anbieter von Katalog-Funktionen."""


if __name__ == "__main__":
    port = int(sys.argv[1])
    slots = int(sys.argv[2]) if len(sys.argv) > 2 else Verkleinerer.SLOTS
    print("START", port, flush=True)
    FunktionsWorker(create_registry(), gen, funktionen=[Verkleinerer(slots=slots)]).run("127.0.0.1", port)
