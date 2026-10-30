"""
IKontrastausgleich für den Integrationstest: rechnet nichts — die Vorschau heißt „<Pfad>_py.png", damit der Test im Log sieht, dass DIESER Worker
(der generische funktions_worker.py mit der Tabelle aus dem Codegen) den letzten Schritt der Bildaufbereitung über gRPC beantwortet hat.
"""
from __future__ import annotations

from cqrs_client.funktion import Herkunft
from domain_client.generated import BildVerkleinertDto, VorschauErstelltDto
from domain_client.generated.funktionen import KontrastausgleichBasis


class Kontrastausgleich(KontrastausgleichBasis):
    async def auf_bild_verkleinert(self, e: BildVerkleinertDto, h: Herkunft) -> VorschauErstelltDto:
        print(f"AUF {e.pfad} {h.schluessel}", flush=True)
        return VorschauErstelltDto(dateiname=e.dateiname, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, vorschau=e.pfad + "_py.png",
                                   breite_pixel=e.breite_pixel, hoehe_pixel=e.hoehe_pixel, erstellt_am=e.erkannt_am)
