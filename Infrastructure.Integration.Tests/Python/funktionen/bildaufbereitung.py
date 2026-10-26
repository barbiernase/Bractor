"""
IBildaufbereitung für den Integrationstest: rechnet nichts — die Vorschau heißt „<Pfad>_py.png", damit der Test im Log sieht, dass DIESER Worker
(der generische funktions_worker.py mit der Tabelle aus dem Codegen) den Eingang über gRPC beantwortet hat.
"""
from __future__ import annotations

from cqrs_client.funktion import Herkunft
from domain_client.generated import BildAufbereitetDto, BildNichtLesbarDto, DateiErkanntDto
from domain_client.generated.funktionen import BildaufbereitungBasis


class Bildaufbereitung(BildaufbereitungBasis):
    async def auf_datei_erkannt(self, e: DateiErkanntDto, h: Herkunft) -> BildAufbereitetDto | BildNichtLesbarDto:
        print(f"AUF {e.pfad} {h.schluessel}", flush=True)
        return BildAufbereitetDto(dateiname=e.name, pfad=e.pfad, groesse=e.groesse, erkannt_am=e.erkannt_am, vorschau=e.pfad + "_py.png",
                                  breite_pixel=7, hoehe_pixel=512, erstellt_am=e.erkannt_am)
