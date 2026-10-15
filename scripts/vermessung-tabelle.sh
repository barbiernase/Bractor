#!/usr/bin/env bash
# Die fortlaufende Messtabelle (docs/12 §12.5, docs/plan-garantien-und-durchsatz.md §0.1) — erzeugt aus den Ergebnis-Ordnern, nie von Hand.
#
#   scripts/vermessung-tabelle.sh             druckt die Tabelle (Markdown)
#   scripts/vermessung-tabelle.sh --schreibe  ersetzt sie in docs/12 zwischen den Markern <!-- vermessung-tabelle:anfang/ende -->
#
# Spalten: jeder Ergebnis-Ordner, dessen ergebnis.json eine "spalte" trägt (Baseline, Phase 1, …) und "gueltig": true ist; gibt es zu einer
# Spalte mehrere Läufe, gilt der jüngste (Ordnername = Zeitstempel). Zeilen: die Vereinigung der "zusammenfassung"-Einträge
# ({szenario, kennzahl, einheit, wert}) in der Reihenfolge ihres ersten Auftretens. Die letzte Spalte ist das Verhältnis der jüngsten
# Spalte zur Baseline — bewertet werden Verhältnisse (§0.1). Ungültige Läufe bleiben gespeichert, erscheinen aber nicht als Spalte.
set -euo pipefail

WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
ORDNER="$WURZEL/vermessung/ergebnisse"
DOKU="$WURZEL/docs/12-tests-und-vermessung.md"

# Alle gültigen Läufe mit Spalte, ältester zuerst; je Spalte der jüngste.
laeufe=$(for f in "$ORDNER"/*/ergebnis.json; do
  jq -c --arg ordner "$(basename "$(dirname "$f")")" \
    'select(.spalte != null and .gueltig == true) | {ordner: $ordner, spalte, zusammenfassung}' "$f"
done | jq -sc 'sort_by(.ordner) | group_by(.spalte) | map(last) | sort_by(.ordner)')

tabelle=$(jq -r '
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
' <<<"$laeufe")

if [[ "${1:-}" == "--schreibe" ]]; then
  grep -q '<!-- vermessung-tabelle:anfang -->' "$DOKU" || { echo "Marker fehlt in $DOKU" >&2; exit 1; }
  TABELLE="$tabelle" python3 - "$DOKU" <<'PY'
import os, re, sys
pfad = sys.argv[1]
text = open(pfad, encoding="utf-8").read()
neu = "<!-- vermessung-tabelle:anfang -->\n" + os.environ["TABELLE"] + "\n<!-- vermessung-tabelle:ende -->"
text = re.sub(r"<!-- vermessung-tabelle:anfang -->.*?<!-- vermessung-tabelle:ende -->", lambda _: neu, text, flags=re.S)
open(pfad, "w", encoding="utf-8").write(text)
PY
  echo "Tabelle in $DOKU geschrieben ($(jq length <<<"$laeufe") Spalten)."
else
  echo "$tabelle"
fi
