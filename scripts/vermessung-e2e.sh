#!/usr/bin/env bash
# Ebene 3 der Test-Suite — Ende zu Ende (docs/12 §12.4): ein Befehl, feste Parameter, echte Hosts, echte Eingänge, Exactly-once-Prüfung.
#
#   scripts/vermessung-e2e.sh --anlass server-leistung              Leistungsmessung (≈ 7 min)
#   scripts/vermessung-e2e.sh --stoerung --anlass server-stoerung    Störungslauf (≈ 4 min)
#
# Gemessen wird auf dem Messserver: scripts/server.sh lauf scripts/vermessung-e2e.sh … (der Lauf holt den Ergebnis-Ordner zurück).
# Ergebnis: vermessung/ergebnisse/<datum>_<commit>_<anlass>/ — mit committen; danach scripts/vermessung-tabelle.sh --schreibe.
# Nie gleichzeitig mit ./pruefe.sh alles oder einer anderen Messung laufen lassen (docs/12 §12.6).
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
