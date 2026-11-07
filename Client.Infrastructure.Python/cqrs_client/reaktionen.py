# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/reaktionen.py
"""
Reaktionen eines Clients (docs/plan-katalog-und-graphen.md, Schritt 6) — das Gegenstück zu Client.Infrastructure/Reaktionen in C#.

Nur Clients und Quellen stoßen Funktionen an: was nach einem Fakt weitergehen soll, stößt ein Client an. Seine Handler (@handle.register auf ein
Event, das sein Akteur hört) reagieren und geben Commands aus (yield). Genau einmal ohne Festhalten beim Framework:

  - der Läufer liest das Log ab dem gesicherten Stand (LiesLog: nur Hörbares, bis zum Horizont) — ein Push weckt ihn nur;
  - jede Command-Id leitet er aus Reaktion, Position und Stelle ab (dieselbe Formel wie in C#: MD5, Guid-Bytes little-endian) — nach einem
    Neustart rechnet er dieselben Ids, die Inbox des Ziels bzw. der offene Eingang am Kopf verwirft die Wiederholung;
  - weiter rückt der Stand erst, wenn jeder Command der Seite beantwortet ist (Quittung, Event, Ablehnung oder Fehler mit seiner Korrelation);
  - jedes Stück geht hinaus, sobald die Reaktion es ausgibt (ein Training meldet so Beginn und Fortschritt, während es rechnet), und trägt das
    Event, auf das reagiert wird, als Ursache (Spur: Stream:Version).
"""

from __future__ import annotations

import asyncio
import hashlib
import logging
import os
import uuid
from pathlib import Path
from typing import Any

from .registry import MessageCategory
from .router import MessageContext

log = logging.getLogger(__name__)

STANDARD_ANTWORTFRIST = 5.0
VERSUCHE = 5
OHNE_ZIEL = uuid.UUID(int=0)


def ableite(text: str) -> str:
    """Eine deterministische Id aus einem Text — wie Herkunft.Ableite in C# (MD5, Guid-Bytes little-endian)."""
    return str(uuid.UUID(bytes_le=hashlib.md5(text.encode("utf-8")).digest()))


def command_id(name: str, position: int, nummer: int, stelle: int) -> str:
    """Die Command-Id der Stelle `stelle` der Reaktion auf das `nummer`-te Event an `position` — wie Reaktionen.CommandId in C#."""
    return ableite(f"reaktion:{name}:{position}:{nummer}:{stelle}")


class ReaktionsStandImSpeicher:
    """Der Stand im Speicher — übersteht ein Wiederverbinden, keinen Neustart (Tests)."""

    def __init__(self) -> None:
        self._je: dict[str, int] = {}

    def lade(self, name: str) -> int:
        return self._je.get(name, 0)

    def sichere(self, name: str, position: int) -> None:
        self._je[name] = position


class DateiReaktionsStand:
    """Der Stand in einer Datei je Name (<verzeichnis>/<name>.stand), geschrieben über eine temporäre Datei und Umbenennen."""

    def __init__(self, verzeichnis: str | Path) -> None:
        self._verzeichnis = Path(verzeichnis)

    def _pfad(self, name: str) -> Path:
        return self._verzeichnis / f"{name}.stand"

    def lade(self, name: str) -> int:
        try:
            return int(self._pfad(name).read_text(encoding="utf-8").strip())
        except (FileNotFoundError, ValueError):
            return 0

    def sichere(self, name: str, position: int) -> None:
        self._verzeichnis.mkdir(parents=True, exist_ok=True)
        tmp = self._pfad(name).with_suffix(".tmp")
        tmp.write_text(str(position), encoding="utf-8")
        os.replace(tmp, self._pfad(name))


class ReaktionUnterbrochen(Exception):
    """Die Verbindung fehlt: wer die Reaktionen trägt, verbindet neu und lässt sie weiterlaufen — ab dem gesicherten Stand."""


