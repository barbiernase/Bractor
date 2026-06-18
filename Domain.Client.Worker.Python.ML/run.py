#!/usr/bin/env python3
# REPO-PFAD: Domain.Client.Worker.Python.ML/run.py
"""
Produktiv-Classifier. Konfiguration: appsettings.json

    python run.py
"""

import importlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / "Client.Infrastructure.Python"))

import torch
from domain_client.classifier import ImageClassifier
from domain_client.domain_registry import create_registry

GENERATED_DIR = Path(__file__).parent / "domain_client" / "generated"


def load_config() -> dict:
    path = Path(__file__).parent / "appsettings.json"
    if not path.exists():
        print("ERROR: appsettings.json nicht gefunden")
        sys.exit(1)
    with open(path) as f:
        return json.load(f)


if __name__ == "__main__":
    cfg = load_config()
    grpc = cfg.get("GrpcServer", {})
    files = cfg.get("FileServer", {})
    model = cfg.get("Model", {})

    grpc_host = grpc.get("Host", "localhost")
    grpc_port = grpc.get("Port", 5001)
    file_host = files.get("Host", grpc_host)
    file_port = files.get("Port", 80)
    model_path = model.get("Path", "")
    device = model.get("Device", "auto")

    if device == "auto":
        device = "cuda" if torch.cuda.is_available() else "cpu"

    if not model_path:
        print("ERROR: Model.Path in appsettings.json setzen")
        sys.exit(1)

    file_base_url = f"http://{file_host}:{file_port}"
    generated_module = importlib.import_module("domain_client.generated")
    registry = create_registry()

    print(f"\n  gRPC:   {grpc_host}:{grpc_port}")
    print(f"  Files:  {file_base_url}/api/files/...")
    print(f"  Model:  {model_path}")
    print(f"  Device: {device}\n")

    client = ImageClassifier(
        registry=registry,
        generated_module=generated_module,
        config={
            "file_base_url": file_base_url,
            "model_path": model_path,
            "device": device,
        },
    )
    client.run(grpc_host, grpc_port, file_base_url=file_base_url, generated_dir=GENERATED_DIR)
