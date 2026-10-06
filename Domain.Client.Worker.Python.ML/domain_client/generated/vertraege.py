# GENERIERT von Cqrs.Codegen aus den Akteur-Verträgen der Domäne (IAkteurVertrag<A>, docs/konzept-akteure.md §3).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Vertrag eine abstrakte Basis: der Worker erbt sie und implementiert je Zusage `auf_<event>`. Die Basis
# verdrahtet den Dispatch, prüft jedes Yield gegen den Ausgabe-Vertrag und meldet Vertrag + Hash am Handshake.
# Client-Verträge (IClientVertrag, docs/konzept-akteure.md §4): je Client eine Basis `<Client>Basis` — ein Client
# kann mehrere Akteure verkörpern; jede Zusage nennt den Akteur, in dessen Namen sie antwortet.
from __future__ import annotations

from abc import abstractmethod
from typing import AsyncIterator, ClassVar, TypeVar

from cqrs_client.router import MessageContext
from cqrs_client.vertrag import AkteurVertragBasis, Zusage

from . import (
    ArchiviereModellDto,
    BildPaarDurchKiKlassifiziertDto,
    BildPaarGelabeltDto,
    BildVerfuegbarDto,
    BricheTrainingAbDto,
    EinzelBildDurchKiKlassifiziertDto,
    EinzelBildGelabeltDto,
    EntfernePaarDto,
    ErstelleDatensatzDto,
    FriereEinDto,
    FuegeRangeHinzuDto,
    GetImagePairHistorieDto,
    GetImagePairStatistikDto,
    GetProduktionsStripDto,
    GetProduktionsTageDto,
    HoleAktivesModellDto,
    HoleDatensaetzeDto,
    HoleDatensaetzeFuerPaarDto,
    HoleDatensatzDto,
    HoleDatensatzPaareDto,
    HoleDatensatzSamplesDto,
    HoleModelleDto,
    HoleTrainingslaeufeDto,
    HoleTrainingslaufDto,
    ImagePairErstelltDto,
    ImagePairInspiziertDto,
    ImagePairKomplettDto,
    KlassifiziereBildPaarDurchKiDto,
    KlassifiziereEinzelBildDurchKiDto,
    LabelBildPaarDto,
    LabelPhysischesProduktDto,
    MarkiereAlsInspiziertDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingBegonnenDto,
    MeldeTrainingGescheitertDto,
    ModellAktiviertDto,
    NimmPaarAufDto,
    PaarAufgenommenDto,
    PaarEntferntDto,
    PaareAufgenommenDto,
    PhysischesProduktGelabeltDto,
    RegistriereModellDto,
    SetzeModellAktivDto,
    SetzeSplitDto,
    StarteTrainingDto,
    SucheImagePairsDto,
    TrainingAbgebrochenDto,
    TrainingAngefordertDto,
)

S = TypeVar("S")


