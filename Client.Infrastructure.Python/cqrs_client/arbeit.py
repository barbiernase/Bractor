# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/arbeit.py
"""
Arbeit für Akteure (docs/konzept-akteure.md §5): ein Client rechnet, was ein Akteur, den er verkörpert, zu tun bekommt.

Arbeit ist das `IHoert` eines Commands OHNE Ziel. Der Akteur steht in der Domäne (C#, eine Quelle):

    public sealed record Klassifizierer : IAkteur,
        IHoert<PaarZuBeurteilen>,                  // Arbeit: ein Command ohne Ziel
        IDarf<KlassifiziereBildPaarDurchKi>;       // die Antwort: Commands, die er senden darf

    public sealed record KlassifikationsWorker : IClient, IVerkoerpert<Klassifizierer>;

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/vertraege.py`: die Client-Basis (`KlassifikationsWorkerBasis`) nennt in `ARBEIT` je Akteur
die Commands, die er hört, mit Methode und erlaubten Antworten (`Arbeit("auf_paar_zu_beurteilen", (KlassifiziereBildPaarDurchKiDto, …))`), und
erklärt je Command eine abstrakte Methode `auf_<command>(c, h, state)`. Der Worker erbt sie und füllt die Methoden:

    class ImageClassifier(KlassifikationsWorkerBasis[ClassifierState]):
        async def auf_paar_zu_beurteilen(self, c, h, state):
            return KlassifiziereBildPaarDurchKiDto(aggregate_id=c.paar, label=0)

Die Antwort ist ein Wert, eine Liste, None (keine Antwort) — oder Stücke (`yield`, ein langer Lauf meldet Fortschritt).

Was das SDK übernimmt — der Worker schreibt nichts davon von Hand:
  - Annehmen: nach dem Handshake (und nach jedem Reconnect) `ArbeitAnnehmen` (Akteur + Plätze); die Sitzung wird ein Arbeiter in der Werkstatt
    ihres Knotens, die ihr Aufträge gibt — höchstens so viele je Akteur, wie sie Plätze hat; hier laufen höchstens so viele gleichzeitig;
  - Zuteilung: ein `ArbeitsAuftrag` nennt den Akteur (`fuer`), dessen Arbeit er ist; der Command geht an `auf_<command>` dieses Akteurs;
  - Lebenszeichen: alle 10 s `ArbeitLebtMeldung`, solange `auf_…` läuft (ohne Regung gibt der Server den Auftrag nach 60 s weiter);
  - Prüfung: jede Antwort muss ein Command sein, den der Akteur senden darf (`IDarf`), sonst geht ein Fehler hinaus; eine Ausnahme in `auf_…`
    ebenso — der Server stellt den Auftrag erneut zu;
  - Stücke: ein `async for`-Generator (Training) schickt jedes Stück sofort (`ab` = Stelle seines ersten Commands); der Server sendet es, bevor
    das nächste kommt; nach einem Abbruch rechnet ein anderer Arbeiter den Auftrag von vorn (die Inbox der Ziele verwirft schon Gesendetes);
  - Genau einmal: die erste Antwort gewinnt; nichts wird festgehalten — die Ids der Commands leitet der Server aus dem Auftrag ab. Wirkungen nach
    außen (Dateien, Mails) sollten über `h.schluessel` (die Kennung des Auftrags, gleich bei jeder Wiederholung) deduplizieren. Einen Zustand im
    Worker (ein Modell) sichert das Framework nicht.
"""

from __future__ import annotations

import asyncio
import inspect
import logging
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, AsyncIterator, Callable, Iterable

log = logging.getLogger(__name__)

LEBENSZEICHEN_S = 10.0


@dataclass(frozen=True)
class Arbeit:
    """Ein gehörter Command (Arbeit eines Akteurs): die Methode `auf_<command>` und die erlaubten Antworten (DTO-Typen aus dem IDarf des Akteurs)."""
    methode: str
    antworten: tuple = ()


@dataclass(frozen=True)
class Herkunft:
    """Woher der Auftrag kommt (Gegenstück zu `Abstractions.Herkunft`): Stream, Version, Log-Zeit, Akteur der Kette; dazu Korrelation und der
    Schlüssel des Auftrags (gleich bei jeder Wiederholung — zum Deduplizieren von Wirkungen nach außen)."""
    stream: str
    version: int
    zeit: datetime
    akteur: str = ""
    korrelation: str = ""
    schluessel: str = ""


