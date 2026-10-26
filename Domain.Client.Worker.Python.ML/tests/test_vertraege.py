"""
Die Worker programmieren gegen ihre generierten Client-Verträge (docs/konzept-akteure.md §4) — offline, ohne Server, ohne Torch.

Bewiesen wird gegen das ECHTE Generat (domain_client/generated/vertraege.py aus Cqrs.Codegen): die Worker erben die Client-Basis,
tragen ihre Funktionen (ITraegt) und hören ihre Kenntnis (IHoert); der Handshake nennt Client + Hash, und eine Kenntnis gibt nichts aus.
"""

import asyncio
import re
from datetime import datetime, timezone
from types import SimpleNamespace
from uuid import uuid4

import pytest

import domain_client.generated as g
from domain_client.generated.nutzlast_tabelle import TABELLE as NUTZLAST
from cqrs_client import Ausgabe, Herkunft, VertragsVerletzung
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


def _getragen(worker, funktion):
    return next(f for f in worker.funktionen._funktionen.values() if f.FUNKTION == funktion)


def test_generat_traegt_funktionen_und_hoert_kenntnis():
    assert KlassifikationsWorkerBasis.CLIENT == "KlassifikationsWorker"
    assert KlassifikationsWorkerBasis.AKTEURE == ("Klassifizierer",)
    assert re.fullmatch(r"[0-9a-f]{16}", KlassifikationsWorkerBasis.VERTRAG_HASH)
    e = KlassifikationsWorkerBasis.TRAEGT["IBildpaarBeurteilung"][g.ImagePairKomplettDto]
    assert e.methode == "bildpaar_beurteilung" and not e.strom and e.ausgaben == {g.KlassifiziereBildPaarDurchKiDto: Ausgabe.COMMAND}
    assert set(KlassifikationsWorkerBasis.HOERT) == {g.BildVerfuegbarDto, g.ModellAktiviertDto}
    t = TrainingsWorkerBasis.TRAEGT["ITraining"][g.TrainingAngefordertDto]
    assert t.methode == "training" and t.strom
    assert TrainingsWorkerBasis.HOERT == {g.TrainingAbgebrochenDto: "auf_training_abgebrochen"}
    assert TrainingsWorkerBasis.FRAGT == (g.HoleDatensatzSamplesDto,)


def test_training_worker_meldet_sich_als_client_und_bietet_seine_funktion_an():
    w = _training()
    req = w._build_capabilities_request()
    assert req.vertrag == "TrainingsWorker", "am Handshake steht der CLIENT (er leitet die Akteure ab)"
    assert req.vertrag_hash == TrainingsWorkerBasis.VERTRAG_HASH
    assert "TrainingAbgebrochen" in req.message_types
    assert w.funktionen.angebote == [("ITraining", 1)]


def test_abbruch_kommt_ueber_die_kenntnis_an():
    w = _training()
    import threading
    tid = uuid4()
    flag = threading.Event()
    w.state.abbruch[str(tid)] = flag
    aus = asyncio.run(_hoere(w, g.TrainingAbgebrochenDto(), MessageContext(aggregate_id=tid)))
    assert aus == [] and flag.is_set()


def test_stub_classifier_beurteilt_das_paar_mit_genau_einer_ausgabe():
    s = _stub()
    agg = uuid4()
    f = _getragen(s, "IBildpaarBeurteilung")
    h = Herkunft(stream=str(agg), version=3, zeit=datetime.now(timezone.utc))
    aus = asyncio.run(f.rufe(g.ImagePairKomplettDto(), h))
    assert isinstance(aus, g.KlassifiziereBildPaarDurchKiDto) and aus.aggregate_id == str(agg)
    assert f.pruefe(f.eingang(g.ImagePairKomplettDto), aus) == Ausgabe.COMMAND
    assert asyncio.run(_hoere(s, g.ModellAktiviertDto(pfad="m.pt", name="m"), MessageContext())) == []
    req = s._build_capabilities_request()
    assert req.vertrag == "KlassifikationsWorker" and "ModellAktiviert" in req.message_types
    assert s.funktionen.angebote == [("IBildpaarBeurteilung", 1)]


def test_eine_kenntnis_gibt_nichts_aus():
    from run_stub import StubClassifier

    class Frech(StubClassifier):
        async def auf_bild_verfuegbar(self, event, ctx, state):
            yield g.KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=2)

    f = Frech(registry=create_registry(), nutzlast=NUTZLAST)
    with pytest.raises(VertragsVerletzung, match="nur Kenntnis"):
        asyncio.run(_hoere(f, g.BildVerfuegbarDto(version=0, pfad="a.png"), MessageContext(aggregate_id=uuid4())))


def test_ohne_jede_methode_des_vertrags_startet_kein_worker():
    class Unvollstaendig(KlassifikationsWorkerBasis[SimpleNamespace]):
        async def bildpaar_beurteilung(self, e, h, state):
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
    Die Nutzlast-Tabelle (generated/nutzlast_tabelle.py) ist ein Generat derselben Codegen-Lesart wie die Verträge und Funktions-Basen:
    alles, was ein Worker empfängt oder sendet, steht darin — sonst ginge es nicht über die Leitung.
    """
    from domain_client.generated import funktionen, vertraege
    import inspect

    klassen = set(NUTZLAST.values())
    assert len(klassen) == len(NUTZLAST), "jede DTO-Klasse unter genau einem Typ"
    noetig: set[type] = set()
    def eigene(modul):   # nur die generierten Basen, nicht die importierten SDK-Klassen
        return [k for _, k in inspect.getmembers(modul, inspect.isclass) if k.__module__ == modul.__name__]

    for basis in eigene(vertraege):
        for eingaenge in basis.__dict__.get("TRAEGT", {}).values():
            for eingang, e in eingaenge.items():
                noetig |= {eingang, *e.ausgaben}
        noetig |= set(basis.__dict__.get("HOERT", {}))
        noetig |= set(basis.__dict__.get("SPONTAN", ())) | set(basis.__dict__.get("FRAGT", ()))
    for basis in eigene(funktionen):
        for eingang, e in basis.__dict__.get("EINGAENGE", {}).items():
            noetig |= {eingang, *e.ausgaben}
    assert noetig, "die Beispielanwendung hat Verträge und Funktionen"
    assert not {t.__name__ for t in noetig - klassen}
