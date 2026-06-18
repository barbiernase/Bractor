#!/usr/bin/env bash
# REPO-PFAD: Domain.Client.Worker.Python.ML/build_python.sh
# Generiert betterproto-Typen nach domain_client/generated/
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROTO_FILE="$REPO_DIR/ProtoRepo/domain.proto"
GENERATED_DIR="$SCRIPT_DIR/domain_client/generated"
HASH_FILE="$GENERATED_DIR/.proto_hash"

[ -f "$PROTO_FILE" ] || { echo "ERROR: $PROTO_FILE nicht gefunden. dotnet build zuerst."; exit 1; }

CURRENT_HASH=$(sha256sum "$PROTO_FILE" | cut -d' ' -f1)
STORED_HASH=""
[ -f "$HASH_FILE" ] && STORED_HASH=$(cat "$HASH_FILE")

if [ "${1:-}" = "--check" ]; then
    [ "$CURRENT_HASH" = "$STORED_HASH" ] && echo "OK (${CURRENT_HASH:0:16}...)" && exit 0
    echo "VERALTET → ./build_python.sh"; exit 1
fi

if [ "$CURRENT_HASH" = "$STORED_HASH" ] && [ -f "$GENERATED_DIR/__init__.py" ]; then
    echo "Aktuell (${CURRENT_HASH:0:16}...)"; exit 0
fi

echo "Generiere → $GENERATED_DIR/"
mkdir -p "$GENERATED_DIR"

# Proto ohne Package-Deklaration generieren.
# betterproto erzeugt sonst ein Unterverzeichnis (CqrsSolution/)
# mit kaputten Cross-References. Ohne package → alles flach in __init__.py.
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

sed 's/^package .*;//' "$PROTO_FILE" > "$TEMP_DIR/domain.proto"

python3 -m grpc_tools.protoc \
    -I "$TEMP_DIR" \
    --python_betterproto_out="$GENERATED_DIR" \
    "$TEMP_DIR/domain.proto"

[ -f "$GENERATED_DIR/__init__.py" ] || { echo "ERROR: Generierung fehlgeschlagen"; exit 1; }

# Aufräumen: betterproto legt manchmal trotzdem Unterordner an
if [ -d "$GENERATED_DIR/CqrsSolution" ]; then
    rm -rf "$GENERATED_DIR/CqrsSolution"
fi

echo "$CURRENT_HASH" > "$HASH_FILE"
TYPES=$(grep -c "^class " "$GENERATED_DIR/__init__.py" || echo "?")
echo "✓ $TYPES Typen generiert (Hash: ${CURRENT_HASH:0:16}...)"
