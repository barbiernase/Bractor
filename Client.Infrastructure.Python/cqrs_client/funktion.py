# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/funktion.py
"""
Funktionen draußen TRAGEN (Plan Pub/Sub-Neustrukturierung §2.2, §2.7: `ITraegt<F>`, Laufort Extern).

Eine Funktion ist ein Vertrag in der Domäne (C#, eine Quelle) — als Schnittstelle deklariert, läuft sie draußen:

    public interface IBildaufbereitung : IFunktion
    {
        Task<OneOf<BildAufbereitet, BildNichtLesbar>> Auf(DateiErkannt e);
    }

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/funktionen.py` mit einer Basis je Funktion (`BildaufbereitungBasis`) und je `Auf` einer
abstrakten Methode `auf_<eingang>(e, h)`. Der Worker erbt sie und füllt die Methoden:

    class Bildaufbereitung(BildaufbereitungBasis):
        SLOTS = 4
        async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft):
            ...
            return BildAufbereitetDto(...)

Ein Client mit Vertrag (`ITraegt<F>` im Client-Vertrag) bekommt die getragenen Funktionen als Methoden seiner generierten Client-Basis
(`vertrag.py`) — dieselbe Mechanik, nur mit Zugriff auf seinen Zustand.

Was das SDK übernimmt — der Worker schreibt nichts davon von Hand:
  - Anbieten: nach dem Handshake (und nach jedem Reconnect) `FunktionenAnbieten` (Funktion + Slots);
  - Pull: der Server holt Eingänge beim Vermittler, höchstens so viele wie Slots — hier laufen höchstens SLOTS gleichzeitig;
  - Lebenszeichen: alle 10 s `ArbeitLebtMeldung`, solange `auf_…` läuft (lange Läufe verlieren ihre Lease nicht);
  - Signatur: jede Ausgabe muss ein Fall der Signatur sein, sonst geht ein Fehler hinaus; eine Ausnahme in `auf_…` ebenso;
  - Strom: ein `async for`-Generator (Training) schickt jede Ausgabe sofort als Stück — der Server hält es fest und sendet es, bevor das nächste
    kommt; nach einem Abbruch übernimmt ein anderer Träger beim nächsten nicht festgehaltenen Stück;
  - Genau einmal: festgehalten wird beim Server unter „Funktion, Eingang“ — die erste Antwort gewinnt. Wirkungen nach außen (Dateien, Mails)
    sollten über `h.schluessel` (gleich bei jeder Wiederholung) deduplizieren.
  - Hinweise (Plan §2.9): ein `auf_…`, dessen Eingang ein Hinweis aus dem Hinweis-Netz ist (`Eingang(..., hinweis=True)`, auf der Leitung
    `ArbeitsAuftrag.hinweis`), läuft höchstens einmal — nichts wird festgehalten; die Antwort geht als Ganzes zurück (auch ein Strom), ihre Commands
    und Hinweise sendet der Funktions-Actor. Dieselbe Adresse wie in C#: alle Eingänge eines Funktions-Actors rechnet derselbe Träger, solange er
    lebt — ein Zustand im Worker (ein Modell) sieht so alles, was zu ihm gehört; ihn zu sichern, ist Sache des Workers.
  - Lebenszyklus (optional, Gegenstück zu `ILebenszyklus` im Host): `async def beim_start(self)` läuft einmal, bevor der Worker die Funktion
    anbietet (ein Netz laden), `async def beim_ende(self)` einmal beim Herunterfahren (Ressourcen schließen, Zustand selbst sichern).
"""

from __future__ import annotations

import asyncio
import inspect
import logging
from abc import ABC
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any, AsyncIterator, Callable, ClassVar

log = logging.getLogger(__name__)

LEBENSZEICHEN_S = 10.0


class Ausgabe:
    """Die Art einer Ausgabe auf der Leitung (FunktionsAusgabe): ein Command, ein Event für das Log oder ein Hinweis."""
    COMMAND = "command"
    EVENT = "event"
    HINWEIS = "hinweis"


