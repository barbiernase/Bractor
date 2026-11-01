# GENERIERT von Cqrs.Codegen aus den Funktionen draußen der Domäne (IFunktion mit Auf, docs/konzept-akteure.md §5).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Funktion eine abstrakte Basis: der Worker erbt sie, füllt je Eingang `auf_<eingang>(e, h)` und übergibt eine Instanz an seinen
# Client (`funktionen=[…]`). Das SDK bietet sie am Handshake an (Funktion + Slots), nimmt Eingänge an, sendet Lebenszeichen und prüft
# jede Ausgabe gegen die Signatur; der Server hält die Antwort fest (die erste gewinnt) und sendet sie.
from __future__ import annotations

from abc import abstractmethod
from typing import AsyncIterator, ClassVar

from cqrs_client.funktion import Ausgabe, Eingang, FunktionsBasis, Herkunft

from . import (
    BildEingelesenDto,
    BildNichtLesbarDto,
    BildVerkleinertDto,
    DateiErkanntDto,
    KlassifiziereBildPaarDurchKiDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingBegonnenDto,
    MeldeTrainingGescheitertDto,
    PaarZuBeurteilenDto,
    TrainingZuRechnenDto,
    VorschauErstelltDto,
)


class BildEinlesenBasis(FunktionsBasis):
    """Funktion Domain.Pipeline.Bildaufbereitung.IBildEinlesen: DateiErkannt → BildEingelesen | BildNichtLesbar."""

    FUNKTION: ClassVar[str] = "IBildEinlesen"
    EINGAENGE: ClassVar[dict] = {
        DateiErkanntDto: Eingang("auf_datei_erkannt", {BildEingelesenDto: Ausgabe.ZWISCHENERGEBNIS, BildNichtLesbarDto: Ausgabe.HINWEIS}),
    }

    @abstractmethod
    async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft) -> BildEingelesenDto | BildNichtLesbarDto | None:
        """OneOf<BildEingelesen, BildNichtLesbar> Auf(DateiErkannt e) — höchstens eine Ausgabe"""
        ...


class BildVerkleinerungBasis(FunktionsBasis):
    """Funktion Domain.Pipeline.Bildaufbereitung.IBildVerkleinerung: BildEingelesen → BildVerkleinert."""

    FUNKTION: ClassVar[str] = "IBildVerkleinerung"
    EINGAENGE: ClassVar[dict] = {
        BildEingelesenDto: Eingang("auf_bild_eingelesen", {BildVerkleinertDto: Ausgabe.ZWISCHENERGEBNIS}),
    }

    @abstractmethod
    async def auf_bild_eingelesen(self, e: BildEingelesenDto, h: Herkunft) -> BildVerkleinertDto | None:
        """BildVerkleinert Auf(BildEingelesen e) — höchstens eine Ausgabe"""
        ...


class KontrastausgleichBasis(FunktionsBasis):
    """Funktion Domain.Pipeline.Bildaufbereitung.IKontrastausgleich: BildVerkleinert → VorschauErstellt."""

    FUNKTION: ClassVar[str] = "IKontrastausgleich"
    EINGAENGE: ClassVar[dict] = {
        BildVerkleinertDto: Eingang("auf_bild_verkleinert", {VorschauErstelltDto: Ausgabe.ZWISCHENERGEBNIS}),
    }

    @abstractmethod
    async def auf_bild_verkleinert(self, e: BildVerkleinertDto, h: Herkunft) -> VorschauErstelltDto | None:
        """VorschauErstellt Auf(BildVerkleinert e) — höchstens eine Ausgabe"""
        ...


class BildpaarBeurteilungBasis(FunktionsBasis):
    """Funktion Domain.Pipeline.ImagePair.IBildpaarBeurteilung: PaarZuBeurteilen → KlassifiziereBildPaarDurchKi."""

    FUNKTION: ClassVar[str] = "IBildpaarBeurteilung"
    EINGAENGE: ClassVar[dict] = {
        PaarZuBeurteilenDto: Eingang("auf_paar_zu_beurteilen", {KlassifiziereBildPaarDurchKiDto: Ausgabe.COMMAND}),
    }

    @abstractmethod
    async def auf_paar_zu_beurteilen(self, e: PaarZuBeurteilenDto, h: Herkunft) -> KlassifiziereBildPaarDurchKiDto | None:
        """KlassifiziereBildPaarDurchKi Auf(PaarZuBeurteilen e) — höchstens eine Ausgabe"""
        ...


class TrainingBasis(FunktionsBasis):
    """Funktion Domain.Pipeline.Trainingslauf.ITraining: TrainingZuRechnen → MeldeTrainingBegonnen | MeldeFortschritt | MeldeTrainingAbgeschlossen | MeldeTrainingGescheitert (Strom)."""

    FUNKTION: ClassVar[str] = "ITraining"
    EINGAENGE: ClassVar[dict] = {
        TrainingZuRechnenDto: Eingang("auf_training_zu_rechnen", {MeldeTrainingBegonnenDto: Ausgabe.COMMAND, MeldeFortschrittDto: Ausgabe.COMMAND, MeldeTrainingAbgeschlossenDto: Ausgabe.COMMAND, MeldeTrainingGescheitertDto: Ausgabe.COMMAND}, strom=True),
    }

    @abstractmethod
    async def auf_training_zu_rechnen(self, e: TrainingZuRechnenDto, h: Herkunft) -> AsyncIterator[MeldeTrainingBegonnenDto | MeldeFortschrittDto | MeldeTrainingAbgeschlossenDto | MeldeTrainingGescheitertDto]:
        """IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>> Auf(TrainingZuRechnen e) — Strom: beliebig viele yields"""
        ...


__all__ = [
    "BildEinlesenBasis",
    "BildVerkleinerungBasis",
    "KontrastausgleichBasis",
    "BildpaarBeurteilungBasis",
    "TrainingBasis",
]
