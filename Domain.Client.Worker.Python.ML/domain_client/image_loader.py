# REPO-PFAD: Domain.Client.Worker.Python.ML/domain_client/image_loader.py
"""
Bild-Download vom Blazor /api/files/ Endpunkt.
Identischer URL-Aufbau wie LocalFilePathResolver in C#.
"""

from __future__ import annotations

import asyncio
import logging
import struct
from dataclasses import dataclass
from io import BytesIO
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen

import numpy as np
import torch

log = logging.getLogger(__name__)


@dataclass
class DownloadedImage:
    pfad: str
    success: bool = False
    data: bytes = b""
    size_bytes: int = 0
    width: int = 0
    height: int = 0
    error: str = ""


def download_image(file_base_url: str, server_pfad: str) -> DownloadedImage:
    """Lädt ein Bild vom Blazor /api/files/ Endpunkt."""
    result = DownloadedImage(pfad=server_pfad)
    url_path = server_pfad.lstrip("/")
    escaped = "/".join(quote(seg, safe="") for seg in url_path.split("/"))
    url = f"{file_base_url}/api/files/{escaped}"

    try:
        with urlopen(Request(url), timeout=30) as resp:
            result.data = resp.read()
            result.size_bytes = len(result.data)
            result.success = True
        if len(result.data) >= 24 and result.data[:8] == b'\x89PNG\r\n\x1a\n':
            result.width = struct.unpack(">I", result.data[16:20])[0]
            result.height = struct.unpack(">I", result.data[20:24])[0]
    except HTTPError as e:
        result.error = f"HTTP {e.code}: {e.reason}"
    except URLError as e:
        result.error = f"Verbindung fehlgeschlagen: {e.reason}"
    except Exception as e:
        result.error = str(e)
    return result


def image_to_tensor(img: DownloadedImage, device: torch.device) -> torch.Tensor:
    """PNG-Bytes → torch.Tensor [C, H, W] float32 normalized."""
    from PIL import Image
    pil = Image.open(BytesIO(img.data)).convert("RGB")
    arr = np.array(pil, dtype=np.float32) / 255.0
    return torch.from_numpy(arr).permute(2, 0, 1).to(device)


async def download_and_convert(
    file_base_url: str, server_pfad: str, device: torch.device,
) -> tuple[DownloadedImage, torch.Tensor | None]:
    """Bild laden + Tensor-Konvertierung im ThreadPool."""
    def _work():
        img = download_image(file_base_url, server_pfad)
        tensor = image_to_tensor(img, device) if img.success else None
        return img, tensor
    return await asyncio.to_thread(_work)
