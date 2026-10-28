#!/usr/bin/env bash
# Die Test-Stufen (docs/12 §12.6) — gedacht für den Messserver: scripts/server.sh lauf scripts/tests.sh <stufe>
#
#   scripts/tests.sh schnell            je Teilschritt: Build, Prüfstand, Integration ohne Kategorie „Last“, Prüfdomäne, Verhaltens-Netz (≈ 8 min)
#   scripts/tests.sh voll               Abnahme: dazu „Last“, Codegen-Drift, Client, Python-SDK und Worker, --check, --sonde, --grammatik, Gate
#   scripts/tests.sh suite <Projekt> [Filter]   eine Suite, optional mit dotnet-test-Filter (z. B. "FullyQualifiedName~WeckerGarantieTests")
#
# Die Suiten laufen nacheinander (Integration, Prüfdomäne und Verhaltens-Netz teilen Postgres und Consul, docs/12 §12.7). Je Suite eine Zeile mit
# Ergebnis und Dauer; bei Fehlern die Namen und Meldungen der roten Tests. Exit-Code ≠ 0, wenn eine Stufe rot war.
set -uo pipefail
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
PY=/opt/miniconda3/envs/gpu_env/bin/python
ROT=0
ZEILEN=()

zeit() { date +%s; }

suite() {   # suite <Projekt> [Filter]
  local p=$1 filter=${2:-} t0 aus
  t0=$(zeit)
  aus=$(dotnet test "$p/$p.csproj" --no-build ${filter:+--filter "$filter"} --logger "console;verbosity=normal" 2>&1)
  local summe; summe=$(grep -E "^(Passed|Failed)!" <<<"$aus" | tail -1 | sed -E 's/ - [^ ]+\.dll.*//')
  [ -z "$summe" ] && summe="kein Ergebnis (Build? Filter?)"
  ZEILEN+=("$(printf '%-42s %4ss  %s' "$p${filter:+ [$filter]}" "$(( $(zeit)-t0 ))" "$summe")")
  if ! grep -q "^Passed!" <<<"$aus"; then
    ROT=1
    echo "── rot: $p"
    grep -E "^\s+Failed [A-Za-z]|Error Message:" -A3 <<<"$aus" | grep -vE "^\s+at |^--$" | cut -c1-300 | head -40
  fi
}

schritt() {  # schritt <Name> <Befehl …>: Ergebnis = Exit-Code
  local name=$1; shift; local t0; t0=$(zeit)
  local aus; aus=$("$@" 2>&1); local rc=$?
  ZEILEN+=("$(printf '%-42s %4ss  %s' "$name" "$(( $(zeit)-t0 ))" "$([ $rc -eq 0 ] && echo grün || echo "ROT (Exit $rc)")")")
  if [ $rc -ne 0 ]; then ROT=1; echo "── rot: $name"; tail -15 <<<"$aus"; fi
}

bauen() {
  local t0 fehler; t0=$(zeit)
  fehler=$(dotnet build -v q 2>&1 | grep -E " error " | sort -u)
  ZEILEN+=("$(printf '%-42s %4ss  %s' "Build" "$(( $(zeit)-t0 ))" "$([ -z "$fehler" ] && echo grün || echo ROT)")")
  if [ -n "$fehler" ]; then ROT=1; echo "── rot: Build"; head -10 <<<"$fehler"; fi
}

case "${1:-}" in
  schnell)
    bauen
    suite Infrastructure.Pruefstand.Tests
    suite Infrastructure.Integration.Tests "Kategorie!=Last"
    suite Pruefdomaene.Integration.Tests
    suite Verhalten.Tests
    ;;
  voll)
    bauen
    schritt "Codegen-Drift" ./codegen.sh --check
    suite Infrastructure.Pruefstand.Tests
    suite Infrastructure.Integration.Tests
    suite Pruefdomaene.Integration.Tests
    suite Verhalten.Tests
    suite Client.Infrastructure.CollectionTests
    schritt "Python-SDK (pytest)" bash -c "cd Client.Infrastructure.Python && $PY -m pytest -q"
    schritt "Python-Worker (pytest)" bash -c "cd Domain.Client.Worker.Python.ML && $PY -m pytest -q"
    schritt "GraphExtractor --check" dotnet run --project GraphExtractor --no-build -- --check
    schritt "GraphExtractor --sonde" dotnet run --project GraphExtractor --no-build -- --sonde
    schritt "GraphExtractor --grammatik" dotnet run --project GraphExtractor --no-build -- --grammatik
    schritt "Gate frischer Checkout" ./pruefe-checkout.sh
    ;;
  suite)
    [ -n "${2:-}" ] || { echo "Aufruf: scripts/tests.sh suite <Projekt> [Filter]" >&2; exit 2; }
    bauen
    suite "$2" "${3:-}"
    ;;
  *)
    sed -n '2,9p' "$0"; exit 2 ;;
esac

echo
echo "== Stufe ${1}: $([ $ROT -eq 0 ] && echo GRÜN || echo ROT)"
printf '%s\n' "${ZEILEN[@]}"
exit $ROT
