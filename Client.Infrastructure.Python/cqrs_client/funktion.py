# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/funktion.py
"""
Katalog-Funktionen in Python ANBIETEN (docs/konzept-editor-pipelines.md §14.5).

Eine Funktion ist ein Vertrag (C#, eine Quelle), der Laufort eine Bindung:

    public interface IBildVerkleinerung : IFunktion
    {
        Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild auftrag, IAusfuehrung x);
    }

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/funktionen.py` mit einer Basis je Funktion
(`BildVerkleinerungBasis`). Der Worker erbt sie und implementiert genau `rufe`:

    class Verkleinerer(BildVerkleinerungBasis):
        SLOTS = 4
        async def rufe(self, auftrag: VerkleinereBildDto, x: Ausfuehrung):
            ...
            return BildVerkleinertDto(pfad=ziel, breite_pixel=w, hoehe_pixel=h)

    client = MeinClient(registry, generated, funktionen=[Verkleinerer()])

Was das SDK übernimmt — der Worker schreibt nichts davon von Hand:
  - Anmelden: nach dem Handshake (und nach jedem Reconnect) `FunktionenAnbieten` (Funktion + Slots);
  - Pull: der Server holt für ihn Aufträge beim Vermittler, höchstens so viele wie Slots — hier laufen höchstens
    SLOTS gleichzeitig;
  - Lebenszeichen: alle 10 s `ArbeitLebtMeldung`, solange `rufe` läuft (lange Läufe verlieren ihre Lease nicht);
  - Ergebnis-Vertrag: das Ergebnis muss einer der ERGEBNISSE sein, sonst geht ein Fehler hinaus (der Prozess sieht
    einen gescheiterten Schritt); eine Ausnahme in `rufe` ebenso;
  - Genau einmal: der Server schreibt das Ergebnis in den Ausführungs-Stream (der Erste gewinnt). `x.ausfuehrungs_id`
    ist deterministisch — eine Wiederholung nach Absturz trägt dieselbe Id (Außenwirkung darüber deduplizieren).
"""

from __future__ import annotations

import asyncio
import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, ClassVar

log = logging.getLogger(__name__)

LEBENSZEICHEN_S = 10.0


@dataclass(frozen=True)
class Ausfuehrung:
    """Was jede Ausführung mitbekommt (Gegenstück zu IAusfuehrung): deterministische Id, Korrelation des Vorgangs, Akteur der Kette."""
    ausfuehrungs_id: str
    korrelation: str
    akteur: str = ""


class FunktionsVerletzung(TypeError):
    """Eine Funktion liefert etwas, das ihr Vertrag nicht erlaubt (kein Fall ihres OneOf)."""


class FunktionsBasis(ABC):
    """
    Basis der generierten Funktions-Klassen. Die generierte Unterklasse setzt:
        FUNKTION   — Name der Funktions-Schnittstelle (z. B. "IBildVerkleinerung"), so meldet sich der Worker an;
        AUFTRAG    — der betterproto-Typ des Auftrags;
        ERGEBNISSE — die erlaubten Ergebnis-Events (betterproto-Typen, die OneOf-Fälle).
    Der Worker setzt SLOTS (wie viele Aufträge gleichzeitig) — als Klassenattribut oder `slots=` im Konstruktor.
    """

    FUNKTION: ClassVar[str] = ""
    AUFTRAG: ClassVar[type | None] = None
    ERGEBNISSE: ClassVar[tuple[type, ...]] = ()
    SLOTS: ClassVar[int] = 1

    def __init__(self, slots: int | None = None):
        self.slots = max(1, slots if slots is not None else self.SLOTS)

    @abstractmethod
    async def rufe(self, auftrag: Any, x: Ausfuehrung) -> Any:
        """Rechnet den Auftrag und liefert GENAU EIN Ergebnis (einer der ERGEBNISSE)."""

    def pruefe(self, ergebnis: Any) -> None:
        """Ergebnis-Vertrag: genau einer der OneOf-Fälle der Funktion."""
        if ergebnis is None or not isinstance(ergebnis, self.ERGEBNISSE):
            erlaubt = ", ".join(t.__name__ for t in self.ERGEBNISSE)
            raise FunktionsVerletzung(
                f"{type(self).__name__}.rufe: liefert {type(ergebnis).__name__} — "
                f"die Funktion {self.FUNKTION} erlaubt: {erlaubt}")


