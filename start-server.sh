#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# start-server.sh — Startet alle Services auf dem Linux-Server
#
# Voraussetzungen:
#   - Docker + Docker Compose installiert
#   - deploy/ Ordner mit grpc/ und blazor/ Unterordnern
#   - /data/input Verzeichnis existiert (oder WATCH_PATH anpassen)
#
# Verwendung:
#   cd /opt/cqrs
#   ./start-server.sh          # Alles starten
#   ./start-server.sh stop     # Alles stoppen
#   ./start-server.sh status   # Status prüfen
#
# Konfiguration:
#   Die wenigen Werte die sich vom Entwicklungs-Default unterscheiden
#   werden hier als Umgebungsvariablen gesetzt. ASP.NET Core liest
#   sie automatisch (__ statt : als Trenner).
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# ─── Konfiguration (hier anpassen!) ───

WATCH_PATH="${WATCH_PATH:-/data/input}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-postgres}"

case "${1:-start}" in

  start)
    echo ""
    echo "═══ 1. Infrastruktur (Docker) ═══"
    docker compose -f docker-compose.infrastructure.yml up -d

    echo ""
    echo "═══ 2. Warte auf Infrastruktur... ═══"
    echo -n "  PostgreSQL..."
    until docker exec cqrs-postgres pg_isready -U postgres -q 2>/dev/null; do
      sleep 1; echo -n "."
    done
    echo " ✓"

    echo -n "  Redis..."
    until docker exec cqrs-redis redis-cli ping 2>/dev/null | grep -q PONG; do
      sleep 1; echo -n "."
    done
    echo " ✓"

    echo -n "  Consul..."
    until docker exec cqrs-consul consul members 2>/dev/null | grep -q alive; do
      sleep 1; echo -n "."
    done
    echo " ✓"

    mkdir -p logs "$WATCH_PATH"

    echo ""
    echo "═══ 3. Host.Grpc starten ═══"

    # Nur die Werte die vom Entwicklungs-Default abweichen:
    #   - WatchPath: /data/input statt /Users/tobi/...
    #   - DB-Passwort: aus Variable statt hardcoded
    cd "$SCRIPT_DIR/grpc"
    Pipeline__WatchPath="$WATCH_PATH" \
    ConnectionStrings__EventStore="Host=localhost;Database=cqrs_events;Username=postgres;Password=$POSTGRES_PASSWORD" \
      ./host-grpc > "$SCRIPT_DIR/logs/grpc.log" 2>&1 &
    echo $! > "$SCRIPT_DIR/grpc.pid"
    echo "  PID: $(cat "$SCRIPT_DIR/grpc.pid")"
    cd "$SCRIPT_DIR"

    echo -n "  Warte auf gRPC..."
    for i in $(seq 1 30); do
      if ss -tlnp 2>/dev/null | grep -q ":5001"; then
        echo " ✓"
        break
      fi
      sleep 1; echo -n "."
    done

    echo ""
    echo "═══ 4. Host.Blazor starten ═══"

    # Nur die Werte die abweichen:
    #   - Blazor__Urls: 0.0.0.0 statt localhost (von außen erreichbar)
    #   - WatchPath: für den File-Endpunkt
    cd "$SCRIPT_DIR/blazor"
    Blazor__Urls="http://0.0.0.0:5010" \
    Pipeline__WatchPath="$WATCH_PATH" \
      ./host-blazor > "$SCRIPT_DIR/logs/blazor.log" 2>&1 &
    echo $! > "$SCRIPT_DIR/blazor.pid"
    echo "  PID: $(cat "$SCRIPT_DIR/blazor.pid")"
    cd "$SCRIPT_DIR"

    echo ""
    echo "═══════════════════════════════════════════════════════"
    echo "  Alles gestartet!"
    echo "  Blazor UI:  http://$(hostname):5010"
    echo "  gRPC:       http://$(hostname):5001"
    echo "  WatchPath:  $WATCH_PATH"
    echo "  Logs:       tail -f logs/grpc.log logs/blazor.log"
    echo "═══════════════════════════════════════════════════════"
    echo ""
    ;;

  stop)
    echo "Stoppe Host-Prozesse..."
    [ -f grpc.pid ]   && kill "$(cat grpc.pid)"   2>/dev/null && rm grpc.pid   && echo "  ✓ host-grpc"
    [ -f blazor.pid ] && kill "$(cat blazor.pid)" 2>/dev/null && rm blazor.pid && echo "  ✓ host-blazor"

    echo "Stoppe Infrastruktur..."
    docker compose -f docker-compose.infrastructure.yml down
    echo "  ✓ Docker-Container gestoppt"
    ;;

  status)
    echo ""
    echo "═══ Prozesse ═══"
    if [ -f grpc.pid ] && kill -0 "$(cat grpc.pid)" 2>/dev/null; then
      echo "  host-grpc:   running (PID $(cat grpc.pid))"
    else
      echo "  host-grpc:   stopped"
    fi
    if [ -f blazor.pid ] && kill -0 "$(cat blazor.pid)" 2>/dev/null; then
      echo "  host-blazor: running (PID $(cat blazor.pid))"
    else
      echo "  host-blazor: stopped"
    fi
    echo ""
    echo "═══ Docker ═══"
    docker compose -f docker-compose.infrastructure.yml ps
    ;;

  *)
    echo "Verwendung: $0 {start|stop|status}"
    exit 1
    ;;
esac
