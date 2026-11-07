"""
Die Worker programmieren gegen ihre generierten Client-Verträge (docs/konzept-akteure.md §4) — offline, ohne Server, ohne Torch.

Bewiesen wird gegen das ECHTE Generat (domain_client/generated/vertraege.py aus Cqrs.Codegen): die Worker erben die Client-Basis und reagieren
auf die Fakten, die ihre Akteure hören (IHoert eines Events, Schritt 6), mit Commands ihres IDarf; der Handshake nennt Client + Hash, und eine
Reaktion gibt nichts außerhalb des IDarf aus.
"""

import asyncio
import re
from types import SimpleNamespace
from uuid import uuid4

import pytest

import domain_client.generated as g
from domain_client.generated.nutzlast_tabelle import TABELLE as NUTZLAST
from cqrs_client import VertragsVerletzung
from cqrs_client.router import MessageContext
from domain_client.domain_registry import create_registry
from domain_client.generated.vertraege import KlassifikationsWorkerBasis, TrainingsWorkerBasis
from domain_client.training_worker import TrainingWorker


def _training():
    return TrainingWorker(registry=create_registry(), nutzlast=NUTZLAST, config={"epoch_seconds": 0})


def _stub():
    from run_stub import StubClassifier
    return StubClassifier(registry=create_registry(), nutzlast=NUTZLAST)


async def _hoere(worker, payload, ctx):
    return [x async for x in worker.handle.receive(worker, payload, ctx, worker.state)]


def test_generat_nennt_die_fakten_und_das_idarf():
    assert KlassifikationsWorkerBasis.CLIENT == "KlassifikationsWorker"
    assert KlassifikationsWorkerBasis.AKTEURE == ("Klassifizierer",)
    assert re.fullmatch(r"[0-9a-f]{16}", KlassifikationsWorkerBasis.VERTRAG_HASH)
    assert set(KlassifikationsWorkerBasis.HOERT) == {g.BildVerfuegbarDto, g.ImagePairKomplettDto, g.ModellAktiviertDto}
    assert g.KlassifiziereBildPaarDurchKiDto in KlassifikationsWorkerBasis.SPONTAN
    assert TrainingsWorkerBasis.HOERT == {g.TrainingAngefordertDto: "auf_training_angefordert"}
    assert set(TrainingsWorkerBasis.SPONTAN) == {g.MeldeTrainingBegonnenDto, g.MeldeFortschrittDto, g.MeldeTrainingAbgeschlossenDto,
                                                 g.MeldeTrainingGescheitertDto}
    assert TrainingsWorkerBasis.FRAGT == (g.HoleDatensatzSamplesDto, g.HoleTrainingslaufDto)


def test_training_worker_meldet_sich_als_client():
    w = _training()
    req = w._build_capabilities_request()
    assert req.vertrag == "TrainingsWorker", "am Handshake steht der CLIENT (er leitet die Akteure ab)"
    assert req.vertrag_hash == TrainingsWorkerBasis.VERTRAG_HASH
    assert "TrainingAngefordert" in req.message_types


def test_stub_classifier_reagiert_auf_das_komplette_paar_mit_genau_einem_urteil():
    s = _stub()
    agg = uuid4()
    ctx = MessageContext(aggregate_id=agg)
    assert asyncio.run(_hoere(s, g.BildVerfuegbarDto(version=0, pfad="a.png"), ctx)) == []
    aus = asyncio.run(_hoere(s, g.ImagePairKomplettDto(), ctx))
    assert len(aus) == 1 and isinstance(aus[0], g.KlassifiziereBildPaarDurchKiDto)
    assert aus[0].aggregate_id == str(agg), "das Ziel des Urteils ist das Paar des Fakts"
    assert asyncio.run(_hoere(s, g.ModellAktiviertDto(pfad="m.pt", name="m"), MessageContext())) == []
    req = s._build_capabilities_request()
    assert req.vertrag == "KlassifikationsWorker" and "ImagePairKomplett" in req.message_types


def test_eine_reaktion_gibt_nichts_ausserhalb_des_idarf_aus():
    from run_stub import StubClassifier

    class Frech(StubClassifier):
        async def auf_bild_verfuegbar(self, event, ctx, state):
            yield g.LabelBildPaarDto(aggregate_id=str(ctx.aggregate_id), label=2)

    f = Frech(registry=create_registry(), nutzlast=NUTZLAST)
    with pytest.raises(VertragsVerletzung, match="nur mit Commands seiner Akteure"):
        asyncio.run(_hoere(f, g.BildVerfuegbarDto(version=0, pfad="a.png"), MessageContext(aggregate_id=uuid4())))


def test_ohne_jede_methode_des_vertrags_startet_kein_worker():
    class Unvollstaendig(KlassifikationsWorkerBasis[SimpleNamespace]):
        async def auf_image_pair_komplett(self, e, ctx, state):
            return None

        async def auf_modell_aktiviert(self, e, ctx, state):
            return None

    with pytest.raises(TypeError, match="auf_bild_verfuegbar"):
        Unvollstaendig(registry=create_registry(), nutzlast=NUTZLAST)


def test_fragen_nur_was_der_client_vertrag_nennt():
    w = _training()
    with pytest.raises(VertragsVerletzung, match="fragt HoleAktivesModellDto"):
        asyncio.run(w.query(g.HoleAktivesModellDto()))


def test_die_generierte_nutzlast_tabelle_traegt_jeden_vertragstyp():
    """
    Die Nutzlast-Tabelle (generated/nutzlast_tabelle.py) ist ein Generat derselben Codegen-Lesart wie die Verträge:
    alles, was ein Worker empfängt oder sendet, steht darin — sonst ginge es nicht über die Leitung.
    """
    from domain_client.generated import vertraege
    import inspect

    klassen = set(NUTZLAST.values())
    assert len(klassen) == len(NUTZLAST), "jede DTO-Klasse unter genau einem Typ"
    noetig: set[type] = set()
    def eigene(modul):   # nur die generierten Basen, nicht die importierten SDK-Klassen
        return [k for _, k in inspect.getmembers(modul, inspect.isclass) if k.__module__ == modul.__name__]

    for basis in eigene(vertraege):
        noetig |= set(basis.__dict__.get("HOERT", {}))
        noetig |= set(basis.__dict__.get("SPONTAN", ())) | set(basis.__dict__.get("FRAGT", ()))
    assert noetig, "die Beispielanwendung hat Verträge"
    assert not {t.__name__ for t in noetig - klassen}