class KlassifiziererBasis(AkteurVertragBasis[S]):
    """Vertrag Domain.Akteure.IKlassifizierer — Akteur Klassifizierer (Ki)."""

    AKTEUR: ClassVar[str] = "Klassifizierer"
    VERTRAG: ClassVar[str] = "IKlassifizierer"
    VERTRAG_HASH: ClassVar[str] = "21afde97da5d19b9"
    ZUSAGEN: ClassVar[dict] = {
        ImagePairKomplettDto: Zusage("auf_image_pair_komplett", (KlassifiziereBildPaarDurchKiDto,)),
        BildVerfuegbarDto: Zusage("auf_bild_verfuegbar", ()),
        ModellAktiviertDto: Zusage("auf_modell_aktiviert", ()),
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
    ZUSAGEN: ClassVar[dict] = {
        TrainingAngefordertDto: Zusage("auf_training_angefordert", (MeldeTrainingBegonnenDto, MeldeFortschrittDto, MeldeTrainingAbgeschlossenDto, MeldeTrainingGescheitertDto,), strom=True),
        TrainingAbgebrochenDto: Zusage("auf_training_abgebrochen", ()),
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


class ArbeitsplatzBasis(AkteurVertragBasis[S]):
    """Client-Vertrag Domain.Clients.IArbeitsplatz — verkörpert Inspekteur, KIOperator, Modellfreigeber, Produktpruefer."""

    CLIENT: ClassVar[str] = "Arbeitsplatz"
    AKTEURE: ClassVar[tuple] = ("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer")
    VERTRAG: ClassVar[str] = "IArbeitsplatz"
    VERTRAG_HASH: ClassVar[str] = "01c4adc2db9af3b8"
    ZUSAGEN: ClassVar[dict] = {
        ImagePairErstelltDto: Zusage("auf_image_pair_erstellt", ()),
        BildVerfuegbarDto: Zusage("auf_bild_verfuegbar", ()),
        ImagePairKomplettDto: Zusage("auf_image_pair_komplett", ()),
        BildPaarGelabeltDto: Zusage("auf_bild_paar_gelabelt", ()),
        EinzelBildGelabeltDto: Zusage("auf_einzel_bild_gelabelt", ()),
        BildPaarDurchKiKlassifiziertDto: Zusage("auf_bild_paar_durch_ki_klassifiziert", ()),
        EinzelBildDurchKiKlassifiziertDto: Zusage("auf_einzel_bild_durch_ki_klassifiziert", ()),
        PhysischesProduktGelabeltDto: Zusage("auf_physisches_produkt_gelabelt", ()),
        ImagePairInspiziertDto: Zusage("auf_image_pair_inspiziert", ()),
        PaarAufgenommenDto: Zusage("auf_paar_aufgenommen", ()),
        PaareAufgenommenDto: Zusage("auf_paare_aufgenommen", ()),
        PaarEntferntDto: Zusage("auf_paar_entfernt", ()),
    }
    SPONTAN: ClassVar[tuple] = (LabelBildPaarDto, MarkiereAlsInspiziertDto, LabelPhysischesProduktDto, ErstelleDatensatzDto, FuegeRangeHinzuDto, NimmPaarAufDto, EntfernePaarDto, SetzeSplitDto, FriereEinDto, StarteTrainingDto, BricheTrainingAbDto, RegistriereModellDto, SetzeModellAktivDto, ArchiviereModellDto,)   # ISendet: von sich aus
    FRAGT: ClassVar[tuple] = (SucheImagePairsDto, GetImagePairHistorieDto, GetImagePairStatistikDto, GetProduktionsTageDto, GetProduktionsStripDto, HoleDatensaetzeDto, HoleDatensatzDto, HoleDatensatzPaareDto, HoleDatensaetzeFuerPaarDto, HoleTrainingslaeufeDto, HoleTrainingslaufDto, HoleModelleDto,)   # IFragt: die Queries des Clients

    @abstractmethod
    async def auf_image_pair_erstellt(self, e: ImagePairErstelltDto, ctx: MessageContext, state: S) -> None:
        """void Auf(ImagePairErstellt e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> None:
        """void Auf(BildVerfuegbar e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx: MessageContext, state: S) -> None:
        """void Auf(ImagePairKomplett e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_bild_paar_gelabelt(self, e: BildPaarGelabeltDto, ctx: MessageContext, state: S) -> None:
        """void Auf(BildPaarGelabelt e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_einzel_bild_gelabelt(self, e: EinzelBildGelabeltDto, ctx: MessageContext, state: S) -> None:
        """void Auf(EinzelBildGelabelt e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_bild_paar_durch_ki_klassifiziert(self, e: BildPaarDurchKiKlassifiziertDto, ctx: MessageContext, state: S) -> None:
        """void Auf(BildPaarDurchKiKlassifiziert e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_einzel_bild_durch_ki_klassifiziert(self, e: EinzelBildDurchKiKlassifiziertDto, ctx: MessageContext, state: S) -> None:
        """void Auf(EinzelBildDurchKiKlassifiziert e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_physisches_produkt_gelabelt(self, e: PhysischesProduktGelabeltDto, ctx: MessageContext, state: S) -> None:
        """void Auf(PhysischesProduktGelabelt e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_image_pair_inspiziert(self, e: ImagePairInspiziertDto, ctx: MessageContext, state: S) -> None:
        """void Auf(ImagePairInspiziert e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_paar_aufgenommen(self, e: PaarAufgenommenDto, ctx: MessageContext, state: S) -> None:
        """void Auf(PaarAufgenommen e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_paare_aufgenommen(self, e: PaareAufgenommenDto, ctx: MessageContext, state: S) -> None:
        """void Auf(PaareAufgenommen e) — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_paar_entfernt(self, e: PaarEntferntDto, ctx: MessageContext, state: S) -> None:
        """void Auf(PaarEntfernt e) — nur zur Kenntnis"""
        ...


class KlassifikationsWorkerBasis(AkteurVertragBasis[S]):
    """Client-Vertrag Domain.Clients.IKlassifikationsWorker — verkörpert Klassifizierer."""

    CLIENT: ClassVar[str] = "KlassifikationsWorker"
    AKTEURE: ClassVar[tuple] = ("Klassifizierer",)
    VERTRAG: ClassVar[str] = "IKlassifikationsWorker"
    VERTRAG_HASH: ClassVar[str] = "e9bfd1333fb3493e"
    ZUSAGEN: ClassVar[dict] = {
        ImagePairKomplettDto: Zusage("auf_image_pair_komplett", (KlassifiziereBildPaarDurchKiDto,), akteur="Klassifizierer"),
        BildVerfuegbarDto: Zusage("auf_bild_verfuegbar", (), akteur="Klassifizierer"),
        ModellAktiviertDto: Zusage("auf_modell_aktiviert", (), akteur="Klassifizierer"),
    }
    SPONTAN: ClassVar[tuple] = (KlassifiziereEinzelBildDurchKiDto,)   # ISendet: von sich aus
    FRAGT: ClassVar[tuple] = (HoleAktivesModellDto,)   # IFragt: die Queries des Clients

    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx: MessageContext, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto]:
        """OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e) — als Klassifizierer — höchstens ein yield"""
        ...

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> None:
        """void Auf(BildVerfuegbar e) — als Klassifizierer — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx: MessageContext, state: S) -> None:
        """void Auf(ModellAktiviert e) — als Klassifizierer — nur zur Kenntnis"""
        ...


class TrainingsWorkerBasis(AkteurVertragBasis[S]):
    """Client-Vertrag Domain.Clients.ITrainingsWorker — verkörpert TrainingsSystem."""

    CLIENT: ClassVar[str] = "TrainingsWorker"
    AKTEURE: ClassVar[tuple] = ("TrainingsSystem",)
    VERTRAG: ClassVar[str] = "ITrainingsWorker"
    VERTRAG_HASH: ClassVar[str] = "350ea78b2852d1e2"
    ZUSAGEN: ClassVar[dict] = {
        TrainingAngefordertDto: Zusage("auf_training_angefordert", (MeldeTrainingBegonnenDto, MeldeFortschrittDto, MeldeTrainingAbgeschlossenDto, MeldeTrainingGescheitertDto,), strom=True, akteur="TrainingsSystem"),
        TrainingAbgebrochenDto: Zusage("auf_training_abgebrochen", (), akteur="TrainingsSystem"),
    }
    SPONTAN: ClassVar[tuple] = ()   # ISendet: von sich aus
    FRAGT: ClassVar[tuple] = (HoleDatensatzSamplesDto,)   # IFragt: die Queries des Clients

    @abstractmethod
    async def auf_training_angefordert(self, e: TrainingAngefordertDto, ctx: MessageContext, state: S) -> AsyncIterator[MeldeTrainingBegonnenDto | MeldeFortschrittDto | MeldeTrainingAbgeschlossenDto | MeldeTrainingGescheitertDto]:
        """IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>> Auf(TrainingAngefordert e) — als TrainingsSystem — Strom: beliebig viele yields"""
        ...

    @abstractmethod
    async def auf_training_abgebrochen(self, e: TrainingAbgebrochenDto, ctx: MessageContext, state: S) -> None:
        """void Auf(TrainingAbgebrochen e) — als TrainingsSystem — nur zur Kenntnis"""
        ...


__all__ = [
    "KlassifiziererBasis",
    "TrainingsSystemBasis",
    "ArbeitsplatzBasis",
    "KlassifikationsWorkerBasis",
    "TrainingsWorkerBasis",
]
