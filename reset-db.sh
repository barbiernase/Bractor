#!/bin/bash
# ═══════════════════════════════════════════════════════════
# reset-db.sh — Setzt die komplette CQRS-Datenbank zurück
#
# Löscht: PostgreSQL (EventStore + ReadModels)
#         Consul KV-Store (Cluster-State)
#
# Voraussetzung: Docker-Container laufen (cqrs-postgres, consul)
# ═══════════════════════════════════════════════════════════

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo ""
echo "╔═══════════════════════════════════════════════════════╗"
echo "║  CQRS Database Reset                                 ║"
echo "╚═══════════════════════════════════════════════════════╝"
echo ""

# ─── 1. PostgreSQL: Datenbank droppen und neu erstellen ───

echo -e "${YELLOW}[1/2] PostgreSQL — cqrs_events droppen + neu erstellen...${NC}"

docker exec cqrs-postgres psql -U postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'cqrs_events' AND pid <> pg_backend_pid();" \
  > /dev/null 2>&1 || true

docker exec cqrs-postgres psql -U postgres -c "DROP DATABASE IF EXISTS cqrs_events;" 2>/dev/null
docker exec cqrs-postgres psql -U postgres -c "CREATE DATABASE cqrs_events;" 2>/dev/null

echo -e "${GREEN}  ✔ PostgreSQL: cqrs_events neu erstellt${NC}"

# ─── 2. Consul: KV-Store leeren ───

echo -e "${YELLOW}[2/2] Consul — KV-Store leeren...${NC}"

docker exec consul consul kv delete -recurse "" > /dev/null 2>&1 || true

echo -e "${GREEN}  ✔ Consul: KV-Store geleert${NC}"

# ─── Fertig ───

echo ""
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}  Alles zurückgesetzt. Server neu starten.${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo ""
