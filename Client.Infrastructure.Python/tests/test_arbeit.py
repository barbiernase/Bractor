"""
Arbeit für Akteure (docs/konzept-akteure.md §5: IHoert eines Commands ohne Ziel bei einem verkörperten Akteur) — domänenfrei, mit minimalen
betterproto-Fakes.

Bewiesen wird der ArbeitsLaeufer des SDK: Annehmen nach dem Handshake (Akteur + Plätze), Zuteilung über `fuer`, den Command auspacken,
`auf_<command>(c, h)` höchstens Plätze-mal gleichzeitig, Lebenszeichen während langer Läufe, Prüfung der Antworten gegen das IDarf des Akteurs
(falscher Typ oder Ausnahme → Fehler statt Antwort), die Antwort als Commands in einer Nachricht, Stücke mit Stelle und am Ende fertig — und der
Weg vom Read-Loop über die Verarbeitungsschleife zum Läufer.
"""

import asyncio
from dataclasses import dataclass
from types import SimpleNamespace

import betterproto
import pytest

from cqrs_client import AkteursArbeit, Arbeit, ArbeitsLaeufer, Herkunft
from cqrs_client.client import CqrsClient
from cqrs_client.leitung import ArbeitsAuftrag, Nutzlast, ServerMessage
from cqrs_client.mapper import PayloadMapper
from cqrs_client.router import MessageRouter


@dataclass
class PruefeDto(betterproto.Message):
    pfad: str = betterproto.string_field(1)


@dataclass
class BewerteDto(betterproto.Message):
    pfad: str = betterproto.string_field(1)


@dataclass
class VerwirfDto(betterproto.Message):
    grund: str = betterproto.string_field(1)


@dataclass
class MeldeDto(betterproto.Message):
    n: int = betterproto.int32_field(1)


@dataclass
class FremdDto(betterproto.Message):
    x: int = betterproto.int32_field(1)


# Die „generierte Nutzlast-Tabelle“ (domain_client/generated/nutzlast_tabelle.py). Hüllen und ServerMessage kommen aus der echten Leitung.
GEN = {"Pruefe": PruefeDto, "Bewerte": BewerteDto, "Verwirf": VerwirfDto, "Melde": MeldeDto, "Fremd": FremdDto}


class Pruefer:
    """So sieht ein Client mit Arbeit aus: Methoden `auf_<command>(c, h, state)`; hier ohne Zustand."""

    def __init__(self, dauer=0.0, liefere=None):
        self.dauer = dauer
        self.liefere = liefere
        self.gleichzeitig = 0
        self.max_gleichzeitig = 0
        self.gesehen: list[tuple[PruefeDto, Herkunft]] = []

    async def auf_pruefe(self, c, h):
        self.gesehen.append((c, h))
        self.gleichzeitig += 1
        self.max_gleichzeitig = max(self.max_gleichzeitig, self.gleichzeitig)
        try:
            await asyncio.sleep(self.dauer)
            if self.liefere is not None:
                return self.liefere(c)
            return BewerteDto(pfad=c.pfad + ".ok")
        finally:
            self.gleichzeitig -= 1

    def arbeit(self, plaetze=None) -> AkteursArbeit:
        return AkteursArbeit("Gutachter", {PruefeDto: Arbeit("auf_pruefe", (BewerteDto, VerwirfDto))},
                             lambda methode, c, h: getattr(self, methode)(c, h), plaetze=plaetze)


class Zaehler:
    async def auf_pruefe(self, c, h):
        yield MeldeDto(n=0)
        yield [MeldeDto(n=1), MeldeDto(n=2)]   # ein Stück aus zwei Commands
        yield MeldeDto(n=3)

    def arbeit(self) -> AkteursArbeit:
        return AkteursArbeit("Zaehlwerk", {PruefeDto: Arbeit("auf_pruefe", (MeldeDto,))}, lambda methode, c, h: getattr(self, methode)(c, h))


class FakeProxy:
    def __init__(self):
        self.angebote = []
        self.ergebnisse = []
        self.lebt = []

    async def send_arbeit_annehmen(self, angebote):
        self.angebote.append(list(angebote))

    async def send_arbeits_ergebnis(self, vorgang, commands=None, ab=0, fertig=True, fehler=""):
        self.ergebnisse.append((vorgang, list(commands or []), ab, fertig, fehler))

    async def send_arbeit_lebt(self, vorgang):
        self.lebt.append(vorgang)