class FunktionsLaeufer:
    """
    Führt die angebotenen Funktionen eines Clients aus — transport-frei: gesendet wird über `proxy`
    (`send_funktionen_anbieten`, `send_arbeits_ergebnis`, `send_arbeit_lebt`), verpackt über `mapper`
    (`extract_auftrag_payload`, `wrap_ergebnis`).
    """

    def __init__(self, funktionen, proxy, mapper, lebenszeichen_s: float = LEBENSZEICHEN_S):
        self._funktionen: dict[str, FunktionsBasis] = {}
        for f in funktionen or ():
            if not isinstance(f, FunktionsBasis) or not f.FUNKTION:
                raise TypeError(f"{type(f).__name__} ist keine generierte Funktions-Basis (FUNKTION fehlt)")
            if f.FUNKTION in self._funktionen:
                raise ValueError(f"Funktion {f.FUNKTION} doppelt angeboten")
            self._funktionen[f.FUNKTION] = f
        self._proxy = proxy
        self._mapper = mapper
        self._lebenszeichen_s = lebenszeichen_s
        self._slots = {name: asyncio.Semaphore(f.slots) for name, f in self._funktionen.items()}
        self._laufend: dict[str, asyncio.Task] = {}

    @property
    def angebote(self) -> list[tuple[str, int]]:
        return [(name, f.slots) for name, f in self._funktionen.items()]

    @property
    def laufend(self) -> int:
        return len(self._laufend)

    async def biete_an(self, _antwort=None) -> None:
        """Nach dem Handshake (und nach jedem Reconnect): Funktionen + Slots anmelden."""
        if not self._funktionen:
            return
        await self._proxy.send_funktionen_anbieten(self.angebote)
        log.info("Funktionen angeboten: %s", ", ".join(f"{n} ({s} Slots)" for n, s in self.angebote))

    def nimm(self, auftrag_msg) -> asyncio.Task | None:
        """Ein ArbeitsAuftrag vom Server: läuft entkoppelt (blockiert die Verarbeitungsschleife nicht)."""
        vorgang = auftrag_msg.vorgang
        if auftrag_msg.funktion not in self._funktionen:
            # Nie angeboten → nicht unsere Arbeit; die Lease läuft ab, der Vermittler gibt sie anderen.
            log.error("Auftrag %s für %s — hier nicht angeboten", vorgang, auftrag_msg.funktion)
            return None
        if vorgang in self._laufend:
            return self._laufend[vorgang]   # derselbe Vorgang erneut zugestellt: läuft schon
        task = asyncio.ensure_future(self._fuehre_aus(auftrag_msg))
        self._laufend[vorgang] = task
        task.add_done_callback(lambda _t: self._laufend.pop(vorgang, None))
        return task

    async def warte_auf_alle(self) -> None:
        while self._laufend:
            await asyncio.gather(*list(self._laufend.values()), return_exceptions=True)

    async def _fuehre_aus(self, msg) -> None:
        f = self._funktionen[msg.funktion]
        x = Ausfuehrung(ausfuehrungs_id=msg.vorgang, korrelation=msg.korrelation, akteur=msg.akteur)
        async with self._slots[msg.funktion]:
            herz = asyncio.ensure_future(self._lebenszeichen(msg.vorgang))
            try:
                ergebnis, fehler = None, ""
                try:
                    auftrag = self._mapper.extract_auftrag_payload(msg.auftrag)
                    if f.AUFTRAG is not None and not isinstance(auftrag, f.AUFTRAG):
                        raise FunktionsVerletzung(
                            f"{f.FUNKTION} erwartet {f.AUFTRAG.__name__}, bekam {type(auftrag).__name__}")
                    ergebnis = await f.rufe(auftrag, x)
                    f.pruefe(ergebnis)
                except asyncio.CancelledError:
                    raise
                except Exception as e:   # Fehlschlag der Funktion → Fehler-Marke im Log (Zweig/Kompensation im Prozess)
                    log.warning("Funktion %s (%s) gescheitert: %s", f.FUNKTION, msg.vorgang, e)
                    ergebnis, fehler = None, f"{type(e).__name__}: {e}"
            finally:
                herz.cancel()
                try:
                    await herz
                except asyncio.CancelledError:
                    pass

        envelope = None
        if not fehler:
            try:
                envelope = self._mapper.wrap_ergebnis(ergebnis, vorgang=msg.vorgang, korrelation=msg.korrelation)
            except Exception as e:
                fehler = f"Ergebnis nicht verpackbar: {e}"
        try:
            await self._proxy.send_arbeits_ergebnis(msg.vorgang, envelope, fehler)
            log.info("→ Ergebnis %s: %s", msg.vorgang[:8], fehler or type(ergebnis).__name__)
        except Exception as e:
            # Verbindung weg: die Lease läuft ab, der Vermittler gibt den Auftrag neu aus (genau einmal bleibt gewahrt).
            log.warning("Ergebnis %s nicht zustellbar: %s", msg.vorgang, e)

    async def _lebenszeichen(self, vorgang: str) -> None:
        while True:
            await asyncio.sleep(self._lebenszeichen_s)
            try:
                await self._proxy.send_arbeit_lebt(vorgang)
            except Exception as e:   # verlierbar: die nächste Meldung (oder die Lease) heilt
                log.debug("Lebenszeichen %s verloren: %s", vorgang, e)


__all__ = ["Ausfuehrung", "FunktionsBasis", "FunktionsLaeufer", "FunktionsVerletzung", "LEBENSZEICHEN_S"]