class ArbeitsVerletzung(TypeError):
    """Eine Arbeit antwortet, was der Akteur nicht senden darf — oder ein Auftrag bringt einen Command, den er nicht hört."""


class AkteursArbeit:
    """
    Die Arbeit eines Akteurs, gebunden an den Client, der ihn verkörpert: der Akteur (`name`), seine gehörten Commands (`arbeit`: Command-DTO →
    Arbeit), der Aufruf der Client-Methode über ihren Namen (`rufe(methode, c, h)` — der Client reicht seinen Zustand dazu) und die Plätze.
    """

    def __init__(self, name: str, arbeit: dict, rufe: Callable[[str, Any, Herkunft], Any], plaetze: int | None = None):
        if not name:
            raise TypeError("AkteursArbeit ohne Akteur")
        self.name = name
        self.arbeit: dict = dict(arbeit)
        self.plaetze = max(1, plaetze if plaetze is not None else 1)
        self._rufe = rufe

    def arbeit_fuer(self, typ: type) -> Arbeit:
        a = self.arbeit.get(typ)
        if a is None:
            gehoert = ", ".join(t.__name__ for t in self.arbeit) or "nichts"
            raise ArbeitsVerletzung(f"{self.name} hört {typ.__name__} nicht (Arbeit: {gehoert})")
        return a

    def rufe(self, c: Any, h: Herkunft) -> Any:
        """Ruft `auf_<command>` (Wert, Liste, None, Coroutine oder Stücke)."""
        return self._rufe(self.arbeit_fuer(type(c)).methode, c, h)

    def pruefe(self, arbeit: Arbeit, antwort: Any) -> None:
        """Die Antwort ist ein Command, den der Akteur senden darf (einer der erlaubten Antworten)."""
        if not arbeit.antworten or not isinstance(antwort, tuple(arbeit.antworten)):
            erlaubt = ", ".join(t.__name__ for t in arbeit.antworten) or "nichts"
            raise ArbeitsVerletzung(f"{self.name}.{arbeit.methode}: antwortet {type(antwort).__name__} — der Akteur darf: {erlaubt}")


def _einzeln(x: Any) -> list:
    """Ein Stück → seine Commands (eine Liste/ein Tupel ist ein Stück aus mehreren, None ist keins)."""
    if x is None:
        return []
    if isinstance(x, (list, tuple)):
        return [y for y in x if y is not None]
    return [x]


async def _stuecke(ergebnis) -> AsyncIterator[list]:
    """Stücke (Async-/Sync-Generator) → je yield ein Stück; Coroutine oder Wert → ein Stück (None = keins)."""
    if inspect.isasyncgen(ergebnis):
        async for x in ergebnis:
            if (stueck := _einzeln(x)):
                yield stueck
    elif inspect.isgenerator(ergebnis):
        for x in ergebnis:
            if (stueck := _einzeln(x)):
                yield stueck
    else:
        if inspect.isawaitable(ergebnis):
            ergebnis = await ergebnis
        if (stueck := _einzeln(ergebnis)):
            yield stueck


def _ist_strom(ergebnis) -> bool:
    return inspect.isasyncgen(ergebnis) or inspect.isgenerator(ergebnis)


