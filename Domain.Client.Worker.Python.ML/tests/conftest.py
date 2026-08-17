import sys
from pathlib import Path

# Worker-Dir (→ domain_client) + SDK-Dir (→ cqrs_client) auf den Importpfad.
ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT.parent / "Client.Infrastructure.Python"))