@dataclass(frozen=True)
class Eingang:
    """Ein `Auf` einer Funktion: die Methode, die erlaubten Ausgaben (DTO-Typ → Art), ob die Ausgabe ein Strom ist und ob der Eingang ein Hinweis
    ist (höchstens einmal, die Antwort als Ganzes)."""
    methode: str
    ausgaben: dict = field(default_factory=dict)
    strom: bool = False
    hinweis: bool = False


@dataclass(frozen=True)
class Herkunft:
    """Woher der Eingang kommt (Gegenstück zu `Abstractions.Herkunft`): Stream, Version, Log-Zeit, Akteur der Kette; dazu Korrelation und der
    Schlüssel „Funktion, Eingang“ (gleich bei jeder Wiederholung — zum Deduplizieren von Wirkungen nach außen)."""
    stream: str
    version: int
    zeit: datetime
    akteur: str = ""
    korrelation: str = ""
    schluessel: str = ""


class FunktionsVerletzung(TypeError):
    """Eine Funktion liefert etwas, das ihre Signatur nicht erlaubt (kein Fall, oder mehr als eine Ausgabe ohne Strom)."""


class FunktionsBasis(ABC):
    """
    Basis der generierten Funktions-Klassen. Die generierte Unterklasse setzt:
        FUNKTION  — Name der Funktions-Schnittstelle (z. B. "IBildaufbereitung"), so meldet sich der Worker an;
        EINGAENGE — Eingangs-DTO → Eingang (Methode `auf_<eingang>`, erlaubte Ausgaben, Strom).
    Der Worker setzt SLOTS (wie viele Eingänge gleichzeitig) — als Klassenattribut oder `slots=` im Konstruktor — und gibt die Werte der
    Implementierung als `konfig=` mit (Name → Text, z. B. ein Ausgabe-Verzeichnis).
    """

    FUNKTION: ClassVar[str] = ""
    EINGAENGE: ClassVar[dict] = {}
    SLOTS: ClassVar[int] = 1

    def __init__(self, slots: int | None = None, konfig: dict[str, str] | None = None):
        self.slots = max(1, slots if slots is not None else self.SLOTS)
        self.konfig: dict[str, str] = dict(konfig or {})

    async def beim_start(self) -> None:
        """Optional: einmal je Worker, bevor die Funktion angeboten wird — etwa ein Netz laden. Wirft es, startet der Worker nicht."""

    async def beim_ende(self) -> None:
        """Optional: einmal beim Herunterfahren — Ressourcen schließen, den eigenen Zustand sichern (das Framework sichert ihn draußen nicht)."""

    def eingang(self, typ: type) -> Eingang:
        e = self.EINGAENGE.get(typ)
        if e is None:
            erlaubt = ", ".join(t.__name__ for t in self.EINGAENGE)
            raise FunktionsVerletzung(f"{self.FUNKTION} hört {typ.__name__} nicht (Eingänge: {erlaubt})")
        return e

    def rufe(self, e: Any, h: Herkunft) -> Any:
        """Ruft das `Auf` des Eingangs (Wert, None, Coroutine oder Strom)."""
        return getattr(self, self.eingang(type(e)).methode)(e, h)

    def pruefe(self, eingang: Eingang, ausgabe: Any) -> str:
        """Signatur: die Ausgabe ist ein Fall des Eingangs — liefert ihre Art (command/event/hinweis)."""
        art = eingang.ausgaben.get(type(ausgabe))
        if art is None:
            erlaubt = ", ".join(t.__name__ for t in eingang.ausgaben) or "nichts"
            raise FunktionsVerletzung(
                f"{type(self).__name__}.{eingang.methode}: liefert {type(ausgabe).__name__} — die Funktion {self.FUNKTION} erlaubt: {erlaubt}")
        return art


class GetrageneFunktion(FunktionsBasis):
    """Eine Funktion, die ein Client-Vertrag trägt: die Methoden stehen am Client (mit seinem Zustand), nicht in einer eigenen Klasse."""

    def __init__(self, funktion: str, eingaenge: dict, rufe: Callable[[str, Any, Herkunft], Any], slots: int | None = None):
        super().__init__(slots=slots)
        self.FUNKTION = funktion
        self.EINGAENGE = eingaenge
        self._rufe = rufe

    def rufe(self, e: Any, h: Herkunft) -> Any:
        return self._rufe(self.eingang(type(e)).methode, e, h)


