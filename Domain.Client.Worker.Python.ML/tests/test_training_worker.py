"""
M8 — der TrainingWorker offline: kein Server, kein Torch. Der Proxy-Query wird gefaked
(liefert DatensatzSamples), dann wird on_training_angefordert als Async-Generator getrieben
und die zurückgemeldeten Commands gesammelt.

Beweist die Akzeptanz (§5 M7/M8): „der TrainingWorker zieht Samples per query() und meldet
Fortschritt zurück."
"""

import asyncio
from types import SimpleNamespace
from uuid import uuid4

import domain_client.generated as g
from domain_client.domain_registry import create_registry
from domain_client.training_worker import TrainingWorker, TrainingState


def _worker():
    return TrainingWorker(
        registry=create_registry(),
        generated_module=g,
        config={"epoch_seconds": 0},   # Test: keine echte Wartezeit
    )


def _angefordert(epochen: int = 3):
    return g.TrainingAngefordertDto(
        datensatz_id=str(uuid4()),
        datensatz_version=1,
        hyperparameter=g.HyperparameterDto(
            epochen=epochen, lern_rate=0.001, batch_groesse=32,
            architektur="stub", seed=42),
    )


def _samples_response(n: int, seite: int = 1, gesamt: int | None = None):
    gesamt = n if gesamt is None else gesamt
    return g.QueryResponse(
        correlation_id="x",
        payload=g.QueryResponsePayloadDto(
            datensatz_samples=g.DatensatzSamplesDto(
                samples=[
                    g.DatensatzSampleDto(
                        image_pair_id=str(uuid4()),
                        dc0_pfad="dc0.png", dc2_pfad="dc2.png",
                        label=i % 3, split=i % 3)
                    for i in range(n)
                ],
                gesamt_anzahl=gesamt, seite=seite, seiten_groesse=500),
        ),
    )


async def _treibe(worker, event, patch_send_query):
    worker._proxy.send_query = patch_send_query
    ctx = SimpleNamespace(aggregate_id=uuid4())
    outputs = []
    async for cmd in worker.on_training_angefordert(event, ctx, worker.state):
        outputs.append(cmd)
    return outputs


# ═══════════════════════════════════════════════════
# Happy Path — Begonnen → Fortschritt×N → Abgeschlossen
# ═══════════════════════════════════════════════════

def test_voller_lauf_meldet_begonnen_fortschritt_abgeschlossen():
    worker = _worker()

    async def fake_send_query(payload, correlation_id=None):
        # Query korrekt ins oneof verpackt?
        field_name, inner = __import__("betterproto").which_one_of(payload, "payload")
        assert field_name == "hole_datensatz_samples"
        return _samples_response(n=4)

    outputs = asyncio.run(_treibe(worker, _angefordert(epochen=3), fake_send_query))

    assert isinstance(outputs[0], g.MeldeTrainingBegonnenDto)
    fortschritte = [o for o in outputs if isinstance(o, g.MeldeFortschrittDto)]
    assert [f.metrik.epoche for f in fortschritte] == [1, 2, 3]
    # Loss sinkt, Genauigkeit steigt (Stub-Trainer)
    assert fortschritte[0].metrik.loss > fortschritte[-1].metrik.loss
    assert fortschritte[0].metrik.genauigkeit < fortschritte[-1].metrik.genauigkeit
    assert isinstance(outputs[-1], g.MeldeTrainingAbgeschlossenDto)
    assert outputs[-1].modell_pfad.endswith(".pt")
    assert outputs[-1].endmetriken.genauigkeit > 0


# ═══════════════════════════════════════════════════
# Leerer Datensatz → Gescheitert (kein Beginn)
# ═══════════════════════════════════════════════════

def test_leerer_datensatz_scheitert():
    worker = _worker()

    async def fake_send_query(payload, correlation_id=None):
        return _samples_response(n=0)

    outputs = asyncio.run(_treibe(worker, _angefordert(), fake_send_query))

    assert len(outputs) == 1
    assert isinstance(outputs[0], g.MeldeTrainingGescheitertDto)
    assert not any(isinstance(o, g.MeldeTrainingBegonnenDto) for o in outputs)


# ═══════════════════════════════════════════════════
# Paginierung — _hole_alle_samples blättert durch
# ═══════════════════════════════════════════════════

def test_samples_werden_durchpaginiert():
    worker = _worker()
    gesehene_seiten = []

    async def fake_send_query(payload, correlation_id=None):
        _, inner = __import__("betterproto").which_one_of(payload, "payload")
        gesehene_seiten.append(inner.seite)
        # Seite 1 → 2 von insgesamt 3; Seite 2 → 1 (Rest)
        if inner.seite == 1:
            return _samples_response(n=2, seite=1, gesamt=3)
        return _samples_response(n=1, seite=2, gesamt=3)

    outputs = asyncio.run(_treibe(worker, _angefordert(epochen=1), fake_send_query))

    assert gesehene_seiten == [1, 2]   # zwei Query-Roundtrips
    assert isinstance(outputs[0], g.MeldeTrainingBegonnenDto)
    assert isinstance(outputs[-1], g.MeldeTrainingAbgeschlossenDto)
