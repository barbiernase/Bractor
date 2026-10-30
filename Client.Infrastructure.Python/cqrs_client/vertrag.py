# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/vertrag.py
"""
Client-Verträge (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5) — die Basis, von der die GENERIERTEN Client-Basen erben.

Der Vertrag steht in der Domäne (C#, eine Quelle):

    public sealed record Klassifizierer : IKi,
        IDarf<IBildpaarBeurteilung>,                // Funktion, deren Code der Client ausführt
        IDarf<KlassifiziereEinzelBildDurchKi>,      // von sich aus
        IDarf<HoleAktivesModell>;                   // eine Query

    public interface IKlassifikationsWorker : IClientVertrag,
        IVerkoerpert<Klassifizierer>,                      // was über die Leitung geht: seine IDarf
        IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>;   // Kenntnis, verlierbar

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/vertraege.py` mit einer Basis je Client (`KlassifikationsWorkerBasis`). Der Worker erbt
sie und füllt je getragener Funktion eine Methode (`bildpaar_beurteilung(e, h, state)`, gibt die Ausgaben ihrer Signatur zurück) und je
Kenntnis eine Methode `auf_<event>(e, ctx, state)` (gibt nichts aus):

    class ImageClassifier(KlassifikationsWorkerBasis[ClassifierState]):
        async def bildpaar_beurteilung(self, e, h, state):
            return KlassifiziereBildPaarDurchKiDto(aggregate_id=h.stream, label=0)
        async def auf_bild_verfuegbar(self, e, ctx, state): ...

Was die Basis übernimmt — der Worker schreibt nichts davon von Hand:
  - getragene Funktionen: sie werden nach jedem Handshake angeboten; der Server reicht ihre Eingänge weiter, die Antwort hält er fest (die
    erste gewinnt) und sendet sie selbst — der Client ist nur die Hand, die Garantie bleibt beim Funktions-Actor;
  - Kenntnis: Dispatch Event-Typ → `auf_<event>`; eine Kenntnis gibt nichts aus (`VertragsVerletzung`);
  - Vollständigkeit: fehlt eine Methode, lässt sich der Worker nicht instanziieren (ABC) — der Bruch fällt beim Start auf;
  - Handshake: der CapabilitiesRequest nennt Client + Hash (der Server nimmt die Fähigkeiten aus SEINER Tabelle); `FRAGT` sind die Queries des
    Vertrags — `query()` mit einem anderen Typ ist eine `VertragsVerletzung`.
"""

from __future__ import annotations

import logging
from abc import ABCMeta
from typing import Any, ClassVar, TypeVar

from .client import CqrsClient
from .dispatch import HandleDescriptor, HandlerMeta
from .funktion import GetrageneFunktion, _ausgaben

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
        async for aus in _ausgaben(getattr(self, methode)(payload, ctx, state)):
            raise VertragsVerletzung(
                f"{type(self).__name__}.{methode}: gibt {type(aus).__name__} aus — {eingang.__name__} ist nur Kenntnis (IHoert); "
                f"eine Ausgabe auf ein Event ist eine Funktion, die der Client trägt (IDarf<F> eines verkörperten Akteurs)")
        if False:   # macht reagiere zum Generator (der Dispatch erwartet einen Strom)
            yield None

    reagiere.__name__ = methode
    return reagiere


class ClientVertragBasis(CqrsClient[S], metaclass=VertragsMeta):
    """
    Basis der generierten Client-Basen. Die generierte Unterklasse setzt:
        CLIENT, AKTEURE, VERTRAG, VERTRAG_HASH — Handshake-Name, verkörperte Akteure, Vertrag, Stand;
        TRAEGT  — Funktions-Name → {Eingangs-DTO: Eingang(Methode, Ausgaben, Strom)}: die getragenen Funktionen;
        HOERT   — Event-DTO → Methode `auf_<event>`: die Kenntnis;
        SPONTAN — Commands, die von sich aus hineingehen (IDarf der verkörperten Akteure);
        FRAGT   — die Queries (IDarf der verkörperten Akteure; leer = nicht geprüft).
    Wie viele Eingänge einer getragenen Funktion gleichzeitig laufen, sagt `SLOTS` (Funktion → Zahl, Standard 1).
    """

    CLIENT: ClassVar[str] = ""
    AKTEURE: ClassVar[tuple[str, ...]] = ()
    VERTRAG: ClassVar[str] = ""
    VERTRAG_HASH: ClassVar[str] = ""
    TRAEGT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {}
    SPONTAN: ClassVar[tuple[type, ...]] = ()
    FRAGT: ClassVar[tuple[type, ...]] = ()
    SLOTS: ClassVar[dict[str, int]] = {}

    def __init_subclass__(cls, **kwargs):
        super().__init_subclass__(**kwargs)
        cls._declared_command_types = list(cls.SPONTAN)

    def __init__(self, registry, nutzlast, config: dict[str, Any] | None = None, funktionen=None):
        # Die Vertrags-Typen kennt der Vertrag selbst — die (handgepflegte) Registry wird um sie ergänzt.
        registry = registry.ergaenzt(events=set(self.HOERT), commands=set(self.SPONTAN), queries=set(self.FRAGT))
        getragen = [GetrageneFunktion(name, eingaenge, self._rufe_getragen, slots=self.SLOTS.get(name))
                    for name, eingaenge in self.TRAEGT.items()]
        super().__init__(registry, nutzlast, config, funktionen=list(funktionen or []) + getragen)

    def _rufe_getragen(self, methode: str, e, h):
        return getattr(self, methode)(e, h, self.state)

    def _build_capabilities_request(self):
        request = super()._build_capabilities_request()
        request.vertrag = self.CLIENT
        request.vertrag_hash = self.VERTRAG_HASH
        log.info("Client %s (Vertrag %s, verkörpert %s, trägt %s, Hash %s)", self.CLIENT, self.VERTRAG, ", ".join(self.AKTEURE),
                 ", ".join(self.TRAEGT) or "nichts", self.VERTRAG_HASH)
        return request

    async def query(self, query_dto):
        """Eine Query stellen — mit Client-Vertrag nur die, die seine Akteure dürfen (`IDarf<T>`)."""
        if self.FRAGT and not isinstance(query_dto, self.FRAGT):
            erlaubt = ", ".join(t.__name__ for t in self.FRAGT)
            raise VertragsVerletzung(
                f"{type(self).__name__}: fragt {type(query_dto).__name__} — der Vertrag {self.VERTRAG} erlaubt: {erlaubt}")
        return await super().query(query_dto)


__all__ = ["ClientVertragBasis", "VertragsVerletzung", "VertragsMeta", "HandleDescriptor"]
