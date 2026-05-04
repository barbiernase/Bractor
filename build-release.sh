#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# build-release.sh — Erzeugt Deployment-Paket für Linux x86_64
#
# Läuft auf macOS (M4/ARM) und erzeugt linux-x64 Binaries.
# Ergebnis: deploy/ Ordner mit allem was auf den Server muss.
#
# Verwendung:
#   ./build-release.sh
#   scp -r deploy/ user@server:/opt/cqrs/
#
# Auf dem Server:
#   cd /opt/cqrs
#   ./start-server.sh
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

RUNTIME="linux-x64"
CONFIG="Release"
OUTPUT="deploy"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  Build für $RUNTIME"
echo "═══════════════════════════════════════════════════════"
echo ""

# Altes Output löschen
rm -rf "$OUTPUT"
mkdir -p "$OUTPUT"

# ─── Host.Grpc ───

echo "▶ Building Host.Grpc..."
dotnet publish Host.Grpc/Host.Grpc.csproj \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUTPUT/grpc"

# Umbenennen für konsistente Benennung
mv "$OUTPUT/grpc/Host.Grpc" "$OUTPUT/grpc/host-grpc"

echo "  ✓ host-grpc"

# ─── Host.Blazor ───

echo "▶ Building Host.Blazor..."
dotnet publish Host.Blazor/Host.Blazor.csproj \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUTPUT/blazor"

mv "$OUTPUT/blazor/Host.Blazor" "$OUTPUT/blazor/host-blazor"

echo "  ✓ host-blazor"

# ─── Konfiguration + Infrastruktur ───

echo "▶ Konfiguration kopieren..."

# Docker Compose für Infrastruktur
cp docker-compose.infrastructure.yml "$OUTPUT/"

# Start-Skript für den Server
cp start-server.sh "$OUTPUT/"
chmod +x "$OUTPUT/start-server.sh"

echo "  ✓ Konfiguration"

# ─── Zusammenfassung ───

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  Build fertig!"
echo "═══════════════════════════════════════════════════════"
echo ""
echo "  deploy/"
echo "  ├── grpc/"
echo "  │   ├── host-grpc                (EXE)"
echo "  │   ├── appsettings.json         (Defaults)"
echo "  │   └── *.so                     (native libs)"
echo "  ├── blazor/"
echo "  │   ├── host-blazor              (EXE)"
echo "  │   ├── appsettings.json         (Defaults)"
echo "  │   └── wwwroot/                 (statische Assets)"
echo "  ├── docker-compose.infrastructure.yml"
echo "  └── start-server.sh              (setzt Env-Variablen)"
echo ""
echo "  Nächste Schritte:"
echo "    scp -r deploy/ user@server:/opt/cqrs/"
echo "    ssh user@server 'cd /opt/cqrs && ./start-server.sh'"
echo ""
