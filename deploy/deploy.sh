#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
GRPC_DIR="/opt/cqrs/grpc"
BLAZOR_DIR="/opt/cqrs/blazor"

echo "=== CQRS Deploy ==="

# --- Native Dependencies prüfen ---
MISSING=""
for pkg in libgdiplus libgtk2.0-0 libavcodec-dev libavformat-dev libswscale-dev libopenexr-dev libdc1394-22-dev; do
    if ! dpkg -s "$pkg" &>/dev/null; then
        MISSING="$MISSING $pkg"
    fi
done
if [ -n "$MISSING" ]; then
    echo ""
    echo "FEHLER: Fehlende System-Packages:$MISSING"
    echo "Bitte einmalig installieren:"
    echo "  sudo apt-get update && sudo apt-get install -y$MISSING"
    echo ""
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

# --- Start Host.Grpc ---
echo "Starting..."
cd "$GRPC_DIR"
Grpc__Port=5001 \
ConnectionStrings__EventStore="Host=localhost;Port=5432;Database=cqrs_events;Username=postgres;Password=postgres" \
Redis__Endpoint="localhost:6379" \
Consul__Address="localhost:8500" \
Pipeline__WatchPath="/data/input" \
Pipeline__PreprocessedPath="/home/wirksam/cqrs-data/preprocessed" \
DOTNET_ENVIRONMENT=Development \
nohup ./Host.Grpc > /opt/cqrs/grpc.log 2>&1 &

echo "  Host.Grpc gestartet (PID $!), warte auf Port 5001..."
for i in $(seq 1 30); do
    if ss -tlnp sport = :5001 2>/dev/null | grep -q 5001; then
        echo "  Host.Grpc bereit"
        break
    fi
    sleep 1
done

# --- Start Host.Blazor ---
cd "$BLAZOR_DIR"
ASPNETCORE_URLS="http://0.0.0.0:5010" \
GrpcServer__Address="http://localhost:5001" \
Pipeline__WatchPath="/data/input" \
Pipeline__PreprocessedPath="/data/preprocessed" \
DOTNET_ENVIRONMENT=Development \
nohup ./Host.Blazor > /opt/cqrs/blazor.log 2>&1 &

echo "  Host.Blazor gestartet (PID $!), warte auf Port 5010..."
for i in $(seq 1 30); do
    if ss -tlnp sport = :5010 2>/dev/null | grep -q 5010; then
        echo "  Host.Blazor bereit"
        break
    fi
    sleep 1
done

echo ""
echo "=== Done ==="
echo "Logs: tail -f /opt/cqrs/grpc.log /opt/cqrs/blazor.log"