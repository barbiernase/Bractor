# GENERIERT von Cqrs.Codegen aus den Client-Verträgen der Domäne (IClientVertrag, docs/konzept-akteure.md §4).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Client eine abstrakte Basis: der Client erbt sie und füllt je getragener Funktion (ITraegt) eine Methode, die die Ausgaben
# ihrer Signatur zurückgibt, und je Kenntnis (IHoert) eine Methode `auf_<event>`. Die Basis bietet die Funktionen an, verdrahtet
# den Dispatch und meldet Client + Hash am Handshake. Ein Client kann mehrere Akteure verkörpern.
from __future__ import annotations

from abc import abstractmethod
from typing import AsyncIterator, ClassVar, TypeVar

from cqrs_client.funktion import Ausgabe, Eingang, Herkunft
from cqrs_client.router import MessageContext
from cqrs_client.vertrag import ClientVertragBasis

from . import (
    ArchiviereModellDto,
    BildPaarDurchKiKlassifiziertDto,
    BildPaarGelabeltDto,
    BildVerfuegbarDto,
    BricheTrainingAbDto,
    DatensatzEingefrorenDto,
    DatensatzErstelltDto,
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
    ModellArchiviertDto,
    ModellRegistriertDto,
    NimmPaarAufDto,
    PaarAufgenommenDto,
    PaarEntferntDto,
    PaareAufgenommenDto,
    PhysischesProduktGelabeltDto,
    RegistriereModellDto,
    SetzeModellAktivDto,
    SetzeSplitDto,
    SplitGesetztDto,
    StarteTrainingDto,
    SucheImagePairsDto,
    TrainingAbgebrochenDto,
    TrainingAbgeschlossenDto,
    TrainingAngefordertDto,
    TrainingBegonnenDto,
    TrainingFortschrittDto,
    TrainingGescheitertDto,
    TrainingHaengengebliebenDto,
)

S = TypeVar("S")


