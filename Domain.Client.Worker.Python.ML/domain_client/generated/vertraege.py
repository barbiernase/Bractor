# GENERIERT von Cqrs.Codegen aus den Clients der Domäne (record X : IClient, docs/konzept-akteure.md §4).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Client eine abstrakte Basis aus den Akteuren, die er verkörpert: je gehörtem Command (Arbeit) eine Methode `auf_<command>`, die die
# Antwort liefert — Commands über das IDarf des Akteurs, gern in Stücken (yield) —, und je gehörtem Event oder Hinweis (Kenntnis) eine
# Methode `auf_<event>` ohne Ausgabe. Die Basis nimmt die Arbeit an, verdrahtet den Dispatch und meldet Client + Hash am Handshake.
from __future__ import annotations

from abc import abstractmethod
from typing import AsyncIterator, ClassVar, TypeVar

from cqrs_client.arbeit import Arbeit, Herkunft
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
    EinfrierenAngefordertDto,
    EinzelBildDurchKiKlassifiziertDto,
    EinzelBildGelabeltDto,
    EntfernePaarDto,
    ErstelleDatensatzDto,
    FriereEinDto,
    FuegeRangeHinzuDto,
    GetImagePairDto,
    GetImagePairHistorieDto,
    GetImagePairStatistikDto,
    GetProduktionsStripDto,
    GetProduktionsTageDto,
    GetProduktionsVerlaufDto,
    GetUnklassifizierteImagePairsDto,
    HoleAktivesModellDto,
    HoleBildPaarStandDto,
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
    LabelBildRegionDto,
    LabelEinzelBildDto,
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
    NimmRangeAufDto,
    PaarAufgenommenDto,
    PaarEntferntDto,
    PaarZuBeurteilenDto,
    PaareAufgenommenDto,
    PhysischesProduktGelabeltDto,
    RangeAngefordertDto,
    RegistriereModellDto,
    SchliesseEinfrierenAbDto,
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
    TrainingZuRechnenDto,
)

S = TypeVar("S")


