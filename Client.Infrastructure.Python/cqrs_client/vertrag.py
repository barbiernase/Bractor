# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/vertrag.py
"""
Client-Verträge (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5) — die Basis, von der die GENERIERTEN Client-Basen erben.

Akteur und Client stehen in der Domäne (C#, eine Quelle):

    public sealed record Klassifizierer : IAkteur,
        IHoert<ImagePairKomplett>,                  // ein Fakt, auf den er reagiert
        IDarf<KlassifiziereBildPaarDurchKi>,        // seine Reaktion (und was er von sich aus sendet)
        IHoert<ModellAktiviert>;                    // Kenntnis

    public sealed record KlassifikationsWorker : IClient,
        IVerkoerpert<Klassifizierer>;               // was über die Leitung geht: IDarf und IHoert seiner Akteure

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/vertraege.py` mit einer Basis je Client (`KlassifikationsAppBasis`). Der Worker erbt
sie und füllt je gehörtem Event eine Methode `auf_<event>(e, ctx, state)`: ihre Reaktion — Commands aus dem IDarf der Akteure (ein Wert oder
Stücke, yield) — oder nichts (Kenntnis):

    class ImageClassifier(KlassifikationsAppBasis[ClassifierState]):
        async def auf_image_pair_komplett(self, e, ctx, state):
            return KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=0)
        async def auf_modell_aktiviert(self, e, ctx, state): ...

Was die Basis übernimmt — der Worker schreibt nichts davon von Hand:
  - Reaktion: Dispatch Event-Typ → `auf_<event>`; was sie ausgibt, muss ein Command aus `SPONTAN` sein (sonst `VertragsVerletzung`). Genau einmal
    wirkt sie mit einem Stand (`reaktion_stand`, cqrs_client.reaktionen): der Client liest das Log ab dort und leitet die Command-Ids ab;
  - Vollständigkeit: fehlt eine Methode, lässt sich der Worker nicht instanziieren (ABC) — der Bruch fällt beim Start auf;
  - Handshake: der CapabilitiesRequest nennt Client + Hash (der Server nimmt die Fähigkeiten aus SEINER Tabelle); `FRAGT` sind die Queries der
    Akteure — `query()` mit einem anderen Typ ist eine `VertragsVerletzung`.
"""

from __future__ import annotations

import inspect
import logging
from abc import ABCMeta
from typing import Any, AsyncIterator, ClassVar, TypeVar

from .client import CqrsClient
from .dispatch import HandleDescriptor, HandlerMeta

log = logging.getLogger(__name__)

S = TypeVar("S")


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
                cls.handle._register_method(eingang, _reaktion(eingang, methode))
        return cls


def _reaktion(eingang: type, methode: str):
    async def reagiere(self, payload, ctx, state):
        async for stueck in _stuecke(getattr(self, methode)(payload, ctx, state)):
            for ausgabe in stueck:
                if not isinstance(ausgabe, self.SPONTAN):
                    raise VertragsVerletzung(
                        f"{type(self).__name__}.{methode}: gibt {type(ausgabe).__name__} aus — auf {eingang.__name__} reagiert der Client nur mit "
                        f"Commands seiner Akteure (IDarf): {', '.join(t.__name__ for t in self.SPONTAN) or 'keinen'}")
                yield ausgabe

    reagiere.__name__ = methode
    return reagiere


class ClientVertragBasis(CqrsClient[S], metaclass=VertragsMeta):
    """
    Basis der generierten App-Basen (je App `record X : IApp<Client>` eine). Die generierte Unterklasse setzt:
        APP, CLIENT, AKTEURE, VERTRAG_HASH — Handshake-Name (die App), ihr Client, seine Akteure, Stand;
        HOERT   — Event-DTO → Methode `auf_<event>`: die Reaktion bzw. Kenntnis;
        SPONTAN — Commands, die von sich aus hineingehen (IDarf der verkörperten Akteure ohne die Nachrichten der Quellen der App);
        FRAGT   — die Queries (IDarf der verkörperten Akteure; leer = nicht geprüft).
    """

    APP: ClassVar[str] = ""
    CLIENT: ClassVar[str] = ""
    AKTEURE: ClassVar[tuple[str, ...]] = ()
    VERTRAG_HASH: ClassVar[str] = ""
    HOERT: ClassVar[dict] = {}
    SPONTAN: ClassVar[tuple[type, ...]] = ()
    FRAGT: ClassVar[tuple[type, ...]] = ()

    def __init_subclass__(cls, **kwargs):
        super().__init_subclass__(**kwargs)
        cls._declared_command_types = list(cls.SPONTAN)

    def __init__(self, registry, nutzlast, config: dict[str, Any] | None = None):
        # Die Vertrags-Typen kennt der Vertrag selbst — die (handgepflegte) Registry wird um sie ergänzt: was er hört, was er sendet, was er fragt.
        registry = registry.ergaenzt(events=set(self.HOERT), commands=set(self.SPONTAN), queries=set(self.FRAGT))
        super().__init__(registry, nutzlast, config)

    def _build_capabilities_request(self):
        request = super()._build_capabilities_request()
        request.vertrag = self.APP
        request.vertrag_hash = self.VERTRAG_HASH
        log.info("App %s des Clients %s (verkörpert %s, Hash %s)", self.APP, self.CLIENT, ", ".join(self.AKTEURE), self.VERTRAG_HASH)
        return request

    async def query(self, query_dto):
        """Eine Query stellen — mit Client-Vertrag nur die, die seine Akteure dürfen (`IDarf<T>`)."""
        if self.FRAGT and not isinstance(query_dto, self.FRAGT):
            erlaubt = ", ".join(t.__name__ for t in self.FRAGT)
            raise VertragsVerletzung(
                f"{type(self).__name__}: fragt {type(query_dto).__name__} — die App {self.APP} erlaubt: {erlaubt}")
        return await super().query(query_dto)


__all__ = ["ClientVertragBasis", "VertragsVerletzung", "VertragsMeta", "HandleDescriptor"]
