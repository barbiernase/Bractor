#!/usr/bin/env python3
# REPO-PFAD: Domain.Client.Worker.Python.ML/run_train_stub.py
"""
TrainingWorker im STUB-Modus — testet den Trainings-Nachrichtenfluss ohne echtes Torch/GPU.

Fährt einen simulierten Trainingslauf: zieht Samples über den Query-Kanal, meldet
Begonnen → Fortschritt (je Epoche) → Abgeschlossen zurück. Kein Modell, keine Pixel.

Konfiguration: appsettings.json (gleiche Konvention wie C#)

    python run_train_stub.py
"""

import importlib
import json
import os
import logging
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / "Client.Infrastructure.Python"))

from domain_client.domain_registry import create_registry
from domain_client.training_worker import TrainingWorker

log = logging.getLogger("train-stub")


def load_config() -> dict:
    path = Path(__file__).parent / "appsettings.json"
    if not path.exists():
        return {}
    with open(path) as f:
        return json.load(f)


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(name)s %(message)s")
    cfg = load_config()
    grpc = cfg.get("GrpcServer", {})
    # Wie jeder Worker, den die Werkbank oder ein Test startet: CQRS_GRPC_HOST/CQRS_GRPC_PORT vor appsettings.json.
    host = os.environ.get("CQRS_GRPC_HOST") or grpc.get("Host", "localhost")
    port = int(os.environ.get("CQRS_GRPC_PORT") or grpc.get("Port", 5001))

    nutzlast = importlib.import_module("domain_client.generated.nutzlast_tabelle").TABELLE
    registry = create_registry()

    print(f"\n  TrainingWorker (Stub): {host}:{port}")
    print("  Kein Torch/GPU — simuliertes Training, nur Nachrichtenfluss\n")

    worker = TrainingWorker(
        registry=registry,
        nutzlast=nutzlast,
        config={"epoch_seconds": 0.3},
    )
    worker.run(host, port)
