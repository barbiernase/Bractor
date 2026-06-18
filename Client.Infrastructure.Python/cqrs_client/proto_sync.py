# REPO-PFAD: Client.Infrastructure.Python/cqrs_client/proto_sync.py
"""
Automatische Proto-Synchronisation (Laufzeit-Sicherheitsnetz).

Framework-Modul — domain-agnostisch.
Das Zielverzeichnis wird als Parameter übergeben.

Der PRIMÄRE Pfad ist build_python.sh (Build-Zeit).
"""

from __future__ import annotations

import logging
import subprocess
import sys
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

log = logging.getLogger(__name__)


def ensure_types_current(file_base_url: str, generated_dir: Path) -> bool:
    """
    Stellt sicher dass die generierten Python-Typen zum Server passen.

    Args:
        file_base_url: Blazor-Host URL (z.B. "http://server:80")
        generated_dir: Zielverzeichnis für die generierten Typen
    """
    hash_file = generated_dir / ".proto_hash"
    proto_file = generated_dir / ".domain.proto"

    server_hash = _fetch_server_hash(file_base_url)
    if server_hash is None:
        if _local_types_exist(generated_dir):
            log.warning("Proto-Endpoint nicht erreichbar. Verwende lokale Version.")
            return True
        log.error("Proto-Endpoint nicht erreichbar und keine lokalen Typen.")
        return False

    local_hash = hash_file.read_text().strip() if hash_file.exists() else None

    if server_hash == local_hash and _local_types_exist(generated_dir):
        log.info("Proto-Typen aktuell (Hash: %s...)", server_hash[:16])
        return True

    if local_hash:
        log.info("Proto geändert. Regeneriere...")
    else:
        log.info("Erstmalige Proto-Generierung via HTTP...")

    proto_content = _download_proto(file_base_url)
    if proto_content is None:
        return False

    if not _generate_types(proto_content, generated_dir, proto_file):
        return False

    hash_file.write_text(server_hash)
    log.info("Proto-Typen aktualisiert (Hash: %s...)", server_hash[:16])
    return True


def verify_hash_at_connect(file_base_url: str, generated_dir: Path) -> bool:
    """Verifiziert beim Connect dass die lokalen Typen zum Server passen."""
    hash_file = generated_dir / ".proto_hash"
    server_hash = _fetch_server_hash(file_base_url)
    local_hash = hash_file.read_text().strip() if hash_file.exists() else None

    if server_hash is None:
        return True

    if server_hash != local_hash:
        log.error(
            "Proto-Mismatch! Server: %s..., Lokal: %s...",
            server_hash[:16], (local_hash or "keine")[:16],
        )
        return False
    return True


def _fetch_server_hash(url: str) -> str | None:
    try:
        with urlopen(Request(f"{url}/api/proto/version"), timeout=5) as r:
            return r.read().decode().strip()
    except Exception as e:
        log.debug("Proto-Hash nicht abrufbar: %s", e)
        return None


def _download_proto(url: str) -> bytes | None:
    try:
        with urlopen(Request(f"{url}/api/proto/domain.proto"), timeout=30) as r:
            return r.read()
    except Exception as e:
        log.error("Proto-Download fehlgeschlagen: %s", e)
        return None


def _generate_types(content: bytes, generated_dir: Path, proto_file: Path) -> bool:
    generated_dir.mkdir(parents=True, exist_ok=True)
    proto_file.write_bytes(content)
    try:
        r = subprocess.run(
            [sys.executable, "-m", "grpc_tools.protoc",
             f"-I{generated_dir}", f"--python_betterproto_out={generated_dir}",
             str(proto_file)],
            capture_output=True, text=True, timeout=60,
        )
        if r.returncode != 0:
            log.error("betterproto fehlgeschlagen: %s", r.stderr)
            return False
        return (generated_dir / "__init__.py").exists()
    except FileNotFoundError:
        log.error("grpc_tools nicht gefunden. pip install 'betterproto[compiler]'")
        return False


def _local_types_exist(generated_dir: Path) -> bool:
    init = generated_dir / "__init__.py"
    return init.exists() and init.stat().st_size > 0
