#!/usr/bin/env bash
# Richtet den Messserver ein (Ubuntu 24.04, als root, idempotent). Aufruf nur über `scripts/server.sh einrichten`, nie auf dem Mac.
#
#   scripts/server-einrichten.sh <dotnet-sdk-version>
#
# Was es tut: .NET-SDK in genau der Version des lokalen Rechners (/usr/local/share/dotnet), Python-Umgebung der Funktions-Worker
# (/opt/bractor-python, torch nur CPU), Infrastruktur-Container aus docker-compose.infrastructure.yml (Projekt `cqrs`, Ports nur 127.0.0.1).
# Was es nicht tut: Firewall, SSH, Caddy und die übrigen Container des Servers — die bleiben unberührt.
set -euo pipefail

SDK="${1:?Aufruf: server-einrichten.sh <dotnet-sdk-version>}"
WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
PY=/opt/bractor-python
LAEUFE=/opt/bractor-laeufe

# ── .NET-SDK (dotnet-install.sh von Microsoft; das SDK von Ubuntu 24.04 endet bei 8.0) ──
if ! /usr/local/share/dotnet/dotnet --list-sdks 2>/dev/null | grep -q "^$SDK "; then
  echo "== .NET-SDK $SDK installieren"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --version "$SDK" --install-dir /usr/local/share/dotnet --no-path
  rm -f /tmp/dotnet-install.sh
fi
ln -sfn /usr/local/share/dotnet/dotnet /usr/local/bin/dotnet
cat > /etc/profile.d/bractor.sh <<'EOF'
export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$DOTNET_ROOT:$PATH"
EOF

# ── Python der Funktions-Worker (S2, Fluss-E2E mit Worker, pytest des SDK) ──
if ! command -v jq >/dev/null || ! dpkg -s python3.12-venv >/dev/null 2>&1; then
  echo "== jq, python3-venv installieren"
  apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq jq python3.12-venv >/dev/null
fi
if [ ! -x "$PY/bin/python" ]; then
  echo "== Python-Umgebung $PY anlegen"
  python3 -m venv "$PY"
fi
if ! "$PY/bin/python" -c "import torch, cv2, betterproto, grpclib, PIL, tifffile, pytest_asyncio" 2>/dev/null; then
  echo "== Python-Pakete installieren"
  "$PY/bin/pip" install -q --upgrade pip
  "$PY/bin/pip" install -q torch --index-url https://download.pytorch.org/whl/cpu
  "$PY/bin/pip" install -q "betterproto[compiler]==2.0.0b7" grpclib grpcio-tools numpy opencv-python-headless pillow tifffile pytest pytest-asyncio
fi
# Die Umgebungen (umgebungen/umgebung.{sim-main,vermessung}.json) nennen den Interpreter des Macs; derselbe Pfad zeigt hier auf $PY.
# Der Link gilt dem Verzeichnis, nicht der Datei: Python erkennt ein venv an pyvenv.cfg neben dem aufgerufenen Pfad.
mkdir -p /opt/miniconda3/envs
[ -L /opt/miniconda3/envs/gpu_env ] || rm -rf /opt/miniconda3/envs/gpu_env
ln -sfn "$PY" /opt/miniconda3/envs/gpu_env

# ── Infrastruktur (Postgres, Redis, Consul) ──
echo "== Infrastruktur-Container"
docker compose -f "$WURZEL/docker-compose.infrastructure.yml" -p cqrs up -d --wait

mkdir -p "$LAEUFE/ergebnisse"
echo "== Fertig: $(dotnet --version) · $("$PY/bin/python" -c 'import torch; print("torch", torch.__version__)') · $(docker ps --filter name=cqrs- --format '{{.Names}}' | tr '\n' ' ')"