def _auftrag(vorgang="v1", fuer="Gutachter", pfad="/roh/a.tiff"):
    return ArbeitsAuftrag(vorgang=vorgang, korrelation="k1", fuer=fuer, eingang=Nutzlast(typ="Pruefe", daten=bytes(PruefeDto(pfad=pfad))),
                          akteur="KameraSystem", stream="s1", version=3, zeit_utc=1_700_000_000_000)


def sync(fn):
    """pytest-asyncio ist nicht überall installiert (gpu_env) — async-Tests laufen über asyncio.run."""
    def lauf():
        asyncio.run(fn())
    lauf.__name__ = fn.__name__
    return lauf


def _laeufer(*arbeit, lebenszeichen_s=10.0):
    proxy = FakeProxy()
    return ArbeitsLaeufer(list(arbeit), proxy, PayloadMapper(GEN), lebenszeichen_s=lebenszeichen_s), proxy


@sync
async def test_annehmen_nennt_akteur_und_plaetze():
    laeufer, proxy = _laeufer(Pruefer().arbeit(plaetze=2), Zaehler().arbeit())
    await laeufer.nimm_an()
    assert proxy.angebote == [[("Gutachter", 2), ("Zaehlwerk", 1)]]


@sync
async def test_ohne_arbeit_wird_nichts_angenommen():
    laeufer, proxy = _laeufer()
    await laeufer.nimm_an()
    assert proxy.angebote == []


@sync
async def test_auftrag_wird_mit_herkunft_gerufen_und_die_antwort_geht_als_commands_zurueck():
    p = Pruefer()
    laeufer, proxy = _laeufer(p.arbeit())

    await laeufer.nimm(_auftrag())

    c, h = p.gesehen[0]
    assert c == PruefeDto(pfad="/roh/a.tiff")
    assert (h.stream, h.version, h.akteur, h.korrelation, h.schluessel) == ("s1", 3, "KameraSystem", "k1", "v1")
    vorgang, commands, ab, fertig, fehler = proxy.ergebnisse[0]
    assert (vorgang, ab, fertig, fehler) == ("v1", 0, True, "")
    [nutzlast] = commands
    assert nutzlast.typ == "Bewerte"
    assert BewerteDto().parse(nutzlast.daten) == BewerteDto(pfad="/roh/a.tiff.ok")
    assert laeufer.laufend == 0


@sync
async def test_eine_liste_geht_in_einer_nachricht_und_nichts_ist_eine_leere_fertige_antwort():
    laeufer, proxy = _laeufer(Pruefer(liefere=lambda c: [BewerteDto(pfad="a"), VerwirfDto(grund="b")]).arbeit())
    await laeufer.nimm(_auftrag("v1"))
    laeufer2, proxy2 = _laeufer(Pruefer(liefere=lambda c: None).arbeit())
    await laeufer2.nimm(_auftrag("v2"))

    [(vorgang, commands, ab, fertig, fehler)] = proxy.ergebnisse
    assert (vorgang, [n.typ for n in commands], ab, fertig, fehler) == ("v1", ["Bewerte", "Verwirf"], 0, True, "")
    assert proxy2.ergebnisse == [("v2", [], 0, True, "")]


@sync
async def test_antwort_ausserhalb_des_idarf_und_ausnahme_werden_zum_fehler():
    laeufer, proxy = _laeufer(Pruefer(liefere=lambda c: FremdDto(x=1)).arbeit())
    await laeufer.nimm(_auftrag("v1"))

    def wirft(c):
        raise IOError("Datei gesperrt")
    laeufer2, proxy2 = _laeufer(Pruefer(liefere=wirft).arbeit())
    await laeufer2.nimm(_auftrag("v2"))

    assert proxy.ergebnisse[0][1] == [] and "ArbeitsVerletzung" in proxy.ergebnisse[0][4] and "FremdDto" in proxy.ergebnisse[0][4]
    assert proxy2.ergebnisse[0][1] == [] and "Datei gesperrt" in proxy2.ergebnisse[0][4]


