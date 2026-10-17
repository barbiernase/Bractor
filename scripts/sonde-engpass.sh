#!/usr/bin/env bash
# Engpass-Sonde (docs/12 §12.5 „Engpass-Sonden“): der Messtreiber läuft unverändert, daneben schreibt scripts/sonde-engpass.py CPU je Prozess
# und Kern, Host-Threads, pg_stat_activity alle 100 ms, WAL und dotnet-counters mit; die Auswertung je Abschnitt landet im Ergebnis-Ordner
# unter sonde/. Optional genau ein Knopf — eine Diagnose, kein Messlauf (ohne --spalte, erscheint in keiner Tabelle).
#
#   scripts/sonde-engpass.sh --anlass server-sonde-a1 [--knopf <k>] [--szenarien S3,S5] [--wiederholungen 3]
#
# Knöpfe (wirken nur auf den Checkout des Messservers, der vor jedem Lauf gesäubert wird; knopf.txt hält die Änderung fest):
#   keiner            Stand des Commits
#   sync-aus          synchronous_commit = off für den ganzen Postgres (ALTER SYSTEM, danach zurückgesetzt) — Obergrenze für das fsync
#   drossel-64        EmitDrossel.StandardJeZiel 8 → 64 (Sends je Ziel-Aggregat gleichzeitig unterwegs)
#   auto-prepare-20   Npgsql „Max Auto Prepare=20“ in den Verbindungen beider Mess-Umgebungen (Statements je Verbindung vorbereiten)
#   drain-8           Schreibpfad.Drain 4 → 8 (parallele Commit-Schleifen des Group-Commits) in beiden Mess-Umgebungen
#
# Nur auf dem Messserver (Linux, /proc, dotnet-counters): scripts/server.sh lauf scripts/sonde-engpass.sh --anlass … --knopf …
set -euo pipefail

WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$WURZEL"

ANLASS="" KNOPF=keiner SZENARIEN=S3,S5 WIEDERHOLUNGEN=3
while [ $# -gt 0 ]; do
  case "$1" in
    --anlass) ANLASS="$2"; shift 2 ;;
    --knopf) KNOPF="$2"; shift 2 ;;
    --szenarien) SZENARIEN="$2"; shift 2 ;;
    --wiederholungen) WIEDERHOLUNGEN="$2"; shift 2 ;;
    *) echo "unbekannte Option $1" >&2; exit 2 ;;
  esac
done
[ -n "$ANLASS" ] || { echo "--anlass fehlt" >&2; exit 2; }
[ "$(uname)" = Linux ] || { echo "Nur auf dem Messserver (scripts/server.sh lauf …): der Knopf ändert den Checkout." >&2; exit 2; }

psql_c() { docker exec cqrs-postgres psql -U postgres -tAqc "$1"; }
sync_zuruecksetzen() { psql_c "ALTER SYSTEM RESET synchronous_commit" >/dev/null; psql_c "SELECT pg_reload_conf()" >/dev/null; }
umgebung_setzen() {   # $1 = Python-Ausdruck über u (das geladene JSON) — für beide Mess-Umgebungen
  for f in umgebungen/umgebung.vermessung.json umgebungen/umgebung.vermessung-pruefdomaene.json; do
    python3 - "$f" "$1" <<'PY'
import json, sys
p, ausdruck = sys.argv[1], sys.argv[2]
u = json.load(open(p))
exec(ausdruck)
json.dump(u, open(p, "w"), indent=2, ensure_ascii=False)
PY
  done
}

docker start cqrs-postgres >/dev/null
until docker exec cqrs-postgres pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done
# Ein abgebrochener Lauf darf keinen Knopf im Postgres hinterlassen.
sync_zuruecksetzen
GEAENDERT=()
trap 'sync_zuruecksetzen; [ ${#GEAENDERT[@]} -eq 0 ] || git checkout -q -- "${GEAENDERT[@]}"' EXIT

case "$KNOPF" in
  keiner) ;;
  sync-aus) psql_c "ALTER SYSTEM SET synchronous_commit = off" >/dev/null; psql_c "SELECT pg_reload_conf()" >/dev/null ;;
  drossel-64)
    grep -q 'public const int StandardJeZiel = 8;' Infrastructure/PubSub/CommandEmitter.cs || { echo "Drossel-Konstante nicht gefunden" >&2; exit 1; }
    GEAENDERT=(Infrastructure/PubSub/CommandEmitter.cs)
    sed -i 's/public const int StandardJeZiel = 8;/public const int StandardJeZiel = 64;/' Infrastructure/PubSub/CommandEmitter.cs ;;
  auto-prepare-20) GEAENDERT=(umgebungen/); umgebung_setzen 'u["EventStore"]["Verbindung"] += ";Max Auto Prepare=20"' ;;
  drain-8) GEAENDERT=(umgebungen/); umgebung_setzen 'u.setdefault("Schreibpfad", {})["Drain"] = 8' ;;
  *) echo "unbekannter Knopf $KNOPF" >&2; exit 2 ;;
esac

SONDE="$(mktemp -d)"
{
  echo "$KNOPF"
  echo "synchronous_commit = $(psql_c 'show synchronous_commit')"
  git diff
} > "$SONDE/knopf.txt"
echo "== Sonde: Knopf $KNOPF, Rohdaten in $SONDE"

vorher="$(ls vermessung/ergebnisse/ 2>/dev/null | sort)"
python3 scripts/sonde-engpass.py --aus "$SONDE" &
SONDE_PID=$!
set +e
scripts/vermessung-e2e.sh --anlass "$ANLASS" --szenarien "$SZENARIEN" --wiederholungen "$WIEDERHOLUNGEN"
rc=$?
set -e
kill -TERM "$SONDE_PID" 2>/dev/null || true
wait "$SONDE_PID" 2>/dev/null || true

neu="$(comm -13 <(echo "$vorher") <(ls vermessung/ergebnisse/ | sort) | tail -1)"
if [ -z "$neu" ]; then echo "Kein neuer Ergebnis-Ordner — Rohdaten bleiben in $SONDE" >&2; exit 1; fi
ziel="vermessung/ergebnisse/$neu/sonde"
mkdir -p "$ziel"
cp "$SONDE"/* "$ziel"/
python3 scripts/sonde-auswertung.py "vermessung/ergebnisse/$neu" || echo "Auswertung fehlgeschlagen (Rohdaten liegen in $ziel)" >&2
gzip -9 "$ziel"/*.csv
rm -rf "$SONDE"
exit $rc
