#!/usr/bin/env bash
# REPO-PFAD: codegen.sh
# Vereinter Codegen-Prepass: erzeugt VOR dem Compile die Artefakte, die ein nachgelagertes
# Werkzeug (protoc / STJ-Source-Generator) braucht und die ein In-Compilation-Roslyn-Generator
# daher nicht liefern kann: domain.proto, die STJ-Kontexte und die Python-Basen (Liste: Cqrs.Codegen --artefakte).
#
# Nutzung:
#   ./codegen.sh           # hash-getriggert: nur generieren, wenn sich die gebauten Domänen-DLLs änderten
#   ./codegen.sh --force   # immer generieren
#   ./codegen.sh --check   # CI-Gate: frisch generieren, dann auf Drift prüfen (git diff)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

PROJECT="Cqrs.Codegen/Cqrs.Codegen.csproj"

# Welche Anwendungen es gibt (je Komposition <Wurzel>.Laufzeit eine), welche Projekte sie bilden und welche Dateien entstehen, weiß nur
# das Werkzeug (Abstractions/Projektstruktur.cs, docs/07); ob sich etwas geändert hat, entscheidet es je Anwendung über einen Hash der
# gebauten Domänen-DLLs (--wenn-geaendert).
run() {
  dotnet run --project "$PROJECT" -c Debug -- "" Debug --alle "$@"
}

case "${1:-}" in
  --check)
    echo "CI-Gate: generiere frisch und prüfe auf Drift..."
    run >/dev/null
    WERKZEUG="$(dotnet msbuild "$PROJECT" -getProperty:TargetPath -p:Configuration=Debug -nologo)"   # Build-Ausgang, nicht geraten
    ARTEFAKTE=($(dotnet exec "$WERKZEUG" --alle --artefakte))
    if ! git diff --quiet -- "${ARTEFAKTE[@]}"; then
      echo "❌ DRIFT: generierte Artefakte sind nicht aktuell. './codegen.sh --force' ausführen und committen." >&2
      git --no-pager diff --stat -- "${ARTEFAKTE[@]}" >&2
      exit 1
    fi
    echo "✅ OK — keine Drift."
    ;;
  --force)
    run
    ;;
  *)
    run --wenn-geaendert
    ;;
esac
