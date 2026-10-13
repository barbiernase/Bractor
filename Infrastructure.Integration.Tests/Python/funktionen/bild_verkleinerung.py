"""
IBildVerkleinerung für den Integrationstest: rechnet nichts — hängt „_py" an den Pfad, damit der Test im Log sieht, dass DIESER Worker (der
generische funktions_worker.py mit der Tabelle aus dem Codegen) den Auftrag über gRPC ausgeführt hat.
"""
from __future__ import annotations

from cqrs_client.funktion import Ausfuehrung
from domain_client.generated import BildNichtLesbarDto, BildVerkleinertDto, VerkleinereBildDto
from domain_client.generated.funktionen import BildVerkleinerungBasis


class BildVerkleinerung(BildVerkleinerungBasis):
    async def rufe(self, auftrag: VerkleinereBildDto, x: Ausfuehrung) -> BildVerkleinertDto | BildNichtLesbarDto:
        print(f"RUFE {auftrag.quell_pfad} {x.ausfuehrungs_id}", flush=True)
        return BildVerkleinertDto(pfad=auftrag.quell_pfad + "_py.png", breite_pixel=7, hoehe_pixel=auftrag.hoehe)
