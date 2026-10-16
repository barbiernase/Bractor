#!/usr/bin/env bash
# Die Test-Suite (docs/12 §12.1): zwei Ebenen, ein Befehl je Ebene. Die dritte Ebene (Ende zu Ende) ist scripts/vermessung-e2e.sh.
#
#   ./pruefe.sh schnell   Ebene 1, store-frei (≈ 2 min): ignorierte Quellen, Build, Codegen-Drift, der Build verändert nichts,
#                         Editor-Parität (--check), Agnostik-Sonde (--sonde), Prüfstand, Python (SDK, Worker)
#   ./pruefe.sh alles     Ebene 1, dann nacheinander gegen echtes Marten (≈ 6 min): Integration, Prüfdomäne, Werkbank-Abnahme
#
# Läuft im Arbeitsbaum, wie er ist. Vor dem Push auf dem Messserver: scripts/server.sh lauf ./pruefe.sh alles — der Server checkt den gepushten
# Commit frisch aus, so prüft der Lauf genau das, was ein Clone sieht. Infrastruktur (Postgres, Redis, Consul) muss laufen
# (docker compose -f docker-compose.infrastructure.yml up -d); nie gleichzeitig mit einer Messung (docs/12 §12.6).
# Exit-Code 0 = alles grün; sonst rot, die letzten Zeilen des roten Schritts stehen in der Ausgabe.
set -uo pipefail
export MSBUILDDISABLENODEREUSE=1   # wiederverwendete MSBuild-Knoten erben die Ausgabe und hielten eine Pipe offen
WURZEL="$(cd "$(dirname "$0")" && pwd)"
cd "$WURZEL"

EBENE="${1:-}"
[[ "$EBENE" == schnell || "$EBENE" == alles ]] || { sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

ROT=0
LOGS="$(mktemp -d "${TMPDIR:-/tmp}/pruefe.XXXXXX")"
schritt() {   # schritt <Name> <Befehl…> — rot merken, weitermachen
  local name="$1"; shift
  local beginn=$SECONDS
  printf '▶ %-22s' "$name"
  if "$@" >"$LOGS/$name.log" 2>&1; then
    printf '✅ %4d s  %s\n' $((SECONDS - beginn)) "$(grep -E '^(Passed|Failed|Bestanden|Fehler)!|[0-9]+ passed' "$LOGS/$name.log" | tail -1 | cut -c1-90)"
  else
    ROT=1
    printf '❌ %4d s\n' $((SECONDS - beginn))
    tail -n 30 "$LOGS/$name.log" | sed 's/^/     /'
  fi
}
test_projekt() { dotnet test "$1/$1.csproj" --no-build -nologo; }

# ── Ignorierte Quellen: lokal vorhanden, aber von .gitignore geschluckt — fehlen in jedem Checkout ──────────────────────
ignorierte_quellen() {
  local ausgaben='(^|/)(bin|obj|__pycache__|\.venv|\.pytest_cache|TestResults|\.idea|\.vs|node_modules|\.werkbank|\.llm-kontext)/|^deploy-(linux|windows)/|(^|/)\.DS_Store$|\.pyc$|\.user$|\.zip$'
  local bewusst='^(cqrs\.proto|Domain/cqrs\.proto|domain-model\.json|knowledge-graph\.json|\.claude/settings\.local\.json|dump\.rdb)$'
  local quellen='\.(cs|razor|cshtml|csproj|props|targets|sln|proto|py|json|ya?ml|sh|ps1|js|mjs|css|html|md|txt|toml|cfg|ini|env)$|(^|/)Dockerfile$'
  local treffer
  treffer="$(git ls-files --others --ignored --exclude-standard | grep -Ev "$ausgaben" | grep -Ev "$bewusst" | grep -E "$quellen" || true)"
  [[ -z "$treffer" ]] && return 0
  while IFS= read -r datei; do git check-ignore -v -- "$datei"; done <<< "$treffer"
  return 1
}

# ── Der Build verändert den Checkout nicht (Generat-Drift, versehentliche Erzeugnisse) ───────────────────────────────
VORHER="$(git status --porcelain)"
unveraendert() {
  local nachher; nachher="$(git status --porcelain)"
  [[ "$nachher" == "$VORHER" ]] && return 0
  diff <(echo "$VORHER") <(echo "$nachher"); return 1
}

# ── Python der Umgebung (docs/12 §12.5): Umgebung:Python aus sim-main, sonst CQRS_PYTHON, sonst python3 ──────────────
PY="$(python3 -c 'import json,os,re; p=json.load(open("umgebungen/umgebung.sim-main.json"))["Umgebung"].get("Python",""); print(re.sub(r"\$\{env:(\w+)\}", lambda m: os.environ.get(m.group(1),""), p))' 2>/dev/null)"
[[ -x "$PY" ]] || PY="${CQRS_PYTHON:-python3}"
pytest_in() { (cd "$1" && "$PY" -m pytest -q); }

echo "== Ebene 1 · schnell (Commit $(git rev-parse --short HEAD)$([[ -n "$VORHER" ]] && echo ", Arbeitsbaum geändert"))"
schritt ignorierte-quellen ignorierte_quellen
schritt build dotnet build CqrsSolution.sln -nologo -v:q
schritt codegen ./codegen.sh --check
schritt checkout-unveraendert unveraendert
schritt editor-paritaet dotnet run --project GraphExtractor --no-build -- --check
schritt agnostik-sonde dotnet run --project GraphExtractor --no-build -- --sonde
schritt pruefstand test_projekt Infrastructure.Pruefstand.Tests
schritt python-sdk pytest_in Client.Infrastructure.Python
schritt python-worker pytest_in Domain.Client.Worker.Python.ML

if [[ "$EBENE" == alles ]]; then
  echo "== Ebene 2 · echtes Marten (nacheinander)"
  schritt integration test_projekt Infrastructure.Integration.Tests
  schritt pruefdomaene test_projekt Pruefdomaene.Integration.Tests
  schritt werkbank-abnahme test_projekt Werkbank.Abnahme.Tests
fi

echo
if [[ $ROT -eq 0 ]]; then echo "✅ grün ($EBENE, $SECONDS s)"; rm -rf "$LOGS"; else echo "❌ rot ($EBENE, $SECONDS s) — Logs: $LOGS" >&2; fi
exit $ROT
