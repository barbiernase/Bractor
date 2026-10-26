"""
Funktionen draußen tragen (Plan Pub/Sub-Neustrukturierung §2.7) — gegen das ECHTE Generat.

`domain_client/generated/funktionen.py` (Cqrs.Codegen) bindet je Funktion ihre Eingänge und Ausgaben an die betterproto-Typen; ein Worker erbt die
Basis und füllt `auf_<eingang>(e, h)`. Bewiesen wird der ganze Draht des Workers ohne Server: der echte GrpcProxy baut die ClientMessages
(FunktionenAnbieten, ArbeitsErgebnis, ArbeitLebtMeldung) aus der Leitung (cqrs_client.leitung), sie überstehen den Byte-Round-trip, und ein
ArbeitsAuftrag vom Server (Eingang als Nutzlast: typ + Bytes des Domänen-DTO, dazu die Herkunft) kommt als typisierter Eingang in `auf_…` an.
"""

import asyncio

import betterproto
import pytest

import domain_client.generated as g
from domain_client.generated.nutzlast_tabelle import TABELLE as NUTZLAST
from cqrs_client import Ausgabe, FunktionsBasis, FunktionsLaeufer, Herkunft
from cqrs_client import leitung as L
from cqrs_client.mapper import PayloadMapper
from cqrs_client.proxy import GrpcProxy
from domain_client.generated.funktionen import BildaufbereitungBasis, TrainingBasis


class Aufbereitung(BildaufbereitungBasis):
    SLOTS = 3

    async def auf_datei_erkannt(self, e: g.DateiErkanntDto, h: Herkunft):
        if e.pfad.endswith(".kaputt"):
            return g.BildNichtLesbarDto(pfad=e.pfad, grund="unlesbar")
        return g.BildAufbereitetDto(dateiname=e.name, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, vorschau=e.pfad + ".png",
                                    breite_pixel=640, hoehe_pixel=512, erstellt_am=e.erkannt_am)


class MitschreibenderStrom:
    """Statt des HTTP/2-Streams: hält jede gesendete ClientMessage als BYTES fest (der echte Draht)."""

    def __init__(self):
        self.gesendet: list[bytes] = []

    async def send_message(self, msg):
        self.gesendet.append(bytes(msg))

    def nachrichten(self):
        return [L.ClientMessage().parse(b) for b in self.gesendet]


def _verbunden():
    proxy = GrpcProxy()
    proxy._stream = strom = MitschreibenderStrom()
    proxy._connected = True
    return proxy, strom


def _eingang_vom_server(vorgang, pfad):
    """So kommt er an: ServerMessage-Bytes → betterproto (wie im Read-Loop)."""
    msg = L.ServerMessage(arbeits_auftrag=L.ArbeitsAuftrag(
        vorgang=vorgang, korrelation="k-1", funktion="IBildaufbereitung", akteur="KameraSystem", stream="s-1", version=1,
        zeit_utc=1_700_000_000_000,
        eingang=L.Nutzlast(typ="DateiErkannt", daten=bytes(g.DateiErkanntDto(name="a.tiff", pfad=pfad, groesse=7)))))
    feld, eingang = betterproto.which_one_of(L.ServerMessage().parse(bytes(msg)), "message")
    assert feld == "arbeits_auftrag"
    return eingang


def test_generat_bindet_eingaenge_und_ausgaben_an_die_betterproto_typen():
    assert issubclass(BildaufbereitungBasis, FunktionsBasis)
    assert BildaufbereitungBasis.FUNKTION == "IBildaufbereitung"
    e = BildaufbereitungBasis.EINGAENGE[g.DateiErkanntDto]
    assert e.methode == "auf_datei_erkannt" and not e.strom
    assert e.ausgaben == {g.BildAufbereitetDto: Ausgabe.EVENT, g.BildNichtLesbarDto: Ausgabe.EVENT}
    t = TrainingBasis.EINGAENGE[g.TrainingAngefordertDto]
    assert t.strom and all(a == Ausgabe.COMMAND for a in t.ausgaben.values())
    with pytest.raises(TypeError):
        BildaufbereitungBasis()   # abstrakt: auf_datei_erkannt fehlt


def test_anbieten_ueber_den_echten_draht():
    proxy, strom = _verbunden()
    laeufer = FunktionsLaeufer([Aufbereitung()], proxy, PayloadMapper(NUTZLAST))

    asyncio.run(laeufer.biete_an())

    feld, anbieten = betterproto.which_one_of(strom.nachrichten()[0], "message")
    assert feld == "funktionen_anbieten"
    assert [(a.funktion, a.slots) for a in anbieten.angebote] == [("IBildaufbereitung", 3)]


def test_eingang_rein_antwort_raus_ueber_den_echten_draht():
    proxy, strom = _verbunden()
    laeufer = FunktionsLaeufer([Aufbereitung()], proxy, PayloadMapper(NUTZLAST))

    async def lauf():
        await asyncio.gather(laeufer.nimm(_eingang_vom_server("v-1", "/roh/a.tiff")),
                             laeufer.nimm(_eingang_vom_server("v-2", "/roh/b.kaputt")))
    asyncio.run(lauf())

    antworten = {}
    for m in strom.nachrichten():
        feld, e = betterproto.which_one_of(m, "message")
        assert feld == "arbeits_ergebnis"
        antworten[e.vorgang] = e
    a, b = antworten["v-1"], antworten["v-2"]
    mapper = PayloadMapper(NUTZLAST)
    assert a.fehler == "" and a.fertig and len(a.ausgaben) == 1
    art, nutzlast = betterproto.which_one_of(a.ausgaben[0], "art")
    assert art == "event" and nutzlast.typ == "BildAufbereitet"
    assert mapper.entpacke(nutzlast).vorschau == "/roh/a.tiff.png"
    _, nb = betterproto.which_one_of(b.ausgaben[0], "art")
    assert nb.typ == "BildNichtLesbar", "auch der zweite OneOf-Fall ist eine Antwort"


def test_falsche_ausgabe_wird_zum_fehler_und_lebenszeichen_reisen_ueber_den_draht():
    class Frech(BildaufbereitungBasis):
        async def auf_datei_erkannt(self, e, h):
            await asyncio.sleep(0.05)
            return g.MeldeFortschrittDto()   # Ausgabe einer ANDEREN Funktion

    proxy, strom = _verbunden()
    laeufer = FunktionsLaeufer([Frech()], proxy, PayloadMapper(NUTZLAST), lebenszeichen_s=0.015)

    async def lauf():
        await laeufer.nimm(_eingang_vom_server("v-3", "/roh/c.tiff"))
    asyncio.run(lauf())

    felder = [betterproto.which_one_of(m, "message") for m in strom.nachrichten()]
    assert any(f == "arbeit_lebt" and x.vorgang == "v-3" for f, x in felder)
    feld, antwort = felder[-1]
    assert feld == "arbeits_ergebnis" and "MeldeFortschrittDto" in antwort.fehler
    assert len(antwort.ausgaben) == 0, "keine Ausgabe bei Fehler"