class ArbeitsplatzBasis(ClientVertragBasis[S]):
    """Client Domain.Akteure.Arbeitsplatz — verkörpert Inspekteur, KIOperator, Modellfreigeber, Produktpruefer."""

    CLIENT: ClassVar[str] = "Arbeitsplatz"
    AKTEURE: ClassVar[tuple] = ("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer")
    VERTRAG_HASH: ClassVar[str] = "095f9149cfc76d4d"
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Kenntnis, verlierbar
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
    SPONTAN: ClassVar[tuple] = (LabelBildPaarDto, LabelEinzelBildDto, LabelBildRegionDto, MarkiereAlsInspiziertDto, ErstelleDatensatzDto, FuegeRangeHinzuDto, NimmPaarAufDto, EntfernePaarDto, SetzeSplitDto, FriereEinDto, StarteTrainingDto, BricheTrainingAbDto, RegistriereModellDto, SetzeModellAktivDto, ArchiviereModellDto, LabelPhysischesProduktDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (SucheImagePairsDto, GetImagePairDto, GetImagePairHistorieDto, GetImagePairStatistikDto, GetProduktionsTageDto, GetProduktionsStripDto, GetProduktionsVerlaufDto, GetUnklassifizierteImagePairsDto, HoleBildPaarStandDto, HoleDatensaetzeDto, HoleDatensatzDto, HoleDatensatzPaareDto, HoleDatensaetzeFuerPaarDto, HoleTrainingslaeufeDto, HoleTrainingslaufDto, HoleDatensatzSamplesDto, HoleModelleDto, HoleAktivesModellDto,)   # IDarf der Akteure: ihre Queries

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


class DatensatzWorkerBasis(ClientVertragBasis[S]):
    """Client Domain.Akteure.DatensatzWorker — verkörpert Datensatzpfleger."""

    CLIENT: ClassVar[str] = "DatensatzWorker"
    AKTEURE: ClassVar[tuple] = ("Datensatzpfleger",)
    VERTRAG_HASH: ClassVar[str] = "3ea75bd61cbb3886"
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Kenntnis, verlierbar
        EinfrierenAngefordertDto: "auf_einfrieren_angefordert",
        RangeAngefordertDto: "auf_range_angefordert",
    }
    SPONTAN: ClassVar[tuple] = (NimmRangeAufDto, SchliesseEinfrierenAbDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (SucheImagePairsDto, GetImagePairDto,)   # IDarf der Akteure: ihre Queries

    @abstractmethod
    async def auf_einfrieren_angefordert(self, e: EinfrierenAngefordertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<EinfrierenAngefordert> — nur zur Kenntnis"""
        ...

    @abstractmethod
    async def auf_range_angefordert(self, e: RangeAngefordertDto, ctx: MessageContext, state: S) -> None:
        """IHoert<RangeAngefordert> — nur zur Kenntnis"""
        ...


class KlassifikationsWorkerBasis(ClientVertragBasis[S]):
    """Client Domain.Akteure.KlassifikationsWorker — verkörpert Klassifizierer."""

    CLIENT: ClassVar[str] = "KlassifikationsWorker"
    AKTEURE: ClassVar[tuple] = ("Klassifizierer",)
    VERTRAG_HASH: ClassVar[str] = "f709fe1440fd6bc2"
    ARBEIT: ClassVar[dict] = {   # IHoert eines Commands ohne Ziel: Arbeit des Akteurs, die Antwort sind Commands seines IDarf
        "Klassifizierer": {
            PaarZuBeurteilenDto: Arbeit("auf_paar_zu_beurteilen", (KlassifiziereBildPaarDurchKiDto, KlassifiziereEinzelBildDurchKiDto,)),
        },
    }
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Kenntnis, verlierbar
        BildVerfuegbarDto: "auf_bild_verfuegbar",
        ModellAktiviertDto: "auf_modell_aktiviert",
    }
    SPONTAN: ClassVar[tuple] = (KlassifiziereBildPaarDurchKiDto, KlassifiziereEinzelBildDurchKiDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (HoleAktivesModellDto,)   # IDarf der Akteure: ihre Queries

    @abstractmethod
    async def auf_paar_zu_beurteilen(self, c: PaarZuBeurteilenDto, h: Herkunft, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto | KlassifiziereEinzelBildDurchKiDto]:
        """Arbeit des Klassifizierer (IHoert<PaarZuBeurteilen>): die Antwort — KlassifiziereBildPaarDurchKi, KlassifiziereEinzelBildDurchKi; ein Wert, eine Liste oder Stücke (yield)"""
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
    """Client Domain.Akteure.TrainingsWorker — verkörpert TrainingsSystem."""

    CLIENT: ClassVar[str] = "TrainingsWorker"
    AKTEURE: ClassVar[tuple] = ("TrainingsSystem",)
    VERTRAG_HASH: ClassVar[str] = "07696c2a45fbf8ea"
    ARBEIT: ClassVar[dict] = {   # IHoert eines Commands ohne Ziel: Arbeit des Akteurs, die Antwort sind Commands seines IDarf
        "TrainingsSystem": {
            TrainingZuRechnenDto: Arbeit("auf_training_zu_rechnen", (MeldeFortschrittDto, MeldeTrainingAbgeschlossenDto, MeldeTrainingBegonnenDto, MeldeTrainingGescheitertDto,)),
        },
    }
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Kenntnis, verlierbar
        TrainingAbgebrochenDto: "auf_training_abgebrochen",
    }
    SPONTAN: ClassVar[tuple] = (MeldeTrainingBegonnenDto, MeldeFortschrittDto, MeldeTrainingAbgeschlossenDto, MeldeTrainingGescheitertDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (HoleDatensatzSamplesDto,)   # IDarf der Akteure: ihre Queries

    @abstractmethod
    async def auf_training_zu_rechnen(self, c: TrainingZuRechnenDto, h: Herkunft, state: S) -> AsyncIterator[MeldeFortschrittDto | MeldeTrainingAbgeschlossenDto | MeldeTrainingBegonnenDto | MeldeTrainingGescheitertDto]:
        """Arbeit des TrainingsSystem (IHoert<TrainingZuRechnen>): die Antwort — MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingBegonnen, MeldeTrainingGescheitert; ein Wert, eine Liste oder Stücke (yield)"""
        ...

    @abstractmethod
    async def auf_training_abgebrochen(self, e: TrainingAbgebrochenDto, ctx: MessageContext, state: S) -> None:
        """IHoert<TrainingAbgebrochen> — nur zur Kenntnis"""
        ...


__all__ = [
    "ArbeitsplatzBasis",
    "DatensatzWorkerBasis",
    "KlassifikationsWorkerBasis",
    "TrainingsWorkerBasis",
]
