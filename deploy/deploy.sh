#!/usr/bin/env bash
# deploy.sh — Baut und deployed Host.Grpc + Host.Blazor
# Wird vom GitHub Actions Runner oder manuell aufgerufen.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
GRPC_DIR="/opt/cqrs/grpc"
BLAZOR_DIR="/opt/cqrs/blazor"

echo "=== CQRS Deploy ==="
echo "Repo: $REPO_DIR"

# --- Dependencies (einmalig) ---
if ! dpkg -s libgdiplus &>/dev/null; then
    echo "Installiere libgdiplus..."
    sudo apt-get update -qq && sudo apt-get install -y -qq libgdiplus
fi

# --- Verzeichnisse ---
for DIR in "$GRPC_DIR" "$BLAZOR_DIR" /data/input /data/preprocessed; do
    sudo mkdir -p "$DIR"
    sudo chown wirksam:wirksam "$DIR"
done

# --- Build ---
echo "Building Host.Grpc..."
dotnet publish "$REPO_DIR/Host.Grpc/Host.Grpc.csproj" \
    -c Release -r linux-x64 --self-contained -o "$GRPC_DIR"

echo "Building Host.Blazor..."
dotnet publish "$REPO_DIR/Host.Blazor/Host.Blazor.csproj" \
    -c Release -r linux-x64 --self-contained -o "$BLAZOR_DIR"

# --- systemd Services (erstellt sie falls nötig) ---
if [ ! -f /etc/systemd/system/cqrs-grpc.service ]; then
    echo "Erstelle systemd Services..."

    sudo tee /etc/systemd/system/cqrs-grpc.service > /dev/null <<EOF
[Unit]
Description=CQRS Host.Grpc
After=network.target docker.service

[Service]
Type=simple
User=wirksam
WorkingDirectory=$GRPC_DIR
ExecStart=$GRPC_DIR/Host.Grpc
Restart=on-failure
RestartSec=5
Environment=DOTNET_ENVIRONMENT=Production
Environment=Grpc__Port=5001
Environment=ConnectionStrings__EventStore=Host=localhost;Port=5432;Database=cqrs;Username=postgres;Password=postgres
Environment=Redis__Endpoint=localhost:6379
Environment=Consul__Address=http://localhost:8500
Environment=Pipeline__WatchPath=/data/input
Environment=Pipeline__PreprocessedPath=/data/preprocessed

[Install]
WantedBy=multi-user.target
EOF

    sudo tee /etc/systemd/system/cqrs-blazor.service > /dev/null <<EOF
[Unit]
Description=CQRS Host.Blazor
After=network.target cqrs-grpc.service

[Service]
Type=simple
User=wirksam
WorkingDirectory=$BLAZOR_DIR
ExecStart=$BLAZOR_DIR/Host.Blazor
Restart=on-failure
RestartSec=5
Environment=DOTNET_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5010
Environment=GrpcServer__Address=http://localhost:5001
Environment=Pipeline__WatchPath=/data/input
Environment=Pipeline__PreprocessedPath=/data/preprocessed

[Install]
WantedBy=multi-user.target
EOF

    sudo systemctl daemon-reload
    sudo systemctl enable cqrs-grpc cqrs-blazor
fi

# --- Restart ---
echo "Restarting services..."
sudo systemctl restart cqrs-grpc
sleep 3
sudo systemctl restart cqrs-blazor
sleep 2

# --- Health Check ---
echo ""
echo "=== Status ==="
systemctl is-active cqrs-grpc && echo "cqrs-grpc: OK" || echo "cqrs-grpc: FAILED"
systemctl is-active cqrs-blazor && echo "cqrs-blazor: OK" || echo "cqrs-blazor: FAILED"
ss -tlnp sport = :5001 sport = :5010 2>/dev/null || true
echo "=== Done ==="
