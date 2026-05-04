#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# build.sh — Erzeugt Deployment-Paket
#
# Läuft auf macOS (M4/ARM) und cross-compiled für das Zielsystem.
# Alle Deployment-Konfiguration wird hier gesetzt — einmal anpassen,
# dann bauen und deployen.
#
# Verwendung:
#   ./build.sh windows    → deploy-windows/  (zum Testen auf dem PC)
#   ./build.sh linux      → deploy-linux/    (für den Server)
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

# ╔═══════════════════════════════════════════════════════════════╗
# ║  DEPLOYMENT-KONFIGURATION — HIER ANPASSEN!                   ║
# ╚═══════════════════════════════════════════════════════════════╝

# ─── Ports ───
GRPC_PORT=5001
BLAZOR_PORT=5010

# ─── Pfade ───
# Ordner in dem der FileWatcher neue Dateien erkennt (kann Netzwerk-Mount sein)
GRPC_WATCH_PATH="/home/wirksam/cqrs-data/input"
# Ordner für vorverarbeitete Bilder (Resize, Histogramm) — IMMER lokal!
# Nicht auf den Netzwerk-Mount schreiben.
GRPC_PREPROCESSED_PATH="/home/wirksam/cqrs-data/preprocessed"

# ─── Blazor → gRPC Verbindung ───
# Adresse unter der Host.Blazor den Host.Grpc erreicht.
# Selbe Maschine = localhost, sonst IP/Hostname des gRPC-Servers.
GRPC_SERVER_ADDRESS="http://localhost:${GRPC_PORT}"

# ─── PostgreSQL ───
POSTGRES_HOST="localhost"
POSTGRES_PORT=5432
POSTGRES_DB="cqrs_events"
POSTGRES_USER="postgres"
POSTGRES_PASSWORD="postgres"
POSTGRES_SCHEMA="es"

# ─── Redis ───
REDIS_ENDPOINT="localhost:6379"
REDIS_DATABASE=1

# ─── Consul (Proto.Actor Cluster Discovery) ───
CONSUL_ADDRESS="localhost:8500"

# ─── Proto.Actor Cluster ───
CLUSTER_NAME="cqrs-cluster"
# Hostname/IP unter der andere Cluster-Nodes diesen Node erreichen.
# Single-Node = localhost, Multi-Node = echte IP/Hostname.
CLUSTER_ADVERTISED_HOST="localhost"

# ╔═══════════════════════════════════════════════════════════════╗
# ║  AB HIER NICHTS MEHR ÄNDERN                                  ║
# ╚═══════════════════════════════════════════════════════════════╝

TARGET="${1:-}"
if [[ "$TARGET" != "windows" && "$TARGET" != "linux" ]]; then
    echo ""
    echo "Verwendung: ./build.sh <target>"
    echo ""
    echo "  ./build.sh windows    Build für Windows x64 (Test)"
    echo "  ./build.sh linux      Build für Linux x64 (Server)"
    echo ""
    exit 1
fi

if [[ "$TARGET" == "windows" ]]; then
    RUNTIME="win-x64"
    OUTPUT="deploy-windows"
else
    RUNTIME="linux-x64"
    OUTPUT="deploy-linux"
fi

CONFIG="Release"
POSTGRES_CONN="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  Build für $RUNTIME → $OUTPUT/"
echo "═══════════════════════════════════════════════════════"
echo ""

rm -rf "$OUTPUT"
mkdir -p "$OUTPUT/grpc" "$OUTPUT/blazor"

# ─── Host.Grpc ───
# KEIN PublishSingleFile! Marten kompiliert zur Laufzeit C#-Code
# und braucht Zugriff auf die DLLs (Roslyn findet sie sonst nicht).

echo "▶ Building Host.Grpc..."
dotnet publish Host.Grpc/Host.Grpc.csproj \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained \
    -o "$OUTPUT/grpc"

