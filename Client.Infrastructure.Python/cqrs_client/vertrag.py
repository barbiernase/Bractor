# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/vertrag.py
"""
Client-Verträge (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5) — die Basis, von der die GENERIERTEN Client-Basen erben.

Akteur und Client stehen in der Domäne (C#, eine Quelle):

    public sealed record Klassifizierer : IAkteur,
        IHoert<PaarZuBeurteilen>,                   // Arbeit: ein Command ohne Ziel
        IDarf<KlassifiziereBildPaarDurchKi>,        // die Antwort (und was er von sich aus sendet)
        IHoert<ModellAktiviert>;                    // Kenntnis, verlierbar

    public sealed record KlassifikationsWorker : IClient,
        IVerkoerpert<Klassifizierer>;               // was über die Leitung geht: IDarf und IHoert seiner Akteure

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/vertraege.py` mit einer Basis je Client (`KlassifikationsWorkerBasis`). Der Worker erbt
sie und füllt je gehörtem Command eine Methode `auf_<command>(c, h, state)` (die Antwort: Commands aus dem IDarf des Akteurs) und je Kenntnis
eine Methode `auf_<event>(e, ctx, state)` (gibt nichts aus):

    class ImageClassifier(KlassifikationsWorkerBasis[ClassifierState]):
        async def auf_paar_zu_beurteilen(self, c, h, state):
            return KlassifiziereBildPaarDurchKiDto(aggregate_id=c.paar, label=0)
        async def auf_modell_aktiviert(self, e, ctx, state): ...

Was die Basis übernimmt — der Worker schreibt nichts davon von Hand:
  - Arbeit: sie wird nach jedem Handshake angenommen (je Akteur mit `PLAETZE`, Standard 1); der Server gibt Aufträge, nimmt die erste Antwort an
    und sendet ihre Commands selbst — der Client ist nur die Hand, die Garantie bleibt beim Server (`arbeit.py`);
  - Kenntnis: Dispatch Event-Typ → `auf_<event>`; eine Kenntnis gibt nichts aus (`VertragsVerletzung`);
  - Vollständigkeit: fehlt eine Methode, lässt sich der Worker nicht instanziieren (ABC) — der Bruch fällt beim Start auf;
  - Handshake: der CapabilitiesRequest nennt Client + Hash (der Server nimmt die Fähigkeiten aus SEINER Tabelle); `FRAGT` sind die Queries der
    Akteure — `query()` mit einem anderen Typ ist eine `VertragsVerletzung`.
"""

from __future__ import annotations

import logging
from abc import ABCMeta
from typing import Any, ClassVar, TypeVar

from .arbeit import AkteursArbeit, _stuecke
from .client import CqrsClient
from .dispatch import HandleDescriptor, HandlerMeta

log = logging.getLogger(__name__)

S = TypeVar("S")


class VertragsVerletzung(TypeError):
    """Ein Client tut, was sein Vertrag nicht erlaubt (eine Kenntnis gibt etwas aus, eine Query steht nicht im Vertrag)."""


class VertragsMeta(HandlerMeta, ABCMeta):
    """
    HandlerMeta (Dispatch-Deskriptor je Klasse) + ABCMeta (abstrakte Methoden). Eine Klasse, die `HOERT` selbst deklariert (die generierte
    Basis), verdrahtet je gehörtem Typ einen Wrapper — er ruft die Methode über ihren NAMEN auf, also die Implementierung der Unterklasse.
    """

    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)
        if "HOERT" in namespace:
            for eingang, methode in namespace["HOERT"].items():
                cls.handle._register_method(eingang, _kenntnis(eingang, methode))
        return cls