class ArbeitsplatzBasis(ClientVertragBasis[S]):
    """Client-Vertrag Domain.Akteure.IArbeitsplatz — verkörpert Inspekteur, KIOperator, Modellfreigeber, Produktpruefer."""

    CLIENT: ClassVar[str] = "Arbeitsplatz"
    AKTEURE: ClassVar[tuple] = ("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer")
    VERTRAG: ClassVar[str] = "IArbeitsplatz"
    VERTRAG_HASH: ClassVar[str] = "9a5f70d646d1d1b6"
    TRAEGT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert: Kenntnis, verlierbar
        BildPaarDurchKiKlassifiziertDto: "auf_bild_paar_durch_ki_klassifiziert",
        BildPaarGelabeltDto: "auf_bild_paar_gelabelt",
        BildVerfuegbarDto: "auf_bild_verfuegbar",
        DatensatzEingefrorenDto: "auf_datensatz_eingefroren",
        DatensatzErstelltDto: "auf_datensatz_erstellt",
        EinzelBildDurchKiKlassifiziertDto: "auf_einzel_bild_durch_ki_klassifiziert",
        EinzelBildGelabeltDto: "auf_einzel_bild_gelabelt",
        ImagePairErstelltDto: "auf_image_pair_erstellt",
        ImagePairInspiziertDto: "auf_image_pair_inspiziert",
        ImagePairKomplettDto: "auf_image_pair_komplett",
        ModellAktiviertDto: "auf_modell_aktiviert",
        ModellArchiviertDto: "auf_modell_archiviert",
        ModellRegistriertDto: "auf_modell_registriert",
        PaarAufgenommenDto: "auf_paar_aufgenommen",
        PaarEntferntDto: "auf_paar_entfernt",
        PaareAufgenommenDto: "auf_paare_aufgenommen",
        PhysischesProduktGelabeltDto: "auf_physisches_produkt_gelabelt",
        SplitGesetztDto: "auf_split_gesetzt",
        TrainingAbgebrochenDto: "auf_training_abgebrochen",
        TrainingAbgeschlossenDto: "auf_training_abgeschlossen",
        TrainingAngefordertDto: "auf_training_angefordert",
        TrainingBegonnenDto: "auf_training_begonnen",
        TrainingFortschrittDto: "auf_training_fortschritt",
        TrainingGescheitertDto: "auf_training_gescheitert",
        TrainingHaengengebliebenDto: "auf_training_haengengeblieben",
    }
    SPONTAN: ClassVar[tuple] = (LabelBildPaarDto, MarkiereAlsInspiziertDto, LabelPhysischesProduktDto, ErstelleDatensatzDto, FuegeRangeHinzuDto, NimmPaarAufDto, EntfernePaarDto, SetzeSplitDto, FriereEinDto, StarteTrainingDto, BricheTrainingAbDto, RegistriereModellDto, SetzeModellAktivDto, ArchiviereModellDto,)   # ISendet: von sich aus
    FRAGT: ClassVar[tuple] = (SucheImagePairsDto, GetImagePairHistorieDto, GetImagePairStatistikDto, GetProduktionsTageDto, GetProduktionsStripDto, HoleDatensaetzeDto, HoleDatensatzDto, HoleDatensatzPaareDto, HoleDatensaetzeFuerPaarDto, HoleTrainingslaeufeDto, HoleTrainingslaufDto, HoleModelleDto, HoleDatensatzSamplesDto,)   # IFragt: die Queries des Clients

    @abstractmethod
    async def auf_bild_paar_durch_ki_klassifiziert(self, e: BildPaarDurchKiKlassifiziertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<BildPaarDurchKiKlassifiziert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_bild_paar_gelabelt(self, e: BildPaarGelabeltDto, ctx: MessageContext, state: S) -> None:
        """IHoert<BildPaarGelabelt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> None:
        """IHoert<BildVerfuegbar> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_datensatz_eingefroren(self, e: DatensatzEingefrorenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<DatensatzEingefroren> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_datensatz_erstellt(self, e: DatensatzErstelltDto, ctx: MessageContext, state: S) -> None:
        """IHoert<DatensatzErstellt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_einzel_bild_durch_ki_klassifiziert(self, e: EinzelBildDurchKiKlassifiziertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<EinzelBildDurchKiKlassifiziert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_einzel_bild_gelabelt(self, e: EinzelBildGelabeltDto, ctx: MessageContext, state: S) -> None:
        """IHoert<EinzelBildGelabelt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_image_pair_erstellt(self, e: ImagePairErstelltDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ImagePairErstellt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_image_pair_inspiziert(self, e: ImagePairInspiziertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ImagePairInspiziert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ImagePairKomplett> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ModellAktiviert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_modell_archiviert(self, e: ModellArchiviertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ModellArchiviert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_modell_registriert(self, e: ModellRegistriertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ModellRegistriert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_paar_aufgenommen(self, e: PaarAufgenommenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<PaarAufgenommen> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_paar_entfernt(self, e: PaarEntferntDto, ctx: MessageContext, state: S) -> None:
        """IHoert<PaarEntfernt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_paare_aufgenommen(self, e: PaareAufgenommenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<PaareAufgenommen> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_physisches_produkt_gelabelt(self, e: PhysischesProduktGelabeltDto, ctx: MessageContext, state: S) -> None:
        """IHoert<PhysischesProduktGelabelt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_split_gesetzt(self, e: SplitGesetztDto, ctx: MessageContext, state: S) -> None:
        """IHoert<SplitGesetzt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_abgebrochen(self, e: TrainingAbgebrochenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingAbgebrochen> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_abgeschlossen(self, e: TrainingAbgeschlossenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingAbgeschlossen> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_angefordert(self, e: TrainingAngefordertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingAngefordert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_begonnen(self, e: TrainingBegonnenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingBegonnen> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_fortschritt(self, e: TrainingFortschrittDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingFortschritt> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_gescheitert(self, e: TrainingGescheitertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingGescheitert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_training_haengengeblieben(self, e: TrainingHaengengebliebenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingHaengengeblieben> — nur zur Kenntnis"""
        ...


class KlassifikationsWorkerBasis(ClientVertragBasis[S]):
    """Client-Vertrag Domain.Akteure.IKlassifikationsWorker — verkörpert Klassifizierer."""

    CLIENT: ClassVar[str] = "KlassifikationsWorker"
    AKTEURE: ClassVar[tuple] = ("Klassifizierer",)
    VERTRAG: ClassVar[str] = "IKlassifikationsWorker"
    VERTRAG_HASH: ClassVar[str] = "5fb289722d93822e"
    TRAEGT: ClassVar[dict] = {   # ITraegt: Funktionen, deren Code der Client ausführt
        "IBildpaarBeurteilung": {
            ImagePairKomplettDto: Eingang("bildpaar_beurteilung", {KlassifiziereBildPaarDurchKiDto: Ausgabe.COMMAND}),
        },
    }
    HOERT: ClassVar[dict] = {   # IHoert: Kenntnis, verlierbar
        BildVerfuegbarDto: "auf_bild_verfuegbar",
        ModellAktiviertDto: "auf_modell_aktiviert",
    }
    SPONTAN: ClassVar[tuple] = (KlassifiziereEinzelBildDurchKiDto,)   # ISendet: von sich aus
    FRAGT: ClassVar[tuple] = (HoleAktivesModellDto,)   # IFragt: die Queries des Clients

    @abstractmethod
    async def bildpaar_beurteilung(self, e: ImagePairKomplettDto, h: Herkunft, state: S) -> KlassifiziereBildPaarDurchKiDto | None:
        """IBildpaarBeurteilung: KlassifiziereBildPaarDurchKi Auf(ImagePairKomplett e) — höchstens eine Ausgabe"""
        ...

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> None:
        """IHoert<BildVerfuegbar> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<ModellAktiviert> — nur zur Kenntnis"""
        ...


class TrainingsWorkerBasis(ClientVertragBasis[S]):
    """Client-Vertrag Domain.Akteure.ITrainingsWorker — verkörpert TrainingsSystem."""

    CLIENT: ClassVar[str] = "TrainingsWorker"
    AKTEURE: ClassVar[tuple] = ("TrainingsSystem",)
    VERTRAG: ClassVar[str] = "ITrainingsWorker"
    VERTRAG_HASH: ClassVar[str] = "275c8a5d782a1fb3"
    TRAEGT: ClassVar[dict] = {   # ITraegt: Funktionen, deren Code der Client ausführt
        "ITraining": {
            TrainingAngefordertDto: Eingang("training", {MeldeTrainingBegonnenDto: Ausgabe.COMMAND, MeldeFortschrittDto: Ausgabe.COMMAND, MeldeTrainingAbgeschlossenDto: Ausgabe.COMMAND, MeldeTrainingGescheitertDto: Ausgabe.COMMAND}, strom=True),
        },
    }
    HOERT: ClassVar[dict] = {   # IHoert: Kenntnis, verlierbar
        TrainingAbgebrochenDto: "auf_training_abgebrochen",
    }
    SPONTAN: ClassVar[tuple] = ()   # ISendet: von sich aus
    FRAGT: ClassVar[tuple] = (HoleDatensatzSamplesDto,)   # IFragt: die Queries des Clients

    @abstractmethod
    async def training(self, e: TrainingAngefordertDto, h: Herkunft, state: S) -> AsyncIterator[MeldeTrainingBegonnenDto | MeldeFortschrittDto | MeldeTrainingAbgeschlossenDto | MeldeTrainingGescheitertDto]:
        """ITraining: IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>> Auf(TrainingAngefordert e) — Strom: beliebig viele yields"""
        ...

    @abstractmethod
    async def auf_training_abgebrochen(self, e: TrainingAbgebrochenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingAbgebrochen> — nur zur Kenntnis"""
        ...


__all__ = [
    "ArbeitsplatzBasis",
    "KlassifikationsWorkerBasis",
    "TrainingsWorkerBasis",
]
