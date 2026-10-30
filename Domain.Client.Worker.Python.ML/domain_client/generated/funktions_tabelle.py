# GENERIERT von Cqrs.Codegen aus den Funktionen draußen (IFunktion mit Auf, docs/09 §9.5). Nicht von Hand ändern.
#
# Die Tabelle des generischen Funktions-Workers (funktions_worker.py): je Funktion mit Python-Basis die Basis (generated/funktionen.py),
# das Modul und die Klasse ihrer Implementierung (funktionen/<modul>.py, die Klasse erbt die Basis und füllt `auf_…`).
# Welche davon ein Worker anbietet, sagt die Umgebung (Funktionen:<F>:Laufort = Extern, Worker-Argumente).
from __future__ import annotations

TABELLE: dict[str, tuple[str, str, str]] = {
    "IBildEinlesen": ("BildEinlesenBasis", "funktionen.bild_einlesen", "BildEinlesen"),
    "IBildVerkleinerung": ("BildVerkleinerungBasis", "funktionen.bild_verkleinerung", "BildVerkleinerung"),
    "IKontrastausgleich": ("KontrastausgleichBasis", "funktionen.kontrastausgleich", "Kontrastausgleich"),
    "IBildpaarBeurteilung": ("BildpaarBeurteilungBasis", "funktionen.bildpaar_beurteilung", "BildpaarBeurteilung"),
    "ITraining": ("TrainingBasis", "funktionen.training", "Training"),
}
