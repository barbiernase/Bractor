#!/usr/bin/env bash
# REPO-PFAD: pruefe-checkout.sh
# Gate „frischer Checkout baut“ (docs/13 WB-3, docs/12 §12.1): baut den aktuellen Commit so, wie ihn ein frischer Clone sieht.
#
#   1. Ignorierte Quellen: Dateien, die lokal liegen, aber von .gitignore geschluckt werden (fehlen im Checkout).
#   2. In einem temporären `git worktree` (nur committete Dateien): `dotnet build`, `./codegen.sh --check`, Prüfstand.
#   3. Der Build darf im Checkout nichts verändern (`git status` danach leer).
#   Der Worktree wird am Ende immer entfernt.
#
# Nutzung:
#   ./pruefe-checkout.sh                 # Commit HEAD, Worktree unter $TMPDIR
#   ./pruefe-checkout.sh --verz <dir>    # Worktree unter <dir> anlegen (z. B. ein Scratch-Verzeichnis)
#   ./pruefe-checkout.sh --ohne-tests    # nur Ignore-Prüfung, Build und Drift
# Exit-Code 0 = grün; sonst rot (die Meldungen nennen den Grund). Gedacht vor jedem Push.
set -euo pipefail

# Keine wiederverwendeten MSBuild-Knoten: sie erben die Ausgabe und hielten eine Pipe (z. B. `| tail`) bis zu ihrem Leerlauf-Ende offen.
export MSBUILDDISABLENODEREUSE=1

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

BASIS=""
MIT_TESTS=1
while [[ $# -gt 0 ]]; do
  case "$1" in
    --verz) BASIS="$2"; shift 2 ;;
    --ohne-tests) MIT_TESTS=0; shift ;;
    *) echo "Unbekannte Option: $1" >&2; exit 64 ;;
  esac
done

COMMIT="$(git rev-parse HEAD)"
KURZ="$(git rev-parse --short HEAD)"
ROT=0

if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
  echo "ℹ Nicht committete Änderungen werden NICHT geprüft — das Gate baut den Commit $KURZ."
fi

# ── 1. Ignorierte Quellen ────────────────────────────────────────────────────────────────────────────────────────
# Lokal vorhandene, nicht verfolgte Dateien, die eine Ignore-Regel schluckt. Ausgenommen: Build- und Cache-Ausgaben (deploy-linux/,
# deploy-windows/ schreibt build.sh aus den Vorlagen im Wurzelverzeichnis) und die bewusst ignorierten Einzeldateien (Altdateien und
# lokale Erzeugnisse, siehe .gitignore „Einzeldateien“).
AUSGABEN='(^|/)(bin|obj|__pycache__|\.venv|\.pytest_cache|TestResults|\.idea|\.vs|node_modules|\.werkbank|\.llm-kontext)/|^deploy-(linux|windows)/|(^|/)\.DS_Store$|\.pyc$|\.user$|\.zip$'
BEWUSST='^(cqrs\.proto|Domain/cqrs\.proto|domain-model\.json|knowledge-graph\.json|\.claude/settings\.local\.json|dump\.rdb)$'
QUELLEN='\.(cs|razor|cshtml|csproj|props|targets|sln|proto|py|json|ya?ml|sh|ps1|js|mjs|css|html|md|txt|toml|cfg|ini|env)$|(^|/)Dockerfile$'

echo "── Ignorierte Quellen (git check-ignore) ──"
TREFFER="$(git ls-files --others --ignored --exclude-standard \
  | grep -Ev "$AUSGABEN" | grep -Ev "$BEWUSST" | grep -E "$QUELLEN" || true)"
if [[ -n "$TREFFER" ]]; then
  ROT=1
  echo "❌ Diese Dateien liegen lokal, fehlen aber in jedem Checkout (Regel → Datei):"
  # check-ignore -v nennt die Regel, die greift (Datei:Zeile:Muster).
  while IFS= read -r datei; do
    git check-ignore -v -- "$datei" | sed 's/^/   /'
  done <<< "$TREFFER"
else
  echo "✅ keine"
fi

# ── 2. Frischer Checkout ─────────────────────────────────────────────────────────────────────────────────────────
if [[ -z "$BASIS" ]]; then BASIS="${TMPDIR:-/tmp}"; fi
mkdir -p "$BASIS"
WT="$(mktemp -d "$BASIS/checkout-$KURZ.XXXXXX")"
aufraeumen() {
  git -C "$SCRIPT_DIR" worktree remove --force "$WT" >/dev/null 2>&1 || true
  rm -rf "$WT"
  git -C "$SCRIPT_DIR" worktree prune
}
trap aufraeumen EXIT

echo "── Frischer Checkout $KURZ in $WT ──"
git worktree add --detach "$WT" "$COMMIT" >/dev/null

schritt() {   # schritt <Name> <Befehl…> — läuft im Worktree; rot merken, weitermachen
  local name="$1"; shift
  local log="$WT/.pruefe-checkout-$name.log"
  echo "▶ $name"
  if (cd "$WT" && "$@") >"$log" 2>&1; then
    echo "  ✅ $name"
  else
    ROT=1
    echo "  ❌ $name — letzte Zeilen:"
    tail -n 30 "$log" | sed 's/^/     /'
  fi
}

schritt build dotnet build CqrsSolution.sln -nologo -v:q
schritt codegen ./codegen.sh --check
if [[ $MIT_TESTS -eq 1 ]]; then
  schritt pruefstand dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj --no-build -nologo -v:q
fi

# ── 3. Der Build verändert den Checkout nicht ────────────────────────────────────────────────────────────────────
# Geänderte verfolgte Dateien = Generat-Drift; neue, nicht ignorierte Dateien = Erzeugnis, das versehentlich ins Repo käme.
echo "── Checkout nach dem Build ──"
VERAENDERT="$(cd "$WT" && git status --porcelain | grep -v ' \.pruefe-checkout-' || true)"
if [[ -n "$VERAENDERT" ]]; then
  ROT=1
  echo "❌ Der Build hat den Checkout verändert:"
  echo "$VERAENDERT" | sed 's/^/   /'
else
  echo "✅ unverändert"
fi

echo
if [[ $ROT -eq 0 ]]; then
  echo "✅ Gate grün: Commit $KURZ baut im frischen Checkout."
else
  echo "❌ Gate rot: Commit $KURZ (Gründe oben)." >&2
fi
exit $ROT