class ArbeitsLaeufer:
    """
    Rechnet die Arbeit der verkörperten Akteure eines Clients — transport-frei: gesendet wird über `proxy` (`send_arbeit_annehmen`,
    `send_arbeits_ergebnis`, `send_arbeit_lebt`), verpackt über `mapper` (`entpacke`: Nutzlast → DTO, `packe`: DTO → Nutzlast).
    """

    def __init__(self, arbeit: Iterable[AkteursArbeit] | None, proxy, mapper, lebenszeichen_s: float = LEBENSZEICHEN_S):
        self._akteure: dict[str, AkteursArbeit] = {}
        for a in arbeit or ():
            self.nimm_auf(a)
        self._proxy = proxy
        self._mapper = mapper
        self._lebenszeichen_s = lebenszeichen_s
        self._laufend: dict[str, asyncio.Task] = {}
        self._plaetze: dict[str, asyncio.Semaphore] = {}

    def nimm_auf(self, a: AkteursArbeit) -> None:
        if not isinstance(a, AkteursArbeit):
            raise TypeError(f"{type(a).__name__} ist keine AkteursArbeit")
        if a.name in self._akteure:
            raise ValueError(f"Arbeit des Akteurs {a.name} doppelt angenommen")
        self._akteure[a.name] = a

    @property
    def angebote(self) -> list[tuple[str, int]]:
        """Je Akteur mit Arbeit: (Name, Plätze)."""
        return [(name, a.plaetze) for name, a in self._akteure.items()]

    @property
    def laufend(self) -> int:
        return len(self._laufend)

    async def nimm_an(self, _antwort=None) -> None:
        """Nach dem Handshake (und nach jedem Reconnect): die Arbeit der Akteure annehmen (Akteur + Plätze)."""
        if not self._akteure:
            return
        await self._proxy.send_arbeit_annehmen(self.angebote)
        log.info("Arbeit angenommen: %s", ", ".join(f"{n} ({p} Plätze)" for n, p in self.angebote))

    def nimm(self, auftrag) -> asyncio.Task | None:
        """Ein ArbeitsAuftrag vom Server: läuft entkoppelt (blockiert die Verarbeitungsschleife nicht)."""
        vorgang = auftrag.vorgang
        if auftrag.fuer not in self._akteure:
            # Nie angenommen → nicht unsere Arbeit; ohne Regung gibt der Server den Auftrag an einen anderen Arbeiter.
            log.error("Auftrag %s für %s — dessen Arbeit ist hier nicht angenommen", vorgang, auftrag.fuer)
            return None
        if vorgang in self._laufend:
            return self._laufend[vorgang]   # derselbe Auftrag erneut gegeben: läuft schon
        task = asyncio.ensure_future(self._fuehre_aus(auftrag))
        self._laufend[vorgang] = task
        task.add_done_callback(lambda _t: self._laufend.pop(vorgang, None))
        return task

    async def warte_auf_alle(self) -> None:
        while self._laufend:
            await asyncio.gather(*list(self._laufend.values()), return_exceptions=True)

    def _platz(self, akteur: str) -> asyncio.Semaphore:
        if akteur not in self._plaetze:
            self._plaetze[akteur] = asyncio.Semaphore(self._akteure[akteur].plaetze)
        return self._plaetze[akteur]

    async def _fuehre_aus(self, msg) -> None:
        a = self._akteure[msg.fuer]
        h = Herkunft(stream=msg.stream, version=msg.version,
                     zeit=datetime.fromtimestamp(msg.zeit_utc / 1000, tz=timezone.utc),
                     akteur=msg.akteur, korrelation=msg.korrelation, schluessel=msg.vorgang)
        async with self._platz(msg.fuer):
            herz = asyncio.ensure_future(self._lebenszeichen(msg.vorgang))
            try:
                try:
                    c = self._mapper.entpacke(msg.eingang)
                    arbeit = a.arbeit_fuer(type(c))
                    ergebnis = a.rufe(c, h)
                    stueckweise = _ist_strom(ergebnis)
                    gesendet = 0
                    rest: list = []
                    async for stueck in _stuecke(ergebnis):
                        for antwort in stueck:
                            a.pruefe(arbeit, antwort)
                        commands = [self._mapper.packe(x) for x in stueck]
                        if stueckweise:
                            # Stücke: jedes sofort — der Server sendet es, bevor das nächste kommt.
                            await self._proxy.send_arbeits_ergebnis(msg.vorgang, commands, ab=gesendet, fertig=False)
                            gesendet += len(commands)
                        else:
                            rest.extend(commands)
                    await self._proxy.send_arbeits_ergebnis(msg.vorgang, rest, ab=gesendet, fertig=True)
                    log.info("→ Antwort %s (%s): %d Command(s)", msg.vorgang[:8], a.name, gesendet + len(rest))
                except asyncio.CancelledError:
                    raise
                except Exception as fehler:   # gescheitert → der Server stellt den Auftrag erneut zu
                    log.warning("Arbeit %s (%s) gescheitert: %s", a.name, msg.vorgang, fehler)
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
            except Exception as e:   # verlierbar: die nächste Meldung heilt
                log.debug("Lebenszeichen %s verloren: %s", vorgang, e)


__all__ = ["Arbeit", "AkteursArbeit", "ArbeitsLaeufer", "ArbeitsVerletzung", "Herkunft", "LEBENSZEICHEN_S"]
