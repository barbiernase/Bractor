#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# cleanup-server.sh — Stoppt ALLES was vom CQRS-Framework läuft
#
# Verwendung:
#   ssh wirksam@172.16.5.7
#   bash cleanup-server.sh
#
# Prüft und stoppt:
#   1. Host.Grpc / Host.Blazor Prozesse (nativ)
#   2. systemd Services (falls eingerichtet)
#   3. Docker Container (cqrs-postgres, cqrs-redis, cqrs-consul, etc.)
#   4. PID-Dateien aus start-server.sh
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo ""
echo -e "${CYAN}╔═══════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║  CQRS Framework — Server Cleanup                     ║${NC}"
echo -e "${CYAN}╚═══════════════════════════════════════════════════════╝${NC}"
echo ""

# ═══════════════════════════════════════════════════════
# 1. PROZESSE FINDEN UND ANZEIGEN
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[1/5] Suche laufende Prozesse...${NC}"
echo ""

# Host.Grpc
GRPC_PIDS=$(pgrep -f "Host.Grpc" 2>/dev/null || true)
if [ -n "$GRPC_PIDS" ]; then
    echo -e "  ${RED}Host.Grpc läuft:${NC}"
    ps -p $GRPC_PIDS -o pid,user,start,command --no-headers 2>/dev/null | sed 's/^/    /'
else
    echo -e "  ${GREEN}Host.Grpc: nicht aktiv${NC}"
fi

# Host.Blazor
BLAZOR_PIDS=$(pgrep -f "Host.Blazor" 2>/dev/null || true)
if [ -n "$BLAZOR_PIDS" ]; then
    echo -e "  ${RED}Host.Blazor läuft:${NC}"
    ps -p $BLAZOR_PIDS -o pid,user,start,command --no-headers 2>/dev/null | sed 's/^/    /'
else
    echo -e "  ${GREEN}Host.Blazor: nicht aktiv${NC}"
fi

# host-grpc / host-blazor (umbenannte Binaries aus build.sh)
OTHER_PIDS=$(pgrep -f "host-grpc|host-blazor" 2>/dev/null || true)
if [ -n "$OTHER_PIDS" ]; then
    echo -e "  ${RED}host-grpc/host-blazor läuft:${NC}"
    ps -p $OTHER_PIDS -o pid,user,start,command --no-headers 2>/dev/null | sed 's/^/    /'
fi

# dotnet Prozesse die zum Framework gehören könnten
DOTNET_PIDS=$(pgrep -f "dotnet.*Host\." 2>/dev/null || true)
if [ -n "$DOTNET_PIDS" ]; then
    echo -e "  ${RED}dotnet Host-Prozesse:${NC}"
    ps -p $DOTNET_PIDS -o pid,user,start,command --no-headers 2>/dev/null | sed 's/^/    /'
fi

echo ""

# ═══════════════════════════════════════════════════════
# 2. SYSTEMD SERVICES PRÜFEN
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[2/5] Suche systemd Services...${NC}"
echo ""

for SVC in cqrs-grpc cqrs-blazor cqrs-server; do
    if systemctl is-active --quiet "$SVC" 2>/dev/null; then
        echo -e "  ${RED}$SVC: aktiv${NC}"
        systemctl status "$SVC" --no-pager 2>/dev/null | head -5 | sed 's/^/    /'
    elif systemctl list-unit-files | grep -q "$SVC" 2>/dev/null; then
        echo -e "  ${YELLOW}$SVC: installiert aber nicht aktiv${NC}"
    else
        echo -e "  ${GREEN}$SVC: nicht vorhanden${NC}"
    fi
done

echo ""

# ═══════════════════════════════════════════════════════
# 3. DOCKER CONTAINER PRÜFEN
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[3/5] Suche Docker Container...${NC}"
echo ""

if command -v docker &>/dev/null; then
    CQRS_CONTAINERS=$(docker ps -a --filter "name=cqrs-" --format "{{.Names}}\t{{.Status}}" 2>/dev/null || true)
    if [ -n "$CQRS_CONTAINERS" ]; then
        echo -e "  ${RED}CQRS Docker Container gefunden:${NC}"
        echo "$CQRS_CONTAINERS" | while read line; do
            echo -e "    $line"
        done
    else
        echo -e "  ${GREEN}Keine CQRS Container${NC}"
    fi

    # Auch nach anderen relevanten Containern suchen
    OTHER_CONTAINERS=$(docker ps -a --filter "name=postgres" --filter "name=redis" --filter "name=consul" --format "{{.Names}}\t{{.Status}}" 2>/dev/null || true)
    if [ -n "$OTHER_CONTAINERS" ]; then
        echo -e "  ${YELLOW}Infrastruktur-Container:${NC}"
        echo "$OTHER_CONTAINERS" | while read line; do
            echo -e "    $line"
        done
    fi
else
    echo -e "  ${YELLOW}Docker nicht installiert${NC}"
fi

echo ""

# ═══════════════════════════════════════════════════════
# 4. PID-DATEIEN UND DEPLOYMENT-ORDNER PRÜFEN
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[4/5] Suche Deployment-Artefakte...${NC}"
echo ""