async def _ausgaben(ergebnis) -> AsyncIterator[Any]:
    """Async-/Sync-Generator, Coroutine oder Wert → die Ausgaben (None = nichts)."""
    if inspect.isasyncgen(ergebnis):
        async for x in ergebnis:
            if x is not None:
                yield x
    elif inspect.isawaitable(ergebnis):
        x = await ergebnis
        if x is not None:
            yield x
    elif inspect.isgenerator(ergebnis):
        for x in ergebnis:
            if x is not None:
                yield x
    elif ergebnis is not None:
        yield ergebnis


class FunktionsLaeufer:
    """
    Führt die angebotenen Funktionen eines Clients aus — transport-frei: gesendet wird über `proxy` (`send_funktionen_anbieten`,
    `send_arbeits_ergebnis`, `send_arbeit_lebt`), verpackt über `mapper` (`entpacke`: Nutzlast → DTO, `packe`: DTO → Nutzlast).
    """

    def __init__(self, funktionen, proxy, mapper, lebenszeichen_s: float = LEBENSZEICHEN_S):
        self._funktionen: dict[str, FunktionsBasis] = {}
        for f in funktionen or ():
            self.nimm_auf(f)
        self._proxy = proxy
        self._mapper = mapper
        self._lebenszeichen_s = lebenszeichen_s
        self._laufend: dict[str, asyncio.Task] = {}

    def nimm_auf(self, f: FunktionsBasis) -> None:
        if not isinstance(f, FunktionsBasis) or not f.FUNKTION:
            raise TypeError(f"{type(f).__name__} ist keine generierte Funktions-Basis (FUNKTION fehlt)")
        if f.FUNKTION in self._funktionen:
            raise ValueError(f"Funktion {f.FUNKTION} doppelt angeboten")
        self._funktionen[f.FUNKTION] = f

    @property
    def angebote(self) -> list[tuple[str, int]]:
        return [(name, f.slots) for name, f in self._funktionen.items()]

    @property
    def laufend(self) -> int:
        return len(self._laufend)

    async def starte(self) -> None:
        """Einmal je Worker, vor dem ersten Anbieten: `beim_start` jeder Funktion (der Lebenszyklus, Gegenstück zu `ILebenszyklus`)."""
        if getattr(self, "_gestartet", False):
            return
        for f in self._funktionen.values():
            await f.beim_start()
        self._gestartet = True

    async def beende(self) -> None:
        """Beim Herunterfahren: laufende Eingänge abwarten, dann `beim_ende` jeder Funktion — einmal, auch wenn eine davon wirft."""
        if not getattr(self, "_gestartet", False):
            return
        self._gestartet = False
        await self.warte_auf_alle()
        for f in self._funktionen.values():
            try:
                await f.beim_ende()
            except Exception as e:   # die übrigen trotzdem schließen
                log.warning("beim_ende von %s gescheitert: %s", f.FUNKTION, e)

    async def biete_an(self, _antwort=None) -> None:
        """Nach dem Handshake (und nach jedem Reconnect): Funktionen + Slots anmelden."""
        if not self._funktionen:
            return
        await self._proxy.send_funktionen_anbieten(self.angebote)
        log.info("Funktionen angeboten: %s", ", ".join(f"{n} ({s} Slots)" for n, s in self.angebote))

    def nimm(self, auftrag_msg) -> asyncio.Task | None:
        """Ein ArbeitsAuftrag (ein Eingang) vom Server: läuft entkoppelt (blockiert die Verarbeitungsschleife nicht)."""
        vorgang = auftrag_msg.vorgang
        if auftrag_msg.funktion not in self._funktionen:
            # Nie angeboten → nicht unsere Arbeit; die Lease läuft ab, der Vermittler gibt sie anderen.
            log.error("Eingang %s für %s — hier nicht angeboten", vorgang, auftrag_msg.funktion)
            return None
        if vorgang in self._laufend:
            return self._laufend[vorgang]   # derselbe Eingang erneut zugeteilt: läuft schon
        task = asyncio.ensure_future(self._fuehre_aus(auftrag_msg))
        self._laufend[vorgang] = task
        task.add_done_callback(lambda _t: self._laufend.pop(vorgang, None))
        return task

    async def warte_auf_alle(self) -> None:
        while self._laufend:
            await asyncio.gather(*list(self._laufend.values()), return_exceptions=True)

    def _slots(self, name: str) -> asyncio.Semaphore:
        if not hasattr(self, "_semaphoren"):
            self._semaphoren: dict[str, asyncio.Semaphore] = {}
        if name not in self._semaphoren:
            self._semaphoren[name] = asyncio.Semaphore(self._funktionen[name].slots)
        return self._semaphoren[name]

    async def _fuehre_aus(self, msg) -> None:
        f = self._funktionen[msg.funktion]
        h = Herkunft(stream=msg.stream, version=msg.version,
                     zeit=datetime.fromtimestamp(msg.zeit_utc / 1000, tz=timezone.utc),
                     akteur=msg.akteur, korrelation=msg.korrelation, schluessel=msg.vorgang)
        async with self._slots(msg.funktion):
            herz = asyncio.ensure_future(self._lebenszeichen(msg.vorgang))
            # Ein Strom, den ein früherer Träger begonnen hat: so viele Ausgaben sind schon festgehalten — sie werden ausgelassen.
            schon = getattr(msg, "ab", 0) or 0
            gesendet = 0
            try:
                try:
                    e = self._mapper.entpacke(msg.eingang)
                    eingang = f.eingang(type(e))
                    # Ein Hinweis als Eingang: nichts ist festgehalten — die Antwort geht als Ganzes zurück (auch ein Strom).
                    hinweis = bool(getattr(msg, "hinweis", False)) or eingang.hinweis
                    stueckweise = eingang.strom and not hinweis
                    puffer = []
                    async for aus in _ausgaben(f.rufe(e, h)):
                        art = f.pruefe(eingang, aus)
                        if stueckweise and gesendet < schon:
                            gesendet += 1
                            continue
                        if stueckweise:
                            # Ein Strom: jedes Stück sofort — der Server hält es fest und sendet es, bevor das nächste kommt.
                            await self._proxy.send_arbeits_ergebnis(msg.vorgang, [(art, self._mapper.packe(aus))], ab=gesendet, fertig=False)
                            gesendet += 1
                        else:
                            puffer.append((art, self._mapper.packe(aus)))
                            if len(puffer) > 1 and not eingang.strom:
                                raise FunktionsVerletzung(f"{type(f).__name__}.{eingang.methode}: mehr als eine Ausgabe — die Signatur ist kein Strom")
                    await self._proxy.send_arbeits_ergebnis(msg.vorgang, puffer, ab=gesendet, fertig=True)
                    log.info("→ Antwort %s (%s): %d Ausgabe(n)", msg.vorgang[:8], f.FUNKTION, gesendet + len(puffer))
                except asyncio.CancelledError:
                    raise
                except Exception as fehler:   # gescheitert → zählt beim Funktions-Actor gegen die Wiederholungen
                    log.warning("Funktion %s (%s) gescheitert: %s", f.FUNKTION, msg.vorgang, fehler)
                    try:
                        await self._proxy.send_arbeits_ergebnis(msg.vorgang, [], fehler=f"{type(fehler).__name__}: {fehler}")
                    except Exception as e2:
                        log.warning("Fehler zu %s nicht zustellbar: %s", msg.vorgang, e2)
            finally:
                herz.cancel()
                try:
                    await herz
                except asyncio.CancelledError:
                    pass

    async def _lebenszeichen(self, vorgang: str) -> None:
        while True:
            await asyncio.sleep(self._lebenszeichen_s)
            try:
                await self._proxy.send_arbeit_lebt(vorgang)
            except Exception as e:   # verlierbar: die nächste Meldung (oder die Lease) heilt
                log.debug("Lebenszeichen %s verloren: %s", vorgang, e)


__all__ = ["Ausgabe", "Eingang", "Herkunft", "FunktionsBasis", "GetrageneFunktion", "FunktionsLaeufer", "FunktionsVerletzung", "LEBENSZEICHEN_S"]