# OpenCvSharp: native Library manuell kopieren.
# dotnet publish erkennt ubuntu-spezifische RIDs nicht als kompatibel
# mit linux-x64 und lässt die .so liegen.
if [[ "$TARGET" == "linux" ]]; then
    echo "  Suche OpenCvSharp native library..."
    OPENCV_SO=$(find ~/.nuget/packages -path "*ubuntu*" -name "libOpenCvSharpExtern.so" 2>/dev/null | head -1)
    if [[ -n "$OPENCV_SO" ]]; then
        cp "$OPENCV_SO" "$OUTPUT/grpc/"
        echo "  ✓ libOpenCvSharpExtern.so kopiert"
    else
        echo "  ⚠ libOpenCvSharpExtern.so nicht gefunden! OpenCvSharp wird auf dem Server nicht funktionieren."
    fi
fi

echo "  ✓ Host.Grpc"

# ─── Host.Blazor ───

echo "▶ Building Host.Blazor..."
dotnet publish Host.Blazor/Host.Blazor.csproj \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUTPUT/blazor"

echo "  ✓ Host.Blazor"

# ─── appsettings schreiben ───

echo "▶ Schreibe appsettings..."

cat > "$OUTPUT/grpc/appsettings.json" << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Grpc": {
    "Port": ${GRPC_PORT}
  },
  "ConnectionStrings": {
    "EventStore": "${POSTGRES_CONN}"
  },
  "EventStore": {
    "Schema": "${POSTGRES_SCHEMA}"
  },
  "Redis": {
    "Endpoint": "${REDIS_ENDPOINT}",
    "Database": ${REDIS_DATABASE}
  },
  "Consul": {
    "Address": "${CONSUL_ADDRESS}"
  },
  "Cluster": {
    "Name": "${CLUSTER_NAME}",
    "AdvertisedHost": "${CLUSTER_ADVERTISED_HOST}"
  },
  "Pipeline": {
    "WatchPath": "${GRPC_WATCH_PATH}",
    "PreprocessedPath": "${GRPC_PREPROCESSED_PATH}"
  }
}
EOF
echo "  ✓ grpc/appsettings.json"

cat > "$OUTPUT/blazor/appsettings.json" << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Blazor": {
    "Urls": "http://0.0.0.0:${BLAZOR_PORT}"
  },
  "GrpcServer": {
    "Address": "${GRPC_SERVER_ADDRESS}"
  },
  "Pipeline": {
    "WatchPath": "${GRPC_WATCH_PATH}",
    "PreprocessedPath": "${GRPC_PREPROCESSED_PATH}"
  }
}
EOF
echo "  ✓ blazor/appsettings.json"

# ─── Deployment-Dateien ───

echo "▶ Deployment-Dateien kopieren..."
cp docker-compose.infrastructure.yml "$OUTPUT/"

if [[ "$TARGET" == "windows" ]]; then
    cp start.ps1 "$OUTPUT/"
    echo "  ✓ start.ps1"
else
    cp start-server.sh "$OUTPUT/"
    chmod +x "$OUTPUT/start-server.sh"
    echo "  ✓ start-server.sh"
fi

# ─── Zusammenfassung ───

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  ✓ Build fertig: $OUTPUT/"
echo "═══════════════════════════════════════════════════════"
echo ""
echo "  Konfiguration:"
echo "    gRPC Port:      ${GRPC_PORT}"
echo "    Blazor Port:    ${BLAZOR_PORT}"
echo "    WatchPath:      ${GRPC_WATCH_PATH}"
echo "    Preprocessed:   ${GRPC_PREPROCESSED_PATH}"
echo "    PostgreSQL:     ${POSTGRES_HOST}:${POSTGRES_PORT}/${POSTGRES_DB}"
echo "    Redis:          ${REDIS_ENDPOINT}"
echo "    Consul:         ${CONSUL_ADDRESS}"
echo ""

if [[ "$TARGET" == "windows" ]]; then
    echo "  Nächste Schritte:"
    echo "    1. Ordner $OUTPUT/ auf den Windows-PC kopieren"
    echo "    2. PowerShell öffnen, in den Ordner wechseln"
    echo "    3. ./start.ps1"
else
    echo "  Nächste Schritte:"
    echo "    scp -r $OUTPUT/ user@server:~/cqrs/"
    echo "    ssh user@server 'cd ~/cqrs && ./start-server.sh'"
fi
echo ""