@sync
async def test_ein_command_den_der_akteur_nicht_hoert_wird_zum_fehler():
    laeufer, proxy = _laeufer(Pruefer().arbeit())
    auftrag = _auftrag()
    auftrag.eingang = Nutzlast(typ="Melde", daten=bytes(MeldeDto(n=1)))
    await laeufer.nimm(auftrag)
    assert "hört MeldeDto nicht" in proxy.ergebnisse[0][4]


@sync
async def test_stuecke_gehen_sofort_mit_stelle_und_enden_fertig():
    laeufer, proxy = _laeufer(Zaehler().arbeit())
    await laeufer.nimm(_auftrag(fuer="Zaehlwerk"))

    assert [(ab, fertig, [MeldeDto().parse(n.daten).n for n in commands]) for _, commands, ab, fertig, _ in proxy.ergebnisse] == \
        [(0, False, [0]), (1, False, [1, 2]), (3, False, [3]), (4, True, [])]


@sync
async def test_hoechstens_plaetze_gleichzeitig_und_derselbe_auftrag_laeuft_nur_einmal():
    p = Pruefer(dauer=0.03)
    laeufer, proxy = _laeufer(p.arbeit(plaetze=2))

    tasks = [laeufer.nimm(_auftrag(f"v{i}")) for i in range(5)]
    assert laeufer.nimm(_auftrag("v0")) is tasks[0], "erneut zugeteilt → kein zweiter Lauf"
    await laeufer.warte_auf_alle()

    assert p.max_gleichzeitig == 2
    assert sorted(v for v, *_ in proxy.ergebnisse) == [f"v{i}" for i in range(5)]


@sync
async def test_lebenszeichen_waehrend_langer_laeufe_und_danach_keins_mehr():
    laeufer, proxy = _laeufer(Pruefer(dauer=0.08).arbeit(), lebenszeichen_s=0.02)
    await laeufer.nimm(_auftrag("lang"))
    n = len(proxy.lebt)
    assert n >= 2 and set(proxy.lebt) == {"lang"}
    await asyncio.sleep(0.06)
    assert len(proxy.lebt) == n, "nach der Antwort kein Lebenszeichen mehr"


@sync
async def test_auftrag_fuer_einen_fremden_akteur_wird_nicht_gerechnet():
    p = Pruefer()
    laeufer, proxy = _laeufer(p.arbeit())
    assert laeufer.nimm(_auftrag(fuer="Anderer")) is None
    await asyncio.sleep(0)
    assert p.gesehen == [] and proxy.ergebnisse == []


def test_nur_akteurs_arbeit_und_kein_akteur_doppelt():
    with pytest.raises(TypeError):
        ArbeitsLaeufer([object()], FakeProxy(), PayloadMapper(GEN))
    with pytest.raises(ValueError):
        ArbeitsLaeufer([Pruefer().arbeit(), Pruefer().arbeit()], FakeProxy(), PayloadMapper(GEN))


@sync
async def test_read_loop_und_verarbeitungsschleife_reichen_den_auftrag_an_den_laeufer():
    p = Pruefer()
    client = CqrsClient(registry=SimpleNamespace(), nutzlast=GEN, arbeit=[p.arbeit(plaetze=2)])
    proxy = client._proxy
    client._arbeit._proxy = fake = FakeProxy()
    assert client.arbeit.angebote == [("Gutachter", 2)]

    nachrichten = [ServerMessage(arbeits_auftrag=_auftrag("v9")), None]

    class Strom:
        async def recv_message(self):
            await asyncio.sleep(0)
            return nachrichten.pop(0)

    proxy._stream = Strom()
    proxy._connected = True
    lesen = asyncio.ensure_future(proxy.read_loop())
    await asyncio.sleep(0.01)
    proxy._connected = True   # read_loop beendet sich beim Stream-Ende; die Schleife soll die Queue noch leeren
    verarbeiten = asyncio.ensure_future(MessageRouter().process_loop(
        proxy, client.handle, client, None, client._mapper, SimpleNamespace(), None, on_arbeit=client.arbeit.nimm))
    for _ in range(100):
        if fake.ergebnisse:
            break
        await asyncio.sleep(0.01)
    proxy._connected = False
    verarbeiten.cancel()
    await asyncio.gather(lesen, verarbeiten, return_exceptions=True)

    assert [v for v, _, _, _, fehler in fake.ergebnisse if not fehler] == ["v9"]
    assert p.gesehen[0][0].pfad == "/roh/a.tiff"
