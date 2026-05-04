#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# cleanup-server.sh — Findet und stoppt alles vom CQRS-Framework
#
# Verwendung:
#   bash deploy/cleanup-server.sh
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo ""
echo "=== CQRS Cleanup ==="
echo ""

FOUND=false

# Prozesse
for PATTERN in "Host.Grpc" "Host.Blazor" "host-grpc" "host-blazor"; do
    PIDS=$(pgrep -f "$PATTERN" 2>/dev/null || true)
    if [ -n "$PIDS" ]; then
        FOUND=true
        echo -e "${RED}$PATTERN läuft: PIDs $PIDS${NC}"
    fi
done

# systemd
for SVC in cqrs-grpc cqrs-blazor; do
    if systemctl is-active --quiet "$SVC" 2>/dev/null; then
        FOUND=true
        echo -e "${RED}systemd $SVC: aktiv${NC}"
    fi
done

# Docker
if command -v docker &>/dev/null; then
    for C in cqrs-postgres cqrs-redis cqrs-consul; do
        if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${C}$"; then
            FOUND=true
            echo -e "${YELLOW}Docker $C: läuft${NC}"
        fi
    done
fi

# Ports
for PORT in 5001 5010; do
    if ss -tlnp "sport = :$PORT" 2>/dev/null | grep -q "$PORT"; then
        FOUND=true
        echo -e "${RED}Port $PORT: belegt${NC}"
    fi
done

if [ "$FOUND" = false ]; then
    echo -e "${GREEN}Nichts läuft. Server ist sauber.${NC}"
    echo ""
    exit 0
fi

echo ""
read -p "Alles stoppen? [j/N] " ANSWER

if [[ "$ANSWER" =~ ^[jJyY]$ ]]; then
    # systemd zuerst
    for SVC in cqrs-blazor cqrs-grpc; do
        sudo systemctl stop "$SVC" 2>/dev/null || true
    done

    # Prozesse
    for PATTERN in "Host.Grpc" "Host.Blazor" "host-grpc" "host-blazor"; do
        pkill -f "$PATTERN" 2>/dev/null || true
    done
    sleep 2
    for PATTERN in "Host.Grpc" "Host.Blazor" "host-grpc" "host-blazor"; do
        pkill -9 -f "$PATTERN" 2>/dev/null || true
    done

    # Docker Infrastruktur
    if command -v docker &>/dev/null; then
        for F in docker-compose.infrastructure.yml ../docker-compose.infrastructure.yml; do
            if [ -f "$F" ]; then
                docker compose -f "$F" down 2>/dev/null || true
                break
            fi
        done
        docker stop cqrs-postgres cqrs-redis cqrs-consul 2>/dev/null || true
        docker rm cqrs-postgres cqrs-redis cqrs-consul 2>/dev/null || true
    fi

    # PID-Dateien
    find /opt/cqrs ~/cqrs ~/deploy 2>/dev/null -name "*.pid" -delete || true

    echo ""
    echo -e "${GREEN}Alles gestoppt.${NC}"
fi

echo ""
