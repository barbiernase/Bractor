"""
Der TrainingWorker offline: kein Server, kein Torch. Der Proxy-Query wird gefaked
(liefert DatensatzSamples bzw. den Stand des Laufs), dann wird die Reaktion auf TrainingAngefordert (`auf_training_angefordert`) als
Async-Generator getrieben und die zurückgemeldeten Commands gesammelt.

Beweist die Akzeptanz: „der TrainingWorker zieht Samples per query() und meldet
Fortschritt zurück."
"""

import asyncio
from uuid import uuid4

import domain_client.generated as g
from domain_client.generated.nutzlast_tabelle import TABELLE as NUTZLAST
from cqrs_client.leitung import Nutzlast, QueryResponse
from cqrs_client.router import MessageContext
from domain_client.domain_registry import create_registry
from domain_client.training_worker import TrainingWorker, TrainingState


def _worker():
    return TrainingWorker(
        registry=create_registry(),
        nutzlast=NUTZLAST,
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
    antwort = g.DatensatzSamplesDto(
        samples=[
            g.DatensatzSampleDto(
                image_pair_id=str(uuid4()),
                dc0_pfad="dc0.png", dc2_pfad="dc2.png",
                label=i % 3, split=i % 3)
            for i in range(n)
        ],
        gesamt_anzahl=gesamt, seite=seite, seiten_groesse=500)
    return QueryResponse(correlation_id="x", payload=Nutzlast(typ="DatensatzSamples", daten=bytes(antwort)))


def _lauf_response(status: int):
    antwort = g.TrainingslaufAntwortDto(id=str(uuid4()), status=status)
    return QueryResponse(correlation_id="x", payload=Nutzlast(typ="TrainingslaufAntwort", daten=bytes(antwort)))


LAEUFT, ABGEBROCHEN = 1, 4


def _mit_lauf(samples, status=lambda: LAEUFT):
    """Eine fake Query-Leitung: HoleTrainingslauf liefert den Stand des Laufs, alles andere `samples(nutzlast)`."""
    async def fake_send_query(nutzlast, correlation_id=None):
        if nutzlast.typ == "HoleTrainingslauf":
            return _lauf_response(status())
        return samples(nutzlast)
    return fake_send_query


async def _treibe(worker, event, patch_send_query, lauf=None):
    worker._proxy.send_query = patch_send_query
    ctx = MessageContext(aggregate_id=lauf or uuid4())
    outputs = []
    async for cmd in worker.auf_training_angefordert(event, ctx, worker.state):
        outputs.append(cmd)
    return outputs


# ═══════════════════════════════════════════════════
# Happy Path — Begonnen → Fortschritt×N → Abgeschlossen
# ═══════════════════════════════════════════════════

def test_voller_lauf_meldet_begonnen_fortschritt_abgeschlossen():
    worker = _worker()

    def samples(nutzlast):
        # Query korrekt als Nutzlast verpackt?
        assert nutzlast.typ == "HoleDatensatzSamples"
        assert isinstance(g.HoleDatensatzSamplesDto().parse(nutzlast.daten), g.HoleDatensatzSamplesDto)
        return _samples_response(n=4)

    lauf = uuid4()
    outputs = asyncio.run(_treibe(worker, _angefordert(epochen=3), _mit_lauf(samples), lauf))

    assert {o.aggregate_id for o in outputs} == {str(lauf)}, "jede Meldung geht an den Lauf, dessen Fakt es ist"
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

    outputs = asyncio.run(_treibe(worker, _angefordert(), _mit_lauf(lambda n: _samples_response(n=0))))

    assert len(outputs) == 1
    assert isinstance(outputs[0], g.MeldeTrainingGescheitertDto)
    assert not any(isinstance(o, g.MeldeTrainingBegonnenDto) for o in outputs)


# ═══════════════════════════════════════════════════
# Paginierung — _hole_alle_samples blättert durch
# ═══════════════════════════════════════════════════

def test_samples_werden_durchpaginiert():
    worker = _worker()
    gesehene_seiten = []

    def samples(nutzlast):
        inner = g.HoleDatensatzSamplesDto().parse(nutzlast.daten)
        gesehene_seiten.append(inner.seite)
        # Seite 1 → 2 von insgesamt 3; Seite 2 → 1 (Rest)
        if inner.seite == 1:
            return _samples_response(n=2, seite=1, gesamt=3)
        return _samples_response(n=1, seite=2, gesamt=3)

    outputs = asyncio.run(_treibe(worker, _angefordert(epochen=1), _mit_lauf(samples)))

    assert gesehene_seiten == [1, 2]   # zwei Query-Roundtrips
    assert isinstance(outputs[0], g.MeldeTrainingBegonnenDto)
    assert isinstance(outputs[-1], g.MeldeTrainingAbgeschlossenDto)


# ═══════════════════════════════════════════════════
# Abbruch — der Lauf steht auf „abgebrochen“: kooperativ aufhören, nichts mehr melden
# ═══════════════════════════════════════════════════

def test_ein_abgebrochener_lauf_endet_nach_der_naechsten_epoche_ohne_abschluss():
    worker = _worker()
    outputs = asyncio.run(_treibe(worker, _angefordert(epochen=5), _mit_lauf(lambda n: _samples_response(n=2), status=lambda: ABGEBROCHEN)))

    assert isinstance(outputs[0], g.MeldeTrainingBegonnenDto)
    assert len([o for o in outputs if isinstance(o, g.MeldeFortschrittDto)]) < 5, "nach dem Abbruch rechnet er keine weiteren Epochen"
    assert not any(isinstance(o, (g.MeldeTrainingAbgeschlossenDto, g.MeldeTrainingGescheitertDto)) for o in outputs)
    assert worker.state.abbruch == {}
