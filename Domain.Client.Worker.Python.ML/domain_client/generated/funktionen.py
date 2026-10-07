# GENERIERT von Cqrs.Codegen aus den Katalog-Funktionen der Domäne (IFunktion, docs/konzept-editor-pipelines.md §14.5).
# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.
#
# Je Funktion eine abstrakte Basis: der Worker erbt sie, implementiert `rufe(auftrag, x)` und übergibt eine Instanz
# an seinen Client (`funktionen=[…]`). Das SDK meldet sie am Handshake an (Funktion + Slots), nimmt Aufträge an,
# sendet Lebenszeichen und prüft, dass das Ergebnis einer der ERGEBNISSE ist; der Server schreibt es genau einmal.
from __future__ import annotations

from abc import abstractmethod
from typing import ClassVar

from cqrs_client.funktion import Ausfuehrung, FunktionsBasis

from . import (
    BildNichtLesbarDto,
    BildVerkleinertDto,
    DateinameUnbekanntDto,
    DeuteDateinameDto,
    GleicheHistogrammAusDto,
    HistogrammAusgeglichenDto,
    ImagePairDateiGedeutetDto,
    VerkleinereBildDto,
)


class BildVerkleinerungBasis(FunktionsBasis):
    """Funktion Domain.Bildaufbereitung.IBildVerkleinerung: VerkleinereBild → BildVerkleinert | BildNichtLesbar."""

    FUNKTION: ClassVar[str] = "IBildVerkleinerung"
    AUFTRAG: ClassVar[type] = VerkleinereBildDto
    ERGEBNISSE: ClassVar[tuple] = (BildVerkleinertDto, BildNichtLesbarDto)

    @abstractmethod
    async def rufe(self, auftrag: VerkleinereBildDto, x: Ausfuehrung) -> BildVerkleinertDto | BildNichtLesbarDto:
        """Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild auftrag, IAusfuehrung x) — genau ein Ergebnis."""
        ...


class HistogrammAusgleichBasis(FunktionsBasis):
    """Funktion Domain.Bildaufbereitung.IHistogrammAusgleich: GleicheHistogrammAus → HistogrammAusgeglichen | BildNichtLesbar."""

    FUNKTION: ClassVar[str] = "IHistogrammAusgleich"
    AUFTRAG: ClassVar[type] = GleicheHistogrammAusDto
    ERGEBNISSE: ClassVar[tuple] = (HistogrammAusgeglichenDto, BildNichtLesbarDto)

    @abstractmethod
    async def rufe(self, auftrag: GleicheHistogrammAusDto, x: Ausfuehrung) -> HistogrammAusgeglichenDto | BildNichtLesbarDto:
        """Task<OneOf<HistogrammAusgeglichen, BildNichtLesbar>> RufeAsync(GleicheHistogrammAus auftrag, IAusfuehrung x) — genau ein Ergebnis."""
        ...


class DateinameDeutungBasis(FunktionsBasis):
    """Funktion Domain.ImagePair.IDateinameDeutung: DeuteDateiname → ImagePairDateiGedeutet | DateinameUnbekannt."""

    FUNKTION: ClassVar[str] = "IDateinameDeutung"
    AUFTRAG: ClassVar[type] = DeuteDateinameDto
    ERGEBNISSE: ClassVar[tuple] = (ImagePairDateiGedeutetDto, DateinameUnbekanntDto)

    @abstractmethod
    async def rufe(self, auftrag: DeuteDateinameDto, x: Ausfuehrung) -> ImagePairDateiGedeutetDto | DateinameUnbekanntDto:
        """Task<OneOf<ImagePairDateiGedeutet, DateinameUnbekannt>> RufeAsync(DeuteDateiname auftrag, IAusfuehrung x) — genau ein Ergebnis."""
        ...


__all__ = [
    "BildVerkleinerungBasis",
    "HistogrammAusgleichBasis",
    "DateinameDeutungBasis",
]
