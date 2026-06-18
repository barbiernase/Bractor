#!/usr/bin/env python3
# REPO-PFAD: Domain.Client.Worker.Python.ML/generate_registry.py
"""Generiert domain_client/domain_registry.py aus Proto-Deskriptoren."""

import importlib
import sys
from pathlib import Path

_repo_root = Path(__file__).parent.parent
sys.path.insert(0, str(_repo_root / "Client.Infrastructure.Python"))

from dataclasses import fields
import betterproto


def collect_oneof_types(module, wrapper_name: str, group: str = "payload") -> list[str]:
    cls = getattr(module, wrapper_name, None)
    if cls is None:
        return []
    names = []
    for f in fields(cls):
        meta = f.metadata.get("betterproto", None)
        if meta and hasattr(meta, "group") and meta.group == group:
            ft = f.type
            names.append(ft if isinstance(ft, str) else getattr(ft, "__name__", str(ft)))
    return names


def main():
    module = importlib.import_module("domain_client.generated")
    events = collect_oneof_types(module, "EventEnvelopeDto")
    commands = collect_oneof_types(module, "CommandEnvelopeDto")
    triggers = collect_oneof_types(module, "TriggerPayloadDto")
    queries = collect_oneof_types(module, "QueryPayloadDto")
    query_responses = collect_oneof_types(module, "QueryResponsePayloadDto")

    all_types = sorted(set(events + commands + triggers + queries + query_responses))

    print(f"Events: {len(events)}, Commands: {len(commands)}, "
          f"Triggers: {len(triggers)}, Queries: {len(queries)}")

    out = Path("domain_client/domain_registry.py")
    # Generate code...
    print(f"→ {out}")


if __name__ == "__main__":
    main()
