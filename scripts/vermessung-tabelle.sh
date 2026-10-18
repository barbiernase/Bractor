#!/usr/bin/env bash
# Die fortlaufenden Messtabellen (docs/12 §12.5, docs/plan-garantien-und-durchsatz.md §0.1) — erzeugt aus den Ergebnis-Ordnern, nie von Hand.
#
#   scripts/vermessung-tabelle.sh             druckt die Tabellen (Markdown)
#   scripts/vermessung-tabelle.sh --schreibe  ersetzt sie in docs/12 zwischen den Markern <!-- vermessung-tabelle:<rechner>:anfang/ende -->
#
# Je Rechner eine Tabelle mit eigener Baseline — Werte verschiedener Rechner sind nicht vergleichbar. Zu welcher Tabelle ein Lauf gehört, sagt
# das Feld "rechner" seiner ergebnis.json (Muster in TABELLEN).
# Spalten: jeder Ergebnis-Ordner, dessen ergebnis.json eine "spalte" trägt (Baseline, Phase 1, …) und "gueltig": true ist; gibt es zu einer
# Spalte mehrere Läufe, gilt der jüngste (Ordnername = Zeitstempel). Zeilen: die Vereinigung der "zusammenfassung"-Einträge
# ({szenario, kennzahl, einheit, wert}) in der Reihenfolge ihres ersten Auftretens. Die letzte Spalte ist das Verhältnis der jüngsten
# Spalte zur Baseline — bewertet werden Verhältnisse (§0.1). Ungültige Läufe bleiben gespeichert, erscheinen aber nicht als Spalte.
set -euo pipefail
shopt -s nullglob

WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
ORDNER="$WURZEL/vermessung/ergebnisse"
DOKU="$WURZEL/docs/12-tests-und-vermessung.md"

# Tabelle → Muster auf "rechner" (jq-Regex).
TABELLEN=("mac:Darwin" "messserver:Ubuntu")

# Alle gültigen Läufe mit Spalte eines Rechners, ältester zuerst; je Spalte der jüngste.
laeufe() {
  for f in "$ORDNER"/*/ergebnis.json; do
    jq -c --arg ordner "$(basename "$(dirname "$f")")" --arg muster "$1" \
      'select(.spalte != null and .gueltig == true and (.rechner | test($muster))) | {ordner: $ordner, spalte, zusammenfassung}' "$f"
  done | jq -sc 'sort_by(.ordner) | group_by(.spalte) | map(last) | sort_by(.ordner)'
}

tabelle() {
  jq -r '
  def zahl: if . == null then "–" elif (type == "number") then (if . >= 100 then (. | round | tostring) else ((. * 10 | round) / 10 | tostring) end) else tostring end;
  . as $l
  | ($l | map(.zusammenfassung[] | {szenario, kennzahl, einheit}) | reduce .[] as $z ([]; if any(.[]; . == $z) then . else . + [$z] end)) as $zeilen
  | ($l | map(select(.spalte == "Baseline")) | first) as $basis
  | ($l | last) as $letzte
  | "| Szenario | Kennzahl | " + ($l | map("\(.spalte) ([\(.ordner)](../vermessung/ergebnisse/\(.ordner)/))") | join(" | "))
      + (if $basis != null and ($l | length) > 1 then " | \($letzte.spalte) / Baseline |" else " |" end),
    "|---|---|" + ($l | map("---:") | join("|")) + (if $basis != null and ($l | length) > 1 then "|---:|" else "|" end),
    ($zeilen[] as $z
      | ($l | map([.zusammenfassung[] | select(.szenario == $z.szenario and .kennzahl == $z.kennzahl) | .wert] | first)) as $werte
      | "| \($z.szenario) | \($z.kennzahl) (\($z.einheit)) | " + ($werte | map(zahl) | join(" | "))
        + (if $basis != null and ($l | length) > 1 then
             ([$basis.zusammenfassung[] | select(.szenario == $z.szenario and .kennzahl == $z.kennzahl) | .wert] | first) as $b
             | ($werte | last) as $n
             | " | " + (if ($b == null or $n == null or $b == 0) then "–" else "\((($n / $b) * 100 | round) / 100)×" end) + " |"
           else " |" end))
  ' <<<"$1"
}

for t in "${TABELLEN[@]}"; do
  name="${t%%:*}"
  l="$(laeufe "${t#*:}")"
  if [ "$(jq length <<<"$l")" -eq 0 ]; then tab="(noch keine Spalte)"; else tab="$(tabelle "$l")"; fi
  if [[ "${1:-}" == "--schreibe" ]]; then
    grep -q "<!-- vermessung-tabelle:$name:anfang -->" "$DOKU" || { echo "Marker vermessung-tabelle:$name fehlt in $DOKU" >&2; exit 1; }
    NAME="$name" TABELLE="$tab" python3 - "$DOKU" <<'EOF'
import os, re, sys
pfad, name = sys.argv[1], os.environ["NAME"]
text = open(pfad, encoding="utf-8").read()
neu = f"<!-- vermessung-tabelle:{name}:anfang -->\n" + os.environ["TABELLE"] + f"\n<!-- vermessung-tabelle:{name}:ende -->"
text = re.sub(rf"<!-- vermessung-tabelle:{name}:anfang -->.*?<!-- vermessung-tabelle:{name}:ende -->", lambda _: neu, text, flags=re.S)
open(pfad, "w", encoding="utf-8").write(text)
EOF
    echo "Tabelle $name in $DOKU geschrieben ($(jq length <<<"$l") Spalten)."
  else
    printf '## %s\n%s\n\n' "$name" "$tab"
  fi
done
