# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/vertrag.py
"""
Akteur-Verträge (docs/konzept-akteure.md §3) — die Basis, von der die GENERIERTEN Vertragsklassen erben.

Der Vertrag steht in der Domäne (C#, eine Quelle):

    public interface IKlassifizierer : IAkteurVertrag<Klassifizierer>
    {
        OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e);
        void Auf(BildVerfuegbar e);
    }

`Cqrs.Codegen` erzeugt daraus `domain_client/generated/vertraege.py` mit einer abstrakten Klasse je Vertrag
(`KlassifiziererBasis`). Der Worker erbt sie und implementiert je Zusage eine Methode `auf_<event>`:

    class ImageClassifier(KlassifiziererBasis[ClassifierState]):
        async def auf_image_pair_komplett(self, e, ctx, state):
            yield KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=0)
        async def auf_bild_verfuegbar(self, e, ctx, state): ...

Was die Basis übernimmt — der Worker schreibt nichts davon von Hand:
  - Dispatch: Event-Typ → `auf_<event>` (kein `@handle.register`, kein `_declared_command_types`);
  - Vollständigkeit: fehlt eine Methode, lässt sich der Worker nicht instanziieren (ABC) — der Bruch fällt beim
    Start auf, nicht beim ersten Event;
  - Ausgabe-Vertrag: jedes Yield wird geprüft (Typ ∈ Ausgänge der Zusage; ohne Strom höchstens einer) —
    ein Verstoß ist eine `VertragsVerletzung`, der Command geht nicht hinaus;
  - Handshake: der CapabilitiesRequest nennt Vertrag + Hash (der Server nimmt die Fähigkeiten aus SEINER Tabelle).

CLIENT-Verträge (docs/konzept-akteure.md §4): ein Client ist die Software an der Leitung und kann MEHRERE Akteure
verkörpern. `Cqrs.Codegen` erzeugt je `interface IX : IClientVertrag, …` eine Basis `XBasis` mit `CLIENT = "X"`; jede
Zusage nennt den Akteur, in dessen Namen sie antwortet (`Zusage(..., akteur="Klassifizierer")`). Am Handshake steht
dann der Client-Name statt eines Akteurs; der Server leitet die Akteure aus dem Vertrag ab (∩ Token). `FRAGT` sind die
Queries des Vertrags — `query()` mit einem anderen Typ ist eine `VertragsVerletzung`.
"""

from __future__ import annotations

import inspect
import logging
from abc import ABCMeta
from dataclasses import dataclass
from typing import Any, AsyncIterator, ClassVar, TypeVar

from .client import CqrsClient
from .dispatch import HandleDescriptor, HandlerMeta

log = logging.getLogger(__name__)

S = TypeVar("S")


@dataclass(frozen=True)
class Zusage:
    """Eine Zusage des Vertrags: `Auf(Eingang)` → Methode, erlaubte Ausgänge, Strom (mehrere Ausgaben je Zusage),
    Akteur (in wessen Namen geantwortet wird; leer = Kenntnis des Clients bzw. Akteur-Vertrag mit genau einem Akteur)."""
    methode: str
    ausgaenge: tuple[type, ...] = ()
    strom: bool = False
    akteur: str = ""


class VertragsVerletzung(TypeError):
    """Ein Worker gibt etwas aus, das sein Vertrag nicht erlaubt (falscher Typ oder mehr als eine Ausgabe ohne Strom)."""


class VertragsMeta(HandlerMeta, ABCMeta):
    """
    HandlerMeta (Dispatch-Deskriptor je Klasse) + ABCMeta (abstrakte Zusagen). Eine Klasse, die `ZUSAGEN` selbst
    deklariert (die generierte Basis), verdrahtet je Eingang einen Prüf-Wrapper — er ruft die Methode über ihren NAMEN
    auf, also die Implementierung der Unterklasse.
    """

    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)
        if "ZUSAGEN" in namespace:
            for eingang, zusage in namespace["ZUSAGEN"].items():
                cls.handle._register_method(eingang, _wrapper(eingang, zusage))
        return cls


def _wrapper(eingang: type, zusage: Zusage):
    async def reagiere(self, payload, ctx, state):
        n = 0
        async for aus in _ausgaben(getattr(self, zusage.methode)(payload, ctx, state)):
            if not isinstance(aus, zusage.ausgaenge):
                erlaubt = ", ".join(t.__name__ for t in zusage.ausgaenge) or "nichts (nur zur Kenntnis)"
                raise VertragsVerletzung(
                    f"{type(self).__name__}.{zusage.methode}: gibt {type(aus).__name__} aus — "
                    f"der Vertrag {self.VERTRAG} erlaubt auf {eingang.__name__}: {erlaubt}")
            n += 1
            if n > 1 and not zusage.strom:
                raise VertragsVerletzung(
                    f"{type(self).__name__}.{zusage.methode}: mehr als eine Ausgabe — "
                    f"die Zusage auf {eingang.__name__} ist kein Strom (IAsyncEnumerable im Vertrag)")
            yield aus

    reagiere.__name__ = zusage.methode
    return reagiere


