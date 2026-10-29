#!/usr/bin/env bash
# REPO-PFAD: Client.Infrastructure.Python/build_leitung.sh
# Generiert die betterproto-Typen der Framework-Leitung (Cqrs.Leitung/cqrs.proto: Dienst, Hüllen, Nutzlast)
# nach cqrs_client/leitung/. Die Domänen-DTOs kommen aus dem Proto der Anwendung (der Codegen-Prepass im Build erzeugt sie im Worker).
#   ./build_leitung.sh           erzeugt neu, wenn sich cqrs.proto geändert hat
#   ./build_leitung.sh --check   prüft nur (Exit 1 = veraltet)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROTO_FILE="$REPO_DIR/Cqrs.Leitung/cqrs.proto"
GENERATED_DIR="$SCRIPT_DIR/cqrs_client/leitung"
HASH_FILE="$GENERATED_DIR/.proto_hash"

[ -f "$PROTO_FILE" ] || { echo "ERROR: $PROTO_FILE nicht gefunden."; exit 1; }

CURRENT_HASH=$(sha256sum "$PROTO_FILE" | cut -d' ' -f1)
STORED_HASH=""
[ -f "$HASH_FILE" ] && STORED_HASH=$(cat "$HASH_FILE")

if [ "${1:-}" = "--check" ]; then
    [ "$CURRENT_HASH" = "$STORED_HASH" ] && [ -f "$GENERATED_DIR/__init__.py" ] && echo "OK (${CURRENT_HASH:0:16}...)" && exit 0
    echo "VERALTET → ./build_leitung.sh"; exit 1
fi

if [ "$CURRENT_HASH" = "$STORED_HASH" ] && [ -f "$GENERATED_DIR/__init__.py" ]; then
    echo "Aktuell (${CURRENT_HASH:0:16}...)"; exit 0
fi

echo "Generiere → $GENERATED_DIR/"
mkdir -p "$GENERATED_DIR"

# Ohne Package-Deklaration generieren: betterproto legt sonst ein Unterpaket (cqrs/) an. Ohne package → alles flach
# in __init__.py. Die gRPC-Route (/cqrs.CqrsClientService/Connect) setzt der GrpcProxy selbst (ROUTE in proxy.py).
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

sed 's/^package .*;//' "$PROTO_FILE" > "$TEMP_DIR/cqrs.proto"

python3 -m grpc_tools.protoc \
    -I "$TEMP_DIR" \
    --python_betterproto_out="$GENERATED_DIR" \
    "$TEMP_DIR/cqrs.proto"

[ -f "$GENERATED_DIR/__init__.py" ] || { echo "ERROR: Generierung fehlgeschlagen"; exit 1; }

if [ -d "$GENERATED_DIR/cqrs" ]; then
    rm -rf "$GENERATED_DIR/cqrs"
fi

echo "$CURRENT_HASH" > "$HASH_FILE"
TYPES=$(grep -c "^class " "$GENERATED_DIR/__init__.py" || echo "?")
echo "✓ $TYPES Typen generiert (Hash: ${CURRENT_HASH:0:16}...)"
