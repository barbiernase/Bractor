#!/usr/bin/env bash
# REPO-PFAD: codegen.sh
# Vereinter Codegen-Prepass: erzeugt VOR dem Compile die Artefakte, die ein nachgelagertes
# Werkzeug (protoc / STJ-Source-Generator) braucht und die ein In-Compilation-Roslyn-Generator
# daher nicht liefern kann:
#   - ProtoRepo/domain.proto
#   - Infrastructure/Serialization/EventJsonSerializerContext.g.cs
#   - Infrastructure/Serialization/CqrsWireJsonContext.g.cs
#
# Nutzung:
#   ./codegen.sh           # hash-getriggert: nur generieren, wenn sich Domain-Quellen änderten
#   ./codegen.sh --force   # immer generieren
#   ./codegen.sh --check   # CI-Gate: frisch generieren, dann auf Drift prüfen (git diff)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

HASH_FILE=".codegen_hash"
PROJECT="Cqrs.Codegen/Cqrs.Codegen.csproj"
ARTEFAKTE=(
  "ProtoRepo/domain.proto"
  "Infrastructure/Serialization/EventJsonSerializerContext.g.cs"
  "Infrastructure/Serialization/CqrsWireJsonContext.g.cs"
)

# Der Output hängt allein an den Domain-Quellen der gescannten Projekte.
compute_hash() {
  find Domain Domain.Projections Domain.Pipeline -name '*.cs' \
       -not -path '*/obj/*' -not -path '*/bin/*' \
    | sort | xargs shasum -a 256 | shasum -a 256 | cut -d' ' -f1
}

run() {
  dotnet run --project "$PROJECT" -c Debug
}

case "${1:-}" in
  --check)
    echo "CI-Gate: generiere frisch und prüfe auf Drift..."
    run >/dev/null
    if ! git diff --quiet -- "${ARTEFAKTE[@]}"; then
      echo "❌ DRIFT: generierte Artefakte sind nicht aktuell. './codegen.sh --force' ausführen und committen." >&2
      git --no-pager diff --stat -- "${ARTEFAKTE[@]}" >&2
      exit 1
    fi
    echo "✅ OK — keine Drift."
    ;;
  --force)
    run
    compute_hash > "$HASH_FILE"
    ;;
  *)
    CUR="$(compute_hash)"
    STORED=""; [ -f "$HASH_FILE" ] && STORED="$(cat "$HASH_FILE")"
    if [ "$CUR" = "$STORED" ]; then
      echo "Codegen aktuell (${CUR:0:16}...) — übersprungen."
    else
      echo "Domain-Quellen geändert → generiere..."
      run
      echo "$CUR" > "$HASH_FILE"
    fi
    ;;
esac
