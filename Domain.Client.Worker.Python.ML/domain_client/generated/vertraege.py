# GENERIERT von Cqrs.Codegen aus den Akteur-Verträgen der Domäne (IAkteurVertrag<A>, docs/konzept-akteure.md §9).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Vertrag eine abstrakte Basis: der Worker erbt sie und implementiert je Reaktion `auf_<event>`. Die Basis
# verdrahtet den Dispatch, prüft jedes Yield gegen den Ausgabe-Vertrag und meldet Vertrag + Hash am Handshake.
from __future__ import annotations

from abc import abstractmethod
from typing import AsyncIterator, ClassVar, TypeVar

from cqrs_client.router import MessageContext
from cqrs_client.vertrag import AkteurVertragBasis, Reaktion

from . import (
    BildVerfuegbarDto,
    ImagePairKomplettDto,
    KlassifiziereBildPaarDurchKiDto,
    KlassifiziereEinzelBildDurchKiDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingBegonnenDto,
    MeldeTrainingGescheitertDto,
    ModellAktiviertDto,
    TrainingAbgebrochenDto,
    TrainingAngefordertDto,
)

S = TypeVar("S")


class KlassifiziererBasis(AkteurVertragBasis[S]):
    """Vertrag Domain.Akteure.IKlassifizierer — Akteur Klassifizierer (Ki)."""

    AKTEUR: ClassVar[str] = "Klassifizierer"
    VERTRAG: ClassVar[str] = "IKlassifizierer"
    VERTRAG_HASH: ClassVar[str] = "21afde97da5d19b9"
    REAKTIONEN: ClassVar[dict] = {
        ImagePairKomplettDto: Reaktion("auf_image_pair_komplett", (KlassifiziereBildPaarDurchKiDto,)),
        BildVerfuegbarDto: Reaktion("auf_bild_verfuegbar", ()),
        ModellAktiviertDto: Reaktion("auf_modell_aktiviert", ()),
    }
    SPONTAN: ClassVar[tuple] = (KlassifiziereEinzelBildDurchKiDto,)   # IDarf-Commands: von sich aus

    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx: MessageContext, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto]:
        """OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e) — höchstens ein yield"""
        ...

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> None:
        """void Auf(BildVerfuegbar e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx: MessageContext, state: S) -> None:
        """void Auf(ModellAktiviert e) — nur zur Kenntnis"""
        ...


class TrainingsSystemBasis(AkteurVertragBasis[S]):
    """Vertrag Domain.Akteure.ITrainingsSystem — Akteur TrainingsSystem (Maschine)."""

    AKTEUR: ClassVar[str] = "TrainingsSystem"
    VERTRAG: ClassVar[str] = "ITrainingsSystem"
    VERTRAG_HASH: ClassVar[str] = "c45c6b980871eb7b"
    REAKTIONEN: ClassVar[dict] = {
        TrainingAngefordertDto: Reaktion("auf_training_angefordert", (MeldeTrainingBegonnenDto, MeldeFortschrittDto, MeldeTrainingAbgeschlossenDto, MeldeTrainingGescheitertDto,), strom=True),
        TrainingAbgebrochenDto: Reaktion("auf_training_abgebrochen", ()),
    }
    SPONTAN: ClassVar[tuple] = ()   # IDarf-Commands: von sich aus

    @abstractmethod
    async def auf_training_angefordert(self, e: TrainingAngefordertDto, ctx: MessageContext, state: S) -> AsyncIterator[MeldeTrainingBegonnenDto | MeldeFortschrittDto | MeldeTrainingAbgeschlossenDto | MeldeTrainingGescheitertDto]:
        """IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>> Auf(TrainingAngefordert e) — Strom: beliebig viele yields"""
        ...

    @abstractmethod
    async def auf_training_abgebrochen(self, e: TrainingAbgebrochenDto, ctx: MessageContext, state: S) -> None:
        """void Auf(TrainingAbgebrochen e) — nur zur Kenntnis"""
        ...


__all__ = [
    "KlassifiziererBasis",
    "TrainingsSystemBasis",
]