class ReaktionsLaeufer:
    """
    Der Läufer der Reaktionen eines Clients: je Event-Typ der registrierte Handler. Der Client gibt ihm jede Server-Nachricht vom Typ „event“
    (angekommen) — eine korrelierte Antwort beendet das Warten ihres Commands, jedes Event weckt ihn.
    """

    def __init__(self, proxy, mapper, registry, handle, instance, state, version_tracker, stand, name: str,
                 takt: float = 10.0, antwortfrist: float = STANDARD_ANTWORTFRIST) -> None:
        self._proxy, self._mapper, self._registry = proxy, mapper, registry
        self._handle, self._instance, self._state, self._versionen = handle, instance, state, version_tracker
        self._stand, self._name = stand, name
        self._takt, self._antwortfrist = takt, antwortfrist
        self._offen: dict[str, asyncio.Future] = {}
        self._weckruf = asyncio.Event()

    def angekommen(self, notification) -> None:
        """Eine EventNotification der Verbindung: Antwort auf einen eigenen Command (Korrelation) und Weckruf."""
        korrelation = notification.envelope.correlation_id
        if korrelation and korrelation in self._offen and not self._offen[korrelation].done():
            self._offen[korrelation].set_result(notification.envelope)
        self._weckruf.set()

    def fehler(self, payload) -> None:
        """Ein Fehler des Servers mit der Korrelation eines eigenen Commands: der Ausgang ist bekannt (gescheitert)."""
        if payload.correlation_id in self._offen and not self._offen[payload.correlation_id].done():
            log.warning("Reaktion %s: Command scheiterte — [%s] %s", self._name, payload.code, payload.message)
            self._offen[payload.correlation_id].set_result(payload)

    async def lauf(self) -> None:
        """Liest ab dem Stand, reagiert, sendet, sichert je Seite; ohne Neues wartet er auf einen Push oder den Takt."""
        ab = self._stand.lade(self._name)
        log.info("Reaktionen %s laufen ab Position %d", self._name, ab)
        while True:
            if not self._proxy.is_connected:
                raise ReaktionUnterbrochen("keine Verbindung")
            self._weckruf.clear()
            seite = await self._proxy.lies_log(ab)
            horizont = getattr(seite, "horizont", None)
            if horizont is not None and horizont < ab:
                # Das Log ist kürzer als der eigene Stand: eine neue Datenbank. Von vorn — dieselben Positionen ergäben dieselben Ids.
                log.warning("Reaktionen %s: das Log endet bei %d, der Stand steht bei %d — neu, von vorn", self._name, horizont, ab)
                ab = 0
                self._stand.sichere(self._name, ab)
                continue
            nummer, vorige = 0, -1
            for eintrag in seite.eintraege:
                # Mehrere Events an einer Position (ein Split-Upcast) zählen der Reihe nach — eine volle Seite trennt eine Position nie.
                nummer = nummer + 1 if eintrag.position == vorige else 0
                vorige = eintrag.position
                payload = self._mapper.extract_event_payload(eintrag.envelope)
                if self._handle.get_handler(type(payload)) is None:
                    continue
                ctx = _kontext(eintrag.envelope, self._proxy, self._mapper, self._versionen)
                ursache = f"{eintrag.envelope.aggregate_id}:{eintrag.envelope.aggregate_version}"
                stelle = 0
                async for ausgabe in self._handle.receive(self._instance, payload, ctx, self._state):
                    await self._sende(ausgabe, command_id(self._name, eintrag.position, nummer, stelle), ursache)
                    stelle += 1
            if seite.bis > ab:
                ab = seite.bis
                self._stand.sichere(self._name, ab)
            if seite.voll:
                continue
            try:
                await asyncio.wait_for(self._weckruf.wait(), timeout=self._takt)
            except asyncio.TimeoutError:
                pass

    async def _sende(self, ausgabe: Any, cid: str, ursache: str) -> None:
        """Sendet mit der abgeleiteten Id und wartet auf die Antwort; bleibt sie aus, mit derselben Id noch einmal."""
        if self._registry.classify(type(ausgabe)) != MessageCategory.COMMAND:
            raise TypeError(f"Eine Reaktion gibt Commands aus, nicht {type(ausgabe).__name__}")
        ziel = getattr(ausgabe, "aggregate_id", None)
        ziel = uuid.UUID(ziel) if isinstance(ziel, str) and ziel else (ziel or OHNE_ZIEL)   # ohne Ziel: an den Kopf eines Graphen
        envelope = self._mapper.wrap_command(ausgabe, aggregate_id=ziel, expected_version=-1, correlation_id=cid,
                                             session_id=self._proxy.session_id)
        envelope.command_id = cid
        envelope.ursache = ursache
        antwort: asyncio.Future = asyncio.get_event_loop().create_future()
        self._offen[cid] = antwort
        try:
            for _ in range(VERSUCHE):
                if not self._proxy.is_connected:
                    raise ReaktionUnterbrochen(f"{type(ausgabe).__name__} nicht gesendet — keine Verbindung")
                await self._proxy.send_command(envelope)
                try:
                    await asyncio.wait_for(asyncio.shield(antwort), timeout=self._antwortfrist)
                    return
                except asyncio.TimeoutError:
                    continue
            raise ReaktionUnterbrochen(f"{type(ausgabe).__name__} bleibt nach {VERSUCHE} Versuchen ohne Antwort")
        finally:
            self._offen.pop(cid, None)


def _kontext(envelope, proxy, mapper, versionen) -> MessageContext:
    try:
        agg = uuid.UUID(envelope.aggregate_id) if envelope.aggregate_id else None
    except ValueError:
        agg = None
    return MessageContext(aggregate_id=agg, aggregate_type=envelope.aggregate_type, aggregate_version=envelope.aggregate_version,
                          correlation_id=envelope.correlation_id, session_id=proxy.session_id,
                          _proxy=proxy, _mapper=mapper, _version_tracker=versionen)