async def _ausgaben(ergebnis) -> AsyncIterator[Any]:
    """Async-/Sync-Generator, Coroutine oder Wert → die Ausgaben (wie HandleDescriptor.receive)."""
    if inspect.isasyncgen(ergebnis):
        async for x in ergebnis:
            yield x
    elif inspect.isawaitable(ergebnis):
        x = await ergebnis
        if x is not None:
            yield x
    elif inspect.isgenerator(ergebnis):
        for x in ergebnis:
            yield x
    elif ergebnis is not None:
        yield ergebnis


class AkteurVertragBasis(CqrsClient[S], metaclass=VertragsMeta):
    """
    Basis der generierten Vertragsklassen. Die generierte Unterklasse setzt:
        AKTEUR, VERTRAG, VERTRAG_HASH  — wer, welcher Vertrag, welcher Stand (Akteur-Vertrag);
        CLIENT, AKTEURE                — bei einem Client-Vertrag: sein Name (Handshake) und die verkörperten Akteure;
        ZUSAGEN                     — Event-DTO → Zusage (Methode, Ausgänge, Strom, Akteur);
        SPONTAN                        — Commands, die von sich aus hineingehen (IDarf bzw. ISendet);
        FRAGT                          — die Queries des Client-Vertrags (IFragt; leer = nicht geprüft).
    """

    AKTEUR: ClassVar[str] = ""
    CLIENT: ClassVar[str] = ""
    AKTEURE: ClassVar[tuple[str, ...]] = ()
    FRAGT: ClassVar[tuple[type, ...]] = ()
    VERTRAG: ClassVar[str] = ""
    VERTRAG_HASH: ClassVar[str] = ""
    ZUSAGEN_ALLE: ClassVar[dict[type, Zusage]] = {}
    SPONTAN: ClassVar[tuple[type, ...]] = ()

    def __init_subclass__(cls, **kwargs):
        super().__init_subclass__(**kwargs)
        # Alle Zusagen der Kette (für Registry und Capabilities) — die generierte Basis deklariert sie in ZUSAGEN.
        alle: dict[type, Zusage] = {}
        for k in reversed(cls.__mro__):
            alle.update(k.__dict__.get("ZUSAGEN", {}))
        cls.ZUSAGEN_ALLE = alle
        cls._declared_command_types = list(dict.fromkeys(
            [t for r in alle.values() for t in r.ausgaenge] + list(cls.SPONTAN)))

    def __init__(self, registry, generated_module, config: dict[str, Any] | None = None):
        # Die Vertrags-Typen kennt der Vertrag selbst — die (handgepflegte) Registry wird um sie ergänzt.
        ausgaenge = {t for r in self.ZUSAGEN_ALLE.values() for t in r.ausgaenge}
        registry = registry.ergaenzt(events=set(self.ZUSAGEN_ALLE), commands=ausgaenge)
        super().__init__(registry, generated_module, config)

    def _build_capabilities_request(self):
        request = super()._build_capabilities_request()
        felder = getattr(request, "__dataclass_fields__", {})
        if "vertrag" in felder:
            # Client-Vertrag: der Client-Name (der Server leitet die Akteure ab); sonst der Akteur des Akteur-Vertrags.
            request.vertrag = self.CLIENT or self.AKTEUR
            request.vertrag_hash = self.VERTRAG_HASH
        else:
            log.warning("CapabilitiesRequest ohne Feld 'vertrag' — Generat veraltet (build_python.sh)")
        if self.CLIENT:
            log.info("Client %s (Vertrag %s, verkörpert %s, Hash %s)", self.CLIENT, self.VERTRAG, ", ".join(self.AKTEURE), self.VERTRAG_HASH)
        else:
            log.info("Vertrag %s (Akteur %s, Hash %s)", self.VERTRAG, self.AKTEUR, self.VERTRAG_HASH)
        return request

    async def query(self, query_dto):
        """Eine Query stellen — mit Client-Vertrag nur die, die er nennt (`IFragt<T>`)."""
        if self.FRAGT and not isinstance(query_dto, self.FRAGT):
            erlaubt = ", ".join(t.__name__ for t in self.FRAGT)
            raise VertragsVerletzung(
                f"{type(self).__name__}: fragt {type(query_dto).__name__} — der Vertrag {self.VERTRAG} erlaubt: {erlaubt}")
        return await super().query(query_dto)


__all__ = ["AkteurVertragBasis", "Zusage", "VertragsVerletzung", "VertragsMeta", "HandleDescriptor"]
