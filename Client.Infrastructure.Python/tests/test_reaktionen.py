"""
Reaktionen eines Clients (Schritt 6): der Läufer liest ab dem gesicherten Stand, sendet mit abgeleiteten Ids (jedes Stück, sobald es entsteht,
mit dem Event als Ursache) und sichert den Stand erst nach den Antworten. Ohne Server: eine Leitung im Speicher (Log nach Position, Quittung je Command, außer sie schweigt).
"""

import asyncio
from types import SimpleNamespace

from cqrs_client.reaktionen import (ReaktionsLaeufer, ReaktionsStandImSpeicher, DateiReaktionsStand, ableite, command_id)
from cqrs_client.registry import MessageCategory


class Bestellt(SimpleNamespace):
    pass


class Buche(SimpleNamespace):
    pass


class Leitung:
    def __init__(self, log, schweigt=0, horizont=None):
        self.log = log                       # [(position, payload)]
        self.horizont = horizont             # was die Seite als Horizont meldet (None = nichts)
        self.gesendet = []                   # [(command_id, command)]
        self.schweigt = schweigt
        self.is_connected = True
        self.session_id = "s1"
        self.laeufer = None
        self.ursachen = []

    async def lies_log(self, ab, hoechstens=500):
        neu = [SimpleNamespace(position=p, envelope=SimpleNamespace(payload=e, aggregate_id=STREAM, aggregate_type="", aggregate_version=p,
                                                                    correlation_id=""))
               for p, e in self.log if p > ab]
        bis = max([ab] + [p for p, _ in self.log])
        seite = SimpleNamespace(eintraege=neu, bis=bis, voll=False)
        if self.horizont is not None:
            seite.horizont = self.horizont
        return seite

    async def send_command(self, envelope):
        self.gesendet.append((envelope.command_id, envelope.command))
        self.ursachen.append(envelope.ursache)
        if self.schweigt > 0:
            self.schweigt -= 1
            return
        self.laeufer.angekommen(SimpleNamespace(envelope=SimpleNamespace(correlation_id=envelope.correlation_id)))


class Mapper:
    def extract_event_payload(self, envelope):
        return envelope.payload

    def wrap_command(self, command, aggregate_id, expected_version, correlation_id, session_id):
        assert expected_version == -1
        return SimpleNamespace(command=command, command_id="", correlation_id=correlation_id)


STREAM = "6f1c2a40-0000-4000-8000-000000000001"


class Handle:
    def __init__(self):
        self.leitung = None
        self.vor_dem_zweiten = []    # wie viele Commands gesendet waren, als das zweite Stück entstand

    def get_handler(self, typ):
        return typ is Bestellt or None

    async def receive(self, instance, payload, ctx, state):
        yield Buche(kunde=payload.kunde, betrag=payload.betrag)
        self.vor_dem_zweiten.append(len(self.leitung.gesendet))
        yield Buche(kunde=payload.kunde, betrag=0)


registry = SimpleNamespace(classify=lambda t: MessageCategory.COMMAND)


def _handle(leitung):
    handle = Handle()
    handle.leitung = leitung
    leitung.handle = handle
    return handle


async def laufe_bis(leitung, stand, bis):
    handle = _handle(leitung)
    laeufer = ReaktionsLaeufer(leitung, Mapper(), registry, handle, None, None, None, stand, "buchhaltung", takt=0.05, antwortfrist=0.2)
    leitung.laeufer = laeufer
    lauf = asyncio.ensure_future(laeufer.lauf())
    for _ in range(200):
        if stand.lade("buchhaltung") >= bis:
            break
        await asyncio.sleep(0.02)
    lauf.cancel()
    try:
        await lauf
    except asyncio.CancelledError:
        pass
    assert stand.lade("buchhaltung") == bis


def test_dieselbe_formel_wie_csharp():
    # Herkunft.Ableite in C#: MD5 der UTF-8-Bytes als Guid (little-endian) — dieselbe Id in beiden Sprachen.
    assert command_id("a", 3, 0, 1) == "d0825625-5785-b876-04cb-8b1b44d5a2e6"   # derselbe Wert prüft ReaktionenTests in C#


async def test_reaktion_sendet_mit_abgeleiteten_ids_und_sichert_den_stand():
    leitung = Leitung([(3, Bestellt(kunde="k", betrag=10)), (5, Bestellt(kunde="k", betrag=20))])
    stand = ReaktionsStandImSpeicher()
    await laufe_bis(leitung, stand, 5)
    assert [i for i, _ in leitung.gesendet] == [command_id("buchhaltung", 3, 0, 0), command_id("buchhaltung", 3, 0, 1),
                                                 command_id("buchhaltung", 5, 0, 0), command_id("buchhaltung", 5, 0, 1)]


async def test_jedes_stueck_geht_hinaus_sobald_es_entsteht_mit_dem_event_als_ursache():
    leitung = Leitung([(3, Bestellt(kunde="k", betrag=10))])
    await laufe_bis(leitung, ReaktionsStandImSpeicher(), 3)
    assert leitung.handle.vor_dem_zweiten == [1], "das erste Stück war gesendet, bevor das zweite entstand"
    assert leitung.ursachen == [f"{STREAM}:3", f"{STREAM}:3"]


async def test_neustart_ab_stand_sendet_nichts_noch_einmal_ohne_stand_dieselben_ids():
    leitung = Leitung([(3, Bestellt(kunde="k", betrag=10))])
    stand = ReaktionsStandImSpeicher()
    await laufe_bis(leitung, stand, 3)
    await laufe_bis(leitung, stand, 3)
    assert len(leitung.gesendet) == 2
    await laufe_bis(leitung, ReaktionsStandImSpeicher(), 3)
    ids = [i for i, _ in leitung.gesendet]
    assert len(ids) == 4 and len(set(ids)) == 2


async def test_bei_stille_mit_derselben_id_noch_einmal():
    leitung = Leitung([(3, Bestellt(kunde="k", betrag=10))], schweigt=1)
    await laufe_bis(leitung, ReaktionsStandImSpeicher(), 3)
    erste = command_id("buchhaltung", 3, 0, 0)
    assert [i for i, _ in leitung.gesendet] == [erste, erste, command_id("buchhaltung", 3, 0, 1)]


async def test_ein_log_unter_dem_eigenen_stand_ist_neu_der_laeufer_liest_von_vorn():
    leitung = Leitung([(3, Bestellt(kunde="k", betrag=10))], horizont=3)
    stand = ReaktionsStandImSpeicher()
    stand.sichere("buchhaltung", 50)   # der Stand einer früheren Datenbank
    laeufer = ReaktionsLaeufer(leitung, Mapper(), registry, _handle(leitung), None, None, None, stand, "buchhaltung", takt=0.05, antwortfrist=0.2)
    leitung.laeufer = laeufer
    lauf = asyncio.ensure_future(laeufer.lauf())
    for _ in range(200):
        if len(leitung.gesendet) >= 2 and stand.lade("buchhaltung") == 3:
            break
        await asyncio.sleep(0.02)
    lauf.cancel()
    try:
        await lauf
    except asyncio.CancelledError:
        pass
    assert [i for i, _ in leitung.gesendet] == [command_id("buchhaltung", 3, 0, 0), command_id("buchhaltung", 3, 0, 1)]
    assert stand.lade("buchhaltung") == 3


def test_dateistand(tmp_path):
    DateiReaktionsStand(tmp_path).sichere("buchhaltung", 42)
    assert DateiReaktionsStand(tmp_path).lade("buchhaltung") == 42
    assert DateiReaktionsStand(tmp_path).lade("andere") == 0