def _kenntnis(eingang: type, methode: str):
    async def reagiere(self, payload, ctx, state):
        async for stueck in _stuecke(getattr(self, methode)(payload, ctx, state)):
            raise VertragsVerletzung(
                f"{type(self).__name__}.{methode}: gibt {type(stueck[0]).__name__} aus — {eingang.__name__} ist nur Kenntnis (IHoert); "
                f"eine Antwort gibt es nur auf Arbeit (IHoert eines Commands ohne Ziel bei einem verkörperten Akteur)")
        if False:   # macht reagiere zum Generator (der Dispatch erwartet einen Strom)
            yield None

    reagiere.__name__ = methode
    return reagiere


class ClientVertragBasis(CqrsClient[S], metaclass=VertragsMeta):
    """
    Basis der generierten Client-Basen. Die generierte Unterklasse setzt:
        CLIENT, AKTEURE, VERTRAG_HASH — Handshake-Name, verkörperte Akteure, Stand;
        ARBEIT  — Akteur → {Command-DTO: Arbeit(Methode, Antworten)}: die gehörten Commands ohne Ziel;
        HOERT   — Event-DTO → Methode `auf_<event>`: die Kenntnis;
        SPONTAN — Commands, die von sich aus hineingehen (IDarf der verkörperten Akteure);
        FRAGT   — die Queries (IDarf der verkörperten Akteure; leer = nicht geprüft).
    Wie viele Aufträge eines Akteurs gleichzeitig laufen, sagt `PLAETZE` (Akteur → Zahl, Standard 1).
    """

    CLIENT: ClassVar[str] = ""
    AKTEURE: ClassVar[tuple[str, ...]] = ()
    VERTRAG_HASH: ClassVar[str] = ""
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {}
    SPONTAN: ClassVar[tuple[type, ...]] = ()
    FRAGT: ClassVar[tuple[type, ...]] = ()
    PLAETZE: ClassVar[dict[str, int]] = {}

    def __init_subclass__(cls, **kwargs):
        super().__init_subclass__(**kwargs)
        cls._declared_command_types = list(cls.SPONTAN)

    def __init__(self, registry, nutzlast, config: dict[str, Any] | None = None):
        # Die Vertrags-Typen kennt der Vertrag selbst — die (handgepflegte) Registry wird um sie ergänzt: Kenntnis, Commands (von sich aus, gehörte
        # Arbeit und ihre Antworten) und Queries.
        arbeits_commands = {t for gehoert in self.ARBEIT.values() for c, a in gehoert.items() for t in (c, *a.antworten)}
        registry = registry.ergaenzt(events=set(self.HOERT), commands=set(self.SPONTAN) | arbeits_commands, queries=set(self.FRAGT))
        arbeit = [AkteursArbeit(akteur, gehoert, self._rufe_arbeit, plaetze=self.PLAETZE.get(akteur))
                  for akteur, gehoert in self.ARBEIT.items()]
        super().__init__(registry, nutzlast, config, arbeit=arbeit)

    def _rufe_arbeit(self, methode: str, c, h):
        return getattr(self, methode)(c, h, self.state)

    def _build_capabilities_request(self):
        request = super()._build_capabilities_request()
        request.vertrag = self.CLIENT
        request.vertrag_hash = self.VERTRAG_HASH
        log.info("Client %s (verkörpert %s, Arbeit für %s, Hash %s)", self.CLIENT, ", ".join(self.AKTEURE),
                 ", ".join(self.ARBEIT) or "niemanden", self.VERTRAG_HASH)
        return request

    async def query(self, query_dto):
        """Eine Query stellen — mit Client-Vertrag nur die, die seine Akteure dürfen (`IDarf<T>`)."""
        if self.FRAGT and not isinstance(query_dto, self.FRAGT):
            erlaubt = ", ".join(t.__name__ for t in self.FRAGT)
            raise VertragsVerletzung(
                f"{type(self).__name__}: fragt {type(query_dto).__name__} — der Client {self.CLIENT} erlaubt: {erlaubt}")
        return await super().query(query_dto)


__all__ = ["ClientVertragBasis", "VertragsVerletzung", "VertragsMeta", "HandleDescriptor"]
