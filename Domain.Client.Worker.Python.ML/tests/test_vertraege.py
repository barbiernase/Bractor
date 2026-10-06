"""
Die Worker programmieren gegen ihre generierten Verträge (docs/konzept-akteure.md §3) — offline, ohne Server, ohne Torch.

Bewiesen wird gegen das ECHTE Generat (domain_client/generated/vertraege.py aus Cqrs.Codegen): die Worker erben die
Vertragsbasis, der Dispatch kommt aus dem Vertrag, der Handshake nennt Vertrag + Hash, und was der Vertrag nicht erlaubt,
geht nicht hinaus.
"""

import asyncio
import re
from types import SimpleNamespace
from uuid import uuid4

import pytest

import domain_client.generated as g
from cqrs_client import VertragsVerletzung
from cqrs_client.router import MessageContext
from domain_client.domain_registry import create_registry
from domain_client.generated.vertraege import (
    KlassifikationsWorkerBasis,
    KlassifiziererBasis,
    TrainingsSystemBasis,
    TrainingsWorkerBasis,
)
from domain_client.training_worker import TrainingWorker


def _training():
    return TrainingWorker(registry=create_registry(), generated_module=g, config={"epoch_seconds": 0})


def _stub():
    from run_stub import StubClassifier
    return StubClassifier(registry=create_registry(), generated_module=g)


async def _reagiere(worker, payload, ctx):
    return [x async for x in worker.handle.receive(worker, payload, ctx, worker.state)]


def test_generat_traegt_vertrag_und_hash():
    assert KlassifiziererBasis.AKTEUR == "Klassifizierer" and KlassifiziererBasis.VERTRAG == "IKlassifizierer"
    assert re.fullmatch(r"[0-9a-f]{16}", KlassifiziererBasis.VERTRAG_HASH)
    assert TrainingsSystemBasis.ZUSAGEN[g.TrainingAngefordertDto].strom is True
    assert KlassifiziererBasis.ZUSAGEN[g.BildVerfuegbarDto].ausgaenge == ()


def test_training_worker_meldet_sich_mit_seinem_vertrag_an():
    req = _training()._build_capabilities_request()
    assert req.vertrag == "TrainingsWorker", "am Handshake steht der CLIENT (er leitet die Akteure ab)"
    assert req.vertrag_hash == TrainingsWorkerBasis.VERTRAG_HASH
    assert {"TrainingAngefordert", "TrainingAbgebrochen"} <= set(req.message_types)
    assert {"MeldeTrainingBegonnen", "MeldeFortschritt", "MeldeTrainingAbgeschlossen", "MeldeTrainingGescheitert"} <= set(req.message_types)


def test_abbruch_kommt_ueber_den_vertrags_dispatch_an():
    w = _training()
    import threading
    tid = uuid4()
    flag = threading.Event()
    w.state.abbruch[str(tid)] = flag
    aus = asyncio.run(_reagiere(w, g.TrainingAbgebrochenDto(), MessageContext(aggregate_id=tid)))
    assert aus == [] and flag.is_set()


def test_stub_classifier_antwortet_genau_einmal_auf_komplett():
    s = _stub()
    agg = uuid4()
    aus = asyncio.run(_reagiere(s, g.ImagePairKomplettDto(), MessageContext(aggregate_id=agg)))
    assert [type(x) for x in aus] == [g.KlassifiziereBildPaarDurchKiDto]
    assert asyncio.run(_reagiere(s, g.ModellAktiviertDto(pfad="m.pt", name="m"), MessageContext())) == []
    req = s._build_capabilities_request()
    assert req.vertrag == "KlassifikationsWorker" and "ModellAktiviert" in req.message_types


def test_was_der_vertrag_nicht_erlaubt_geht_nicht_hinaus():
    from run_stub import StubClassifier

    class Frech(StubClassifier):
        async def auf_bild_verfuegbar(self, event, ctx, state):
            yield g.KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=2)   # Kenntnis-Zusage

    f = Frech(registry=create_registry(), generated_module=g)
    with pytest.raises(VertragsVerletzung, match="nur zur Kenntnis"):
        asyncio.run(_reagiere(f, g.BildVerfuegbarDto(version=0, pfad="a.png"), MessageContext(aggregate_id=uuid4())))


def test_ohne_alle_zusagen_startet_kein_worker():
    class Unvollstaendig(KlassifikationsWorkerBasis[SimpleNamespace]):
        async def auf_image_pair_komplett(self, e, ctx, state):
            return None

    with pytest.raises(TypeError, match="auf_bild_verfuegbar"):
        Unvollstaendig(registry=create_registry(), generated_module=g)


def test_client_generat_nennt_client_akteure_und_akteur_je_zusage():
    assert KlassifikationsWorkerBasis.CLIENT == "KlassifikationsWorker"
    assert KlassifikationsWorkerBasis.AKTEURE == ("Klassifizierer",)
    assert KlassifikationsWorkerBasis.ZUSAGEN[g.ImagePairKomplettDto].akteur == "Klassifizierer"
    assert TrainingsWorkerBasis.FRAGT == (g.HoleDatensatzSamplesDto,)
    assert re.fullmatch(r"[0-9a-f]{16}", TrainingsWorkerBasis.VERTRAG_HASH)
    # Der Client trägt den Akteur-Vertrag: dieselben Zusagen wie die Akteur-Basis.
    assert set(KlassifikationsWorkerBasis.ZUSAGEN) == set(KlassifiziererBasis.ZUSAGEN)
    assert set(TrainingsWorkerBasis.ZUSAGEN) == set(TrainingsSystemBasis.ZUSAGEN)


def test_fragen_nur_was_der_client_vertrag_nennt():
    w = _training()
    with pytest.raises(VertragsVerletzung, match="fragt HoleAktivesModellDto"):
        asyncio.run(w.query(g.HoleAktivesModellDto()))
