# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/__init__.py
"""
cqrs_client — Python First-Citizen Framework für CQRS/ES.

Verbindet sich über denselben gRPC-Service wie der Blazor-Client und nutzt dieselbe Leitung
(Cqrs.Leitung/cqrs.proto → cqrs_client.leitung, erzeugt von build_leitung.sh). Domänen-DTOs (Proto der Anwendung,
z. B. Domain.Leitung/domain.proto) reisen darin als Nutzlast(typ, daten) — PayloadMapper/NutzlastTabelle packen und entpacken.

Öffentliche API:
    - CqrsClient[S]: Basisklasse für Clients
    - handle: Dispatch-Deskriptor (@handle.register)
    - CategoryRegistry: Typ-Klassifizierung
    - MessageContext: Handler-Kontext
    - NutzlastTabelle: typ → DTO-Klasse (generiert: domain_client/generated/nutzlast_tabelle.py), packe(dto) → Nutzlast,
      entpacke(nutzlast) → DTO
"""

from .client import CqrsClient
from .vertrag import AkteurVertragBasis, Zusage, VertragsVerletzung
from .funktion import Ausfuehrung, FunktionsBasis, FunktionsLaeufer, FunktionsVerletzung
from .dispatch import HandleDescriptor, HandlerBase, handle
from .leitung import Nutzlast
from .mapper import NutzlastTabelle, PayloadMapper, UnbekannteNutzlast
from .registry import CategoryRegistry, MessageCategory
from .router import MessageContext
from .versioning import VersionTracker

__all__ = [
    "AkteurVertragBasis",
    "Ausfuehrung",
    "FunktionsBasis",
    "FunktionsLaeufer",
    "FunktionsVerletzung",
    "CqrsClient",
    "Zusage",
    "VertragsVerletzung",
    "CategoryRegistry",
    "HandleDescriptor",
    "HandlerBase",
    "MessageCategory",
    "MessageContext",
    "Nutzlast",
    "NutzlastTabelle",
    "PayloadMapper",
    "UnbekannteNutzlast",
    "VersionTracker",
    "handle",
]
