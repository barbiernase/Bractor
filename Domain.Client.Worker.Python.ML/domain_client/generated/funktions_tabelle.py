# GENERIERT von Cqrs.Codegen aus den Funktionen draußen (IFunktion mit Auf, konzept-werkbank §12.3 E13). Nicht von Hand ändern.
#
# Die Tabelle des generischen Funktions-Workers (funktions_worker.py): je Funktion mit Python-Basis die Basis (generated/funktionen.py),
# das Modul und die Klasse ihrer Implementierung (funktionen/<modul>.py, die Klasse erbt die Basis und füllt `auf_…`).
# Welche davon ein Worker anbietet, sagt die Umgebung (Funktionen:<F>:Laufort = Extern, Worker-Argumente).
from __future__ import annotations

TABELLE: dict[str, tuple[str, str, str]] = {
    "IBildaufbereitung": ("BildaufbereitungBasis", "funktionen.bildaufbereitung", "Bildaufbereitung"),
    "IBildpaarBeurteilung": ("BildpaarBeurteilungBasis", "funktionen.bildpaar_beurteilung", "BildpaarBeurteilung"),
    "ITraining": ("TrainingBasis", "funktionen.training", "Training"),
}
