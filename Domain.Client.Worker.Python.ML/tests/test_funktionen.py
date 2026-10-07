"""
Katalog-Funktionen in Python anbieten (docs/konzept-editor-pipelines.md §14.5) — gegen das ECHTE Generat.

`domain_client/generated/funktionen.py` (Cqrs.Codegen) bindet je Funktion Auftrag und Ergebnisse an die betterproto-Typen;
ein Worker erbt die Basis und implementiert nur `rufe`. Bewiesen wird der ganze Draht des Workers ohne Server: der echte
GrpcProxy baut die ClientMessages (FunktionenAnbieten, ArbeitsErgebnis, ArbeitLebtMeldung) aus dem Generat, sie überstehen
den Byte-Round-trip, und ein ArbeitsAuftrag vom Server kommt als typisierter Auftrag in `rufe` an.
"""

import asyncio

import betterproto
import pytest

import domain_client.generated as g
from cqrs_client import Ausfuehrung, FunktionsBasis, FunktionsLaeufer
from cqrs_client.mapper import PayloadMapper
from cqrs_client.proxy import GrpcProxy
from domain_client.generated.funktionen import BildVerkleinerungBasis, HistogrammAusgleichBasis


class Verkleinerer(BildVerkleinerungBasis):
    SLOTS = 3

    async def rufe(self, auftrag: g.VerkleinereBildDto, x: Ausfuehrung):
        if auftrag.quell_pfad.endswith(".kaputt"):
            return g.BildNichtLesbarDto(pfad=auftrag.quell_pfad, grund="unlesbar")
        return g.BildVerkleinertDto(pfad=auftrag.quell_pfad + ".klein.png", breite_pixel=640, hoehe_pixel=auftrag.hoehe)


class MitschreibenderStrom:
    """Statt des HTTP/2-Streams: hält jede gesendete ClientMessage als BYTES fest (der echte Draht)."""

    def __init__(self):
        self.gesendet: list[bytes] = []

    async def send_message(self, msg):
        self.gesendet.append(bytes(msg))

    def nachrichten(self):
        return [g.ClientMessage().parse(b) for b in self.gesendet]


def _verbunden():
    proxy = GrpcProxy(g)
    proxy._stream = strom = MitschreibenderStrom()
    proxy._connected = True
    return proxy, strom


def _auftrag_vom_server(vorgang, pfad):
    """So kommt er an: ServerMessage-Bytes → betterproto (wie im Read-Loop)."""
    msg = g.ServerMessage(arbeits_auftrag=g.ArbeitsAuftrag(
        vorgang=vorgang, korrelation="k-1", funktion="IBildVerkleinerung", akteur="KameraSystem",
        auftrag=g.AuftragPayloadDto(verkleinere_bild=g.VerkleinereBildDto(quell_pfad=pfad, hoehe=512))))
    feld, auftrag = betterproto.which_one_of(g.ServerMessage().parse(bytes(msg)), "message")
    assert feld == "arbeits_auftrag"
    return auftrag


def test_generat_bindet_auftrag_und_ergebnisse_an_die_betterproto_typen():
    assert issubclass(BildVerkleinerungBasis, FunktionsBasis)
    assert BildVerkleinerungBasis.FUNKTION == "IBildVerkleinerung"
    assert BildVerkleinerungBasis.AUFTRAG is g.VerkleinereBildDto
    assert BildVerkleinerungBasis.ERGEBNISSE == (g.BildVerkleinertDto, g.BildNichtLesbarDto)
    assert HistogrammAusgleichBasis.ERGEBNISSE == (g.HistogrammAusgeglichenDto, g.BildNichtLesbarDto)
    with pytest.raises(TypeError):
        BildVerkleinerungBasis()   # abstrakt: rufe fehlt


def test_anbieten_ueber_den_echten_draht():
    proxy, strom = _verbunden()
    laeufer = FunktionsLaeufer([Verkleinerer()], proxy, PayloadMapper(g))

    asyncio.run(laeufer.biete_an())

    feld, anbieten = betterproto.which_one_of(strom.nachrichten()[0], "message")
    assert feld == "funktionen_anbieten"
    assert [(a.funktion, a.slots) for a in anbieten.angebote] == [("IBildVerkleinerung", 3)]


def test_auftrag_rein_ergebnis_raus_ueber_den_echten_draht():
    proxy, strom = _verbunden()
    laeufer = FunktionsLaeufer([Verkleinerer()], proxy, PayloadMapper(g))

    async def lauf():
        await asyncio.gather(laeufer.nimm(_auftrag_vom_server("v-1", "/roh/a.tiff")),
                             laeufer.nimm(_auftrag_vom_server("v-2", "/roh/b.kaputt")))
    asyncio.run(lauf())

    ergebnisse = {}
    for m in strom.nachrichten():
        feld, e = betterproto.which_one_of(m, "message")
        assert feld == "arbeits_ergebnis"
        ergebnisse[e.vorgang] = e
    a, b = ergebnisse["v-1"], ergebnisse["v-2"]
    assert a.fehler == "" and betterproto.which_one_of(a.ergebnis, "payload") == (
        "bild_verkleinert", g.BildVerkleinertDto(pfad="/roh/a.tiff.klein.png", breite_pixel=640, hoehe_pixel=512))
    assert a.ergebnis.aggregate_id == "v-1" and a.ergebnis.correlation_id == "k-1"
    assert betterproto.which_one_of(b.ergebnis, "payload")[0] == "bild_nicht_lesbar", "auch der zweite OneOf-Fall ist ein Ergebnis"


def test_falsches_ergebnis_wird_zum_fehler_und_lebenszeichen_reisen_ueber_den_draht():
    class Frech(BildVerkleinerungBasis):
        async def rufe(self, auftrag, x):
            await asyncio.sleep(0.05)
            return g.HistogrammAusgeglichenDto(pfad="x")   # Ergebnis einer ANDEREN Funktion

    proxy, strom = _verbunden()
    laeufer = FunktionsLaeufer([Frech()], proxy, PayloadMapper(g), lebenszeichen_s=0.015)
    async def lauf():
        await laeufer.nimm(_auftrag_vom_server("v-3", "/roh/c.tiff"))
    asyncio.run(lauf())

    felder = [betterproto.which_one_of(m, "message") for m in strom.nachrichten()]
    assert any(f == "arbeit_lebt" and x.vorgang == "v-3" for f, x in felder)
    feld, ergebnis = felder[-1]
    assert feld == "arbeits_ergebnis" and "HistogrammAusgeglichenDto" in ergebnis.fehler
    assert not betterproto.serialized_on_wire(ergebnis.ergebnis), "kein Ergebnis-Envelope bei Fehler"
