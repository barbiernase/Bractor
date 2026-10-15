#!/usr/bin/env bash
# E2E-Messung (docs/plan-garantien-und-durchsatz.md §0.1/§0.2): ein Befehl, feste Parameter, echte Hosts, echte Eingänge, Exactly-once-Prüfung.
#
#   scripts/vermessung-e2e.sh --anlass baseline --spalte Baseline                     Höchstlast S1–S5 (≈ 5 min)
#   scripts/vermessung-e2e.sh --anlass phase-1 --spalte "Phase 1"                     dieselbe Messung nach einer Phase
#   scripts/vermessung-e2e.sh --art stoerung --anlass baseline-stoerung               Störungslauf (≈ 3 min)
#   scripts/vermessung-e2e.sh --anlass probe --probe                                   Funktionstest des Treibers: verkürzt, kein
#                                                                                      Messlauf — Ergebnis im Temp-Verzeichnis, nicht im Repo
#
# Ergebnis: vermessung/ergebnisse/<datum>_<commit>_<anlass>/ — mit committen; danach scripts/vermessung-tabelle.sh --schreibe.
# Nie gleichzeitig mit den Integrations-Suiten oder der Werkbank-Abnahme laufen lassen (docs/12 §12.7).
set -euo pipefail

WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$WURZEL"

# ── Infrastruktur: Postgres mit pg_stat_statements, eigener Redis für die Vermessung (der Störungslauf stoppt ihn) ──
docker start cqrs-postgres >/dev/null
until docker exec cqrs-postgres pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done
if ! docker exec cqrs-postgres psql -U postgres -tAc "show shared_preload_libraries" | grep -q pg_stat_statements; then
  echo "pg_stat_statements einschalten (ALTER SYSTEM, Neustart von cqrs-postgres) …"
  docker exec cqrs-postgres psql -U postgres -qc "ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements'"
  docker restart cqrs-postgres >/dev/null
  until docker exec cqrs-postgres pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done
fi
if [ "$(docker exec cqrs-postgres psql -U postgres -tAc "show max_connections")" -lt 300 ]; then
  echo "max_connections auf 300 (ALTER SYSTEM, Neustart von cqrs-postgres) …"
  docker exec cqrs-postgres psql -U postgres -qc "ALTER SYSTEM SET max_connections = 300"
  docker restart cqrs-postgres >/dev/null
  until docker exec cqrs-postgres pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done
fi
if ! docker ps -a --format '{{.Names}}' | grep -qx cqrs-vermessung-redis; then
  docker run -d --name cqrs-vermessung-redis -p 127.0.0.1:6390:6379 --restart unless-stopped redis:7-alpine >/dev/null
fi
docker start cqrs-vermessung-redis >/dev/null

# ── Release-Build der Hosts und des Treibers (der Debug-Build der Werkbank bleibt unberührt) ──
for p in Host.Grpc Host.Pruefdomaene Cqrs.Vermessung; do
  if ! ausgabe=$(dotnet build "$p/$p.csproj" -c Release -nologo -v q -nodeReuse:false 2>&1); then
    echo "$ausgabe" | grep -E "error" | sort -u; echo "Build von $p fehlgeschlagen." >&2; exit 1
  fi
done

exec dotnet "Cqrs.Vermessung/bin/Release/net9.0/Cqrs.Vermessung.dll" "$@"
