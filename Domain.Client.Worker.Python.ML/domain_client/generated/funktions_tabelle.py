# GENERIERT von Cqrs.Codegen aus den Katalog-Funktionen der Domäne (IFunktion, konzept-werkbank §12.3 E13). Nicht von Hand ändern.
#
# Die Tabelle des generischen Funktions-Workers (funktions_worker.py): je Funktion mit Python-Basis die Basis (generated/funktionen.py),
# das Modul und die Klasse ihrer Implementierung (funktionen/<modul>.py, die Klasse erbt die Basis und füllt `rufe`).
# Welche davon ein Worker anbietet, sagt die Umgebung (Funktionen:<F>:Laufort = Extern, Worker-Argumente).
from __future__ import annotations

TABELLE: dict[str, tuple[str, str, str]] = {
    "IBildVerkleinerung": ("BildVerkleinerungBasis", "funktionen.bild_verkleinerung", "BildVerkleinerung"),
    "IHistogrammAusgleich": ("HistogrammAusgleichBasis", "funktionen.histogramm_ausgleich", "HistogrammAusgleich"),
    "IDateinameDeutung": ("DateinameDeutungBasis", "funktionen.dateiname_deutung", "DateinameDeutung"),
}
