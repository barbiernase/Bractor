#!/usr/bin/env bash
# REPO-PFAD: deploy.sh  (MODIFIZIERT)
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
GRPC_DIR="/opt/cqrs/grpc"
BLAZOR_DIR="/opt/cqrs/blazor"
PROTO_DIR="/opt/cqrs/proto"

echo "=== CQRS Deploy ==="

# --- Native Dependencies prüfen ---
MISSING=""
for pkg in libgdiplus libgtk2.0-0 libavcodec-dev libavformat-dev libswscale-dev libopenexr-dev libdc1394-22-dev; do
    if ! dpkg -s "$pkg" &>/dev/null; then
        MISSING="$MISSING $pkg"
    fi
done
if [ -n "$MISSING" ]; then
    echo "FEHLER: Fehlende System-Packages:$MISSING"
    echo "  sudo apt-get update && sudo apt-get install -y$MISSING"
    exit 1
fi

# --- Stop ---
echo "Stopping..."
pkill -f "Host.Grpc" 2>/dev/null && echo "  Host.Grpc gestoppt" || echo "  Host.Grpc war nicht aktiv"
pkill -f "Host.Blazor" 2>/dev/null && echo "  Host.Blazor gestoppt" || echo "  Host.Blazor war nicht aktiv"
sleep 2

# --- Build ---
echo "Building Host.Grpc..."
~/.dotnet/dotnet publish "$REPO_DIR/Host.Grpc/Host.Grpc.csproj" \
    -c Release -r linux-x64 --self-contained -o "$GRPC_DIR"

echo "Building Host.Blazor..."
~/.dotnet/dotnet publish "$REPO_DIR/Host.Blazor/Host.Blazor.csproj" \
    -c Release -r linux-x64 --self-contained -o "$BLAZOR_DIR"

# --- Proto-Distribution (NEU) ---
echo "Proto-Distribution..."
mkdir -p "$PROTO_DIR"
PROTO_SOURCE=$(find "$REPO_DIR" -path "*/ProtoRepo/*" -name "domain.proto" -type f | head -1)
if [ -n "$PROTO_SOURCE" ]; then
    cp "$PROTO_SOURCE" "$PROTO_DIR/domain.proto"
    HASH=$(sha256sum "$PROTO_DIR/domain.proto" | cut -d' ' -f1)
    echo "$HASH" > "$PROTO_DIR/domain.proto.sha256"
    echo "  ✓ Proto → $PROTO_DIR/ (Hash: ${HASH:0:16}...)"
else
    echo "  ⚠ domain.proto nicht gefunden"
fi

# --- Python-Typen generieren (NEU) ---
WORKER_DIR="$REPO_DIR/Domain.Client.Worker.Python.ML"
INFRA_DIR="$REPO_DIR/Client.Infrastructure.Python"
if [ -d "$WORKER_DIR" ] && command -v python3 &>/dev/null; then
    echo "Python-Typen generieren..."
    "$WORKER_DIR/build_python.sh" || echo "  ⚠ Python-Generierung fehlgeschlagen (nicht kritisch)"
fi

# --- Python auf GPU-Maschine deployen (NEU, optional) ---
GPU_HOST="${GPU_HOST:-}"
if [ -n "$GPU_HOST" ]; then
    echo "Python-Client deployen → $GPU_HOST..."
    rsync -az --delete \
        "$INFRA_DIR/cqrs_client/" "$GPU_HOST:/opt/cqrs/python/cqrs_client/"
    rsync -az \
        "$WORKER_DIR/domain_client/" "$WORKER_DIR/run.py" \
        "$GPU_HOST:/opt/cqrs/python/"
    echo "  ✓ Python-Client deployed"
else
    echo "  (GPU_HOST nicht gesetzt — Python-Deploy übersprungen)"
fi

# --- Port 80 ohne Root ---
sudo setcap 'cap_net_bind_service=+ep' "$BLAZOR_DIR/Host.Blazor"

# --- Start Host.Grpc ---
echo "Starting..."
cd "$GRPC_DIR"
Grpc__Port=5001 \
ConnectionStrings__EventStore="Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres" \
Redis__Endpoint="localhost:6379" \
Consul__Address="localhost:8500" \
Pipeline__WatchPath="/mnt/unlabeledgewebebilder/SNP/IO" \
Pipeline__PreprocessedPath="/home/wirksam/cqrs-data/preprocessed" \
DOTNET_ENVIRONMENT=Development \
nohup ./Host.Grpc > /opt/cqrs/grpc.log 2>&1 &

echo "  Host.Grpc gestartet (PID $!), warte auf Port 5001..."
for i in $(seq 1 30); do
    ss -tlnp sport = :5001 2>/dev/null | grep -q 5001 && echo "  Host.Grpc bereit" && break
    sleep 1
done

# --- Start Host.Blazor ---
cd "$BLAZOR_DIR"
ASPNETCORE_URLS="http://0.0.0.0:80" \
Blazor__Urls="http://0.0.0.0:80" \
GrpcServer__Address="http://localhost:5001" \
Pipeline__WatchPath="/mnt/unlabeledgewebebilder/SNP/IO" \
Pipeline__PreprocessedPath="/home/wirksam/cqrs-data/preprocessed" \
DOTNET_ENVIRONMENT=Development \
nohup ./Host.Blazor > /opt/cqrs/blazor.log 2>&1 &

echo "  Host.Blazor gestartet (PID $!), warte auf Port 80..."
for i in $(seq 1 30); do
    ss -tlnp sport = :80 2>/dev/null | grep -q 80 && echo "  Host.Blazor bereit" && break
    sleep 1
done

echo ""
echo "=== Done ==="
echo "Logs:   tail -f /opt/cqrs/grpc.log /opt/cqrs/blazor.log"
