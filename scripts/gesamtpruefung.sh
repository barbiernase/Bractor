#!/usr/bin/env bash
# Die Gesamtprüfung (docs/12 §12.6): Korrektheit, Geschwindigkeit, Störung und Profil eines Commits in einem Lauf, mit einem Bericht —
# für Meilensteine (vor einem Merge, nach einem Umbau), nicht für jeden Commit (≈ 45–50 min). Nur auf dem Messserver:
#
#   scripts/server.sh lauf scripts/gesamtpruefung.sh [--vergleich <ergebnis-ordner>] [--spalte <name>] [--ohne-ab]
#
#   1 Korrektheit  scripts/tests.sh voll; jeder rote Test wird bis zu zweimal einzeln wiederholt → „rot“ oder „wackelt“ (mit bekannter Nummer)
#   2 Messung      scripts/vermessung-e2e.sh (S1–S6, Leerlauf) mit Exactly-once; mit --spalte erscheint sie in der Tabelle von docs/12
#   3 Störung      derselbe Treiber mit harten Abbrüchen: Exactly-once und Vollständigkeit danach
#   4 Profil       scripts/sonde-engpass.sh über S1, S5, S6 mit CPU-Profilen der Abschnitte S1, S5-64 und S6-64
#   Bericht        <Messung>/gesamtpruefung.md: Ampel je Teil, jede Kennzahl gegen den Vergleich (Rausch-Bänder in scripts/gesamtpruefung.py),
#                  Profile; ist ein Szenario auffällig, misst es A gegen B nach (A = Commit des Vergleichs im Worktree /opt/bractor-a)
#
# Vergleich ohne Angabe: die letzte frühere Gesamtprüfung, sonst die Spalte „Phase 6“.
set -uo pipefail
WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$WURZEL"
ERG=vermessung/ergebnisse
VERGLEICH="" SPALTE="" AB=1
while [ $# -gt 0 ]; do
  case "$1" in
    --vergleich) VERGLEICH="$2"; shift 2 ;;
    --spalte) SPALTE="$2"; shift 2 ;;
    --ohne-ab) AB=0; shift ;;
    *) echo "unbekannte Option $1" >&2; exit 2 ;;
  esac
done
[ "$(uname)" = Linux ] || { echo "Nur auf dem Messserver (scripts/server.sh lauf …)." >&2; exit 2; }

T0=$(date +%s)
LOGS="$(mktemp -d)"
neuer_ordner() { comm -13 <(echo "$1") <(ls "$ERG" | sort) | tail -1; }
if [ -z "$VERGLEICH" ]; then
  VERGLEICH="$(ls "$ERG" | grep -E '_gesamtpruefung$' | sort | tail -1)"
  [ -n "$VERGLEICH" ] || VERGLEICH="$(ls "$ERG" | grep -E '_server-pubsub-phase6$' | sort | tail -1)"
fi
[ -z "$VERGLEICH" ] || VERGLEICH="$ERG/$(basename "$VERGLEICH")"
echo "== Gesamtprüfung $(git rev-parse --short HEAD), Vergleich: ${VERGLEICH:-keiner}"

echo "== 1/4 Korrektheit"
scripts/tests.sh voll > "$LOGS/korrektheit.log" 2>&1
sed -n '/^== Stufe/,$p' "$LOGS/korrektheit.log"
python3 scripts/gesamtpruefung.py korrektheit "$LOGS/korrektheit.log" > "$LOGS/korrektheit.json"

echo "== 2/4 Messung"
vorher="$(ls "$ERG" | sort)"
scripts/vermessung-e2e.sh --anlass gesamtpruefung ${SPALTE:+--spalte "$SPALTE"}
MESSUNG="$ERG/$(neuer_ordner "$vorher")"

echo "== 3/4 Störung"
vorher="$(ls "$ERG" | sort)"
scripts/vermessung-e2e.sh --art stoerung --anlass gesamtpruefung-stoerung
STOERUNG="$(neuer_ordner "$vorher")"; STOERUNG="${STOERUNG:+$ERG/$STOERUNG}"

echo "== 4/4 Profil"
vorher="$(ls "$ERG" | sort)"
scripts/sonde-engpass.sh --anlass gesamtpruefung-profil --szenarien S1,S5,S6 --wiederholungen 1 --profil S1-w1,S5-64-w1,S6-64-w1
PROFIL="$(neuer_ordner "$vorher")"; PROFIL="${PROFIL:+$ERG/$PROFIL}"

python3 scripts/gesamtpruefung.py bericht --korrektheit "$LOGS/korrektheit.json" --messung "$MESSUNG" --stoerung "$STOERUNG" --profil "$PROFIL" \
  --vergleich "$VERGLEICH" --dauer "$(( $(date +%s) - T0 ))"
cp "$LOGS/korrektheit.json" "$MESSUNG/korrektheit.json"

# Auffällige Szenarien gegen den Commit des Vergleichs nachmessen (nur die, die es dort gibt).
AUFFAELLIG="$(paste -sd, "$MESSUNG/auffaellig.txt" 2>/dev/null | sed 's/-[0-9]*//g' | tr ',' '\n' | sort -u | paste -sd, -)"
if [ "$AB" -eq 1 ] && [ -n "$AUFFAELLIG" ] && [ -n "$VERGLEICH" ]; then
  REF="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]+'/ergebnis.json'))['commit'])" "$VERGLEICH")"
  VORHANDEN="$(python3 -c "import json,sys; print(','.join(sorted({k.split('-')[0] for k in json.load(open(sys.argv[1]+'/ergebnis.json'))['szenarien']})))" "$VERGLEICH")"
  SZ="$(comm -12 <(tr ',' '\n' <<<"$AUFFAELLIG" | grep -E '^S' | sort) <(tr ',' '\n' <<<"$VORHANDEN" | sort) | paste -sd, -)"
  if [ -n "$SZ" ]; then
    echo "== A/B-Nachmessung $SZ: A = $REF, B = $(git rev-parse --short HEAD)"
    A=/opt/bractor-a
    if [ -d "$A/.git" ] || [ -f "$A/.git" ]; then git -C "$A" fetch -q origin && git -C "$A" checkout -q --detach "$REF" && git -C "$A" clean -qfdx -e vermessung/ergebnisse
    else git clone -q "$(git remote get-url origin)" "$A" && git -C "$A" checkout -q --detach "$REF"; fi
    vorher_a="$(ls "$A/$ERG" | sort)"
    (cd "$A" && scripts/vermessung-e2e.sh --anlass gesamtpruefung-ab-a --szenarien "$SZ")
    NEU_A="$(comm -13 <(echo "$vorher_a") <(ls "$A/$ERG" | sort) | tail -1)"
    [ -n "$NEU_A" ] && cp -r "$A/$ERG/$NEU_A" "$ERG/"
    vorher="$(ls "$ERG" | sort)"
    scripts/vermessung-e2e.sh --anlass gesamtpruefung-ab-b --szenarien "$SZ"
    NEU_B="$(neuer_ordner "$vorher")"
    [ -n "$NEU_A" ] && [ -n "$NEU_B" ] && python3 scripts/gesamtpruefung.py ab --a "$ERG/$NEU_A" --b "$ERG/$NEU_B" --messung "$MESSUNG"
  else
    echo "== Auffällig nur in Szenarien ohne Vergleich (${AUFFAELLIG}) — keine A/B-Nachmessung"
  fi
fi
rm -rf "$LOGS"
echo "== Bericht: $MESSUNG/gesamtpruefung.md · $(( ($(date +%s) - T0) / 60 )) min"
