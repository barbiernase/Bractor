# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/__init__.py
"""
cqrs_client — Python First-Citizen Framework für CQRS/ES.

Verbindet sich über denselben gRPC-Service wie der Blazor-Client
und nutzt dasselbe Protokoll (domain.proto).

Öffentliche API:
    - CqrsClient[S]: Basisklasse für Clients
    - handle: Dispatch-Deskriptor (@handle.register)
    - CategoryRegistry: Typ-Klassifizierung
    - MessageContext: Handler-Kontext
"""

from .client import CqrsClient
from .dispatch import HandleDescriptor, HandlerBase, handle
from .registry import CategoryRegistry, MessageCategory
from .router import MessageContext
from .versioning import VersionTracker

__all__ = [
    "CqrsClient",
    "CategoryRegistry",
    "HandleDescriptor",
    "HandlerBase",
    "MessageCategory",
    "MessageContext",
    "VersionTracker",
    "handle",
]
