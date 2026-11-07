# GENERIERT von Cqrs.Codegen aus den Clients der Domäne (record X : IClient, docs/konzept-akteure.md §4).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Client eine abstrakte Basis aus den Akteuren, die er verkörpert: je gehörtem Command (Arbeit) eine Methode `auf_<command>`, die die
# Antwort liefert — Commands über das IDarf des Akteurs, gern in Stücken (yield) —, und je gehörtem Event oder Hinweis eine Methode
# `auf_<event>`: die Reaktion (Commands des IDarf) oder nichts (Kenntnis). Die Basis verdrahtet den Dispatch und meldet Client + Hash am Handshake.
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
    MeldeTeilFertigDto,
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
    TeilauftragAbgeschlossenDto,
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
    """Client Domain.Akteure.Arbeitsplatz — verkörpert Inspekteur, KIOperator, Modellfreigeber, Produktpruefer."""

    CLIENT: ClassVar[str] = "Arbeitsplatz"
    AKTEURE: ClassVar[tuple] = ("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer")
    VERTRAG_HASH: ClassVar[str] = "095f9149cfc76d4d"
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Reaktion oder Kenntnis
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
    async def auf_bild_paar_durch_ki_klassifiziert(self, e: BildPaarDurchKiKlassifiziertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<BildPaarDurchKiKlassifiziert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_bild_paar_gelabelt(self, e: BildPaarGelabeltDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<BildPaarGelabelt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<BildVerfuegbar> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_datensatz_eingefroren(self, e: DatensatzEingefrorenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<DatensatzEingefroren> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_datensatz_erstellt(self, e: DatensatzErstelltDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<DatensatzErstellt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_einzel_bild_durch_ki_klassifiziert(self, e: EinzelBildDurchKiKlassifiziertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<EinzelBildDurchKiKlassifiziert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_einzel_bild_gelabelt(self, e: EinzelBildGelabeltDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<EinzelBildGelabelt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_image_pair_erstellt(self, e: ImagePairErstelltDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<ImagePairErstellt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_image_pair_inspiziert(self, e: ImagePairInspiziertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<ImagePairInspiziert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<ImagePairKomplett> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<ModellAktiviert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_modell_archiviert(self, e: ModellArchiviertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<ModellArchiviert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_modell_registriert(self, e: ModellRegistriertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<ModellRegistriert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_paar_aufgenommen(self, e: PaarAufgenommenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<PaarAufgenommen> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_paar_entfernt(self, e: PaarEntferntDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<PaarEntfernt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_paare_aufgenommen(self, e: PaareAufgenommenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<PaareAufgenommen> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_physisches_produkt_gelabelt(self, e: PhysischesProduktGelabeltDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<PhysischesProduktGelabelt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_split_gesetzt(self, e: SplitGesetztDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<SplitGesetzt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_abgebrochen(self, e: TrainingAbgebrochenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingAbgebrochen> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_abgeschlossen(self, e: TrainingAbgeschlossenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingAbgeschlossen> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_angefordert(self, e: TrainingAngefordertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingAngefordert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_begonnen(self, e: TrainingBegonnenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingBegonnen> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_fortschritt(self, e: TrainingFortschrittDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingFortschritt> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_gescheitert(self, e: TrainingGescheitertDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingGescheitert> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_training_haengengeblieben(self, e: TrainingHaengengebliebenDto, ctx: MessageContext, state: S) -> AsyncIterator[LabelBildPaarDto | LabelEinzelBildDto | LabelBildRegionDto | MarkiereAlsInspiziertDto | ErstelleDatensatzDto | FuegeRangeHinzuDto | NimmPaarAufDto | EntfernePaarDto | SetzeSplitDto | FriereEinDto | StarteTrainingDto | BricheTrainingAbDto | RegistriereModellDto | SetzeModellAktivDto | ArchiviereModellDto | LabelPhysischesProduktDto] | None:
        """IHoert<TrainingHaengengeblieben> — die Reaktion: LabelBildPaar, LabelEinzelBild, LabelBildRegion, MarkiereAlsInspiziert, ErstelleDatensatz, FuegeRangeHinzu, NimmPaarAuf, EntfernePaar, SetzeSplit, FriereEin, StarteTraining, BricheTrainingAb, RegistriereModell, SetzeModellAktiv, ArchiviereModell, LabelPhysischesProdukt (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...


class KlassifikationsWorkerBasis(ClientVertragBasis[S]):
    """Client Domain.Akteure.KlassifikationsWorker — verkörpert Klassifizierer."""

    CLIENT: ClassVar[str] = "KlassifikationsWorker"
    AKTEURE: ClassVar[tuple] = ("Klassifizierer",)
    VERTRAG_HASH: ClassVar[str] = "e7dd2142dc11d49c"
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Reaktion oder Kenntnis
        BildVerfuegbarDto: "auf_bild_verfuegbar",
        ImagePairKomplettDto: "auf_image_pair_komplett",
        ModellAktiviertDto: "auf_modell_aktiviert",
    }
    SPONTAN: ClassVar[tuple] = (KlassifiziereBildPaarDurchKiDto, KlassifiziereEinzelBildDurchKiDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (HoleAktivesModellDto,)   # IDarf der Akteure: ihre Queries

    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx: MessageContext, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto | KlassifiziereEinzelBildDurchKiDto] | None:
        """IHoert<BildVerfuegbar> — die Reaktion: KlassifiziereBildPaarDurchKi, KlassifiziereEinzelBildDurchKi (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx: MessageContext, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto | KlassifiziereEinzelBildDurchKiDto] | None:
        """IHoert<ImagePairKomplett> — die Reaktion: KlassifiziereBildPaarDurchKi, KlassifiziereEinzelBildDurchKi (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx: MessageContext, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto | KlassifiziereEinzelBildDurchKiDto] | None:
        """IHoert<ModellAktiviert> — die Reaktion: KlassifiziereBildPaarDurchKi, KlassifiziereEinzelBildDurchKi (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...


class ReaktionsWorkerBasis(ClientVertragBasis[S]):
    """Client Domain.Akteure.ReaktionsWorker — verkörpert Datensatzpfleger, Teilmelder."""

    CLIENT: ClassVar[str] = "ReaktionsWorker"
    AKTEURE: ClassVar[tuple] = ("Datensatzpfleger", "Teilmelder")
    VERTRAG_HASH: ClassVar[str] = "23f2d1708aa2efd3"
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Reaktion oder Kenntnis
        EinfrierenAngefordertDto: "auf_einfrieren_angefordert",
        RangeAngefordertDto: "auf_range_angefordert",
        TeilauftragAbgeschlossenDto: "auf_teilauftrag_abgeschlossen",
    }
    SPONTAN: ClassVar[tuple] = (NimmRangeAufDto, SchliesseEinfrierenAbDto, MeldeTeilFertigDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (SucheImagePairsDto, GetImagePairDto,)   # IDarf der Akteure: ihre Queries

    @abstractmethod
    async def auf_einfrieren_angefordert(self, e: EinfrierenAngefordertDto, ctx: MessageContext, state: S) -> AsyncIterator[NimmRangeAufDto | SchliesseEinfrierenAbDto | MeldeTeilFertigDto] | None:
        """IHoert<EinfrierenAngefordert> — die Reaktion: NimmRangeAuf, SchliesseEinfrierenAb, MeldeTeilFertig (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_range_angefordert(self, e: RangeAngefordertDto, ctx: MessageContext, state: S) -> AsyncIterator[NimmRangeAufDto | SchliesseEinfrierenAbDto | MeldeTeilFertigDto] | None:
        """IHoert<RangeAngefordert> — die Reaktion: NimmRangeAuf, SchliesseEinfrierenAb, MeldeTeilFertig (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...

    @abstractmethod
    async def auf_teilauftrag_abgeschlossen(self, e: TeilauftragAbgeschlossenDto, ctx: MessageContext, state: S) -> AsyncIterator[NimmRangeAufDto | SchliesseEinfrierenAbDto | MeldeTeilFertigDto] | None:
        """IHoert<TeilauftragAbgeschlossen> — die Reaktion: NimmRangeAuf, SchliesseEinfrierenAb, MeldeTeilFertig (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...


class TrainingsWorkerBasis(ClientVertragBasis[S]):
    """Client Domain.Akteure.TrainingsWorker — verkörpert TrainingsSystem."""

    CLIENT: ClassVar[str] = "TrainingsWorker"
    AKTEURE: ClassVar[tuple] = ("TrainingsSystem",)
    VERTRAG_HASH: ClassVar[str] = "bc9b0f0d5825ddb4"
    ARBEIT: ClassVar[dict] = {}
    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Reaktion oder Kenntnis
        TrainingAngefordertDto: "auf_training_angefordert",
    }
    SPONTAN: ClassVar[tuple] = (MeldeTrainingBegonnenDto, MeldeFortschrittDto, MeldeTrainingAbgeschlossenDto, MeldeTrainingGescheitertDto,)   # IDarf der Akteure: Commands und Hinweise
    FRAGT: ClassVar[tuple] = (HoleDatensatzSamplesDto, HoleTrainingslaufDto,)   # IDarf der Akteure: ihre Queries

    @abstractmethod
    async def auf_training_angefordert(self, e: TrainingAngefordertDto, ctx: MessageContext, state: S) -> AsyncIterator[MeldeTrainingBegonnenDto | MeldeFortschrittDto | MeldeTrainingAbgeschlossenDto | MeldeTrainingGescheitertDto] | None:
        """IHoert<TrainingAngefordert> — die Reaktion: MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert (ein Wert oder Stücke, yield); oder nichts (Kenntnis)"""
        ...


__all__ = [
    "ArbeitsplatzBasis",
    "KlassifikationsWorkerBasis",
    "ReaktionsWorkerBasis",
    "TrainingsWorkerBasis",
]