for DIR in /opt/cqrs ~/cqrs ~/deploy ~/deploy-linux ~/deploy-windows; do
    if [ -d "$DIR" ]; then
        echo -e "  ${YELLOW}Gefunden: $DIR${NC}"
        ls -la "$DIR" 2>/dev/null | head -10 | sed 's/^/    /'

        # PID-Dateien
        for PID_FILE in "$DIR"/*.pid; do
            if [ -f "$PID_FILE" ]; then
                PID=$(cat "$PID_FILE" 2>/dev/null)
                if kill -0 "$PID" 2>/dev/null; then
                    echo -e "    ${RED}PID-Datei $PID_FILE → Prozess $PID läuft!${NC}"
                else
                    echo -e "    ${YELLOW}PID-Datei $PID_FILE → Prozess $PID nicht mehr aktiv${NC}"
                fi
            fi
        done
        echo ""
    fi
done

# ═══════════════════════════════════════════════════════
# 5. PORTS PRÜFEN
# ═══════════════════════════════════════════════════════

echo -e "${YELLOW}[5/5] Prüfe relevante Ports...${NC}"
echo ""

for PORT in 5001 5010 5432 6379 8500; do
    LISTENER=$(ss -tlnp "sport = :$PORT" 2>/dev/null | grep -v "^State" || true)
    if [ -n "$LISTENER" ]; then
        echo -e "  ${RED}Port $PORT belegt:${NC}"
        echo "$LISTENER" | sed 's/^/    /'
    else
        echo -e "  ${GREEN}Port $PORT: frei${NC}"
    fi
done

echo ""

# ═══════════════════════════════════════════════════════
# ZUSAMMENFASSUNG + STOP-OPTIONEN
# ═══════════════════════════════════════════════════════

ALL_PIDS="$GRPC_PIDS $BLAZOR_PIDS $OTHER_PIDS $DOTNET_PIDS"
ALL_PIDS=$(echo "$ALL_PIDS" | xargs)  # trim

HAS_CONTAINERS=false
if command -v docker &>/dev/null && docker ps -q --filter "name=cqrs-" 2>/dev/null | grep -q .; then
    HAS_CONTAINERS=true
fi

if [ -z "$ALL_PIDS" ] && [ "$HAS_CONTAINERS" = false ]; then
    echo -e "${GREEN}═══════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}  Alles sauber! Keine CQRS-Prozesse oder Container aktiv.${NC}"
    echo -e "${GREEN}═══════════════════════════════════════════════════════${NC}"
    echo ""
    exit 0
fi

echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}  Was soll gestoppt werden?${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo ""
echo "  1) Alles stoppen (Prozesse + Docker)"
echo "  2) Nur Prozesse stoppen (Host.Grpc, Host.Blazor)"
echo "  3) Nur Docker stoppen"
echo "  4) Nichts tun (nur anzeigen)"
echo ""
read -p "  Auswahl [1-4]: " CHOICE

case "$CHOICE" in
    1)
        echo ""
        echo -e "${YELLOW}Stoppe alles...${NC}"

        # Prozesse
        if [ -n "$ALL_PIDS" ]; then
            for PID in $ALL_PIDS; do
                echo -e "  Stoppe PID $PID..."
                kill "$PID" 2>/dev/null || true
            done
            sleep 2
            # Hartnäckige Prozesse
            for PID in $ALL_PIDS; do
                if kill -0 "$PID" 2>/dev/null; then
                    echo -e "  ${YELLOW}Force-kill PID $PID${NC}"
                    kill -9 "$PID" 2>/dev/null || true
                fi
            done
        fi

        # systemd
        for SVC in cqrs-grpc cqrs-blazor cqrs-server; do
            if systemctl is-active --quiet "$SVC" 2>/dev/null; then
                echo -e "  Stoppe systemd $SVC..."
                sudo systemctl stop "$SVC" 2>/dev/null || true
            fi
        done

        # Docker
        if command -v docker &>/dev/null; then
            echo -e "  Stoppe Docker Container..."
            # docker-compose.infrastructure.yml suchen
            for DIR in /opt/cqrs ~/cqrs ~/deploy ~/deploy-linux .; do
                if [ -f "$DIR/docker-compose.infrastructure.yml" ]; then
                    docker compose -f "$DIR/docker-compose.infrastructure.yml" down 2>/dev/null || true
                    break
                fi
            done
            # Fallback: einzeln stoppen
            docker stop cqrs-postgres cqrs-redis cqrs-consul 2>/dev/null || true
            docker rm cqrs-postgres cqrs-redis cqrs-consul 2>/dev/null || true
        fi

        # PID-Dateien aufräumen
        find /opt/cqrs ~/cqrs ~/deploy ~/deploy-linux 2>/dev/null -name "*.pid" -delete || true

        echo ""
        echo -e "${GREEN}  ✔ Alles gestoppt${NC}"
        ;;

    2)
        echo ""
        if [ -n "$ALL_PIDS" ]; then
            for PID in $ALL_PIDS; do
                echo -e "  Stoppe PID $PID..."
                kill "$PID" 2>/dev/null || true
            done
            sleep 2
            for PID in $ALL_PIDS; do
                if kill -0 "$PID" 2>/dev/null; then
                    kill -9 "$PID" 2>/dev/null || true
                fi
            done
            echo -e "${GREEN}  ✔ Prozesse gestoppt${NC}"
        else
            echo -e "${GREEN}  Keine Prozesse zu stoppen${NC}"
        fi
        ;;

    3)
        echo ""
        if command -v docker &>/dev/null; then
            for DIR in /opt/cqrs ~/cqrs ~/deploy ~/deploy-linux .; do
                if [ -f "$DIR/docker-compose.infrastructure.yml" ]; then
                    docker compose -f "$DIR/docker-compose.infrastructure.yml" down 2>/dev/null || true
                    break
                fi
            done
            docker stop cqrs-postgres cqrs-redis cqrs-consul 2>/dev/null || true
            docker rm cqrs-postgres cqrs-redis cqrs-consul 2>/dev/null || true
            echo -e "${GREEN}  ✔ Docker Container gestoppt${NC}"
        fi
        ;;

    *)
        echo -e "  Nichts geändert."
        ;;
esac

echo ""