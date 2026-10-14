#!/usr/bin/env bash
# Postgres-Grundgrößen für die Konsum- und Schreibseite (docs/12 §12.5, docs/plan-garantien-und-durchsatz.md).
# Misst mit pgbench IM Postgres-Container, rohes SQL ohne Marten und ohne Netzwerk — die absoluten Werte sind eine Obergrenze,
# belastbar sind die Verhältnisse. Legt eine eigene Datenbank an und löscht sie am Ende wieder; andere Datenbanken bleiben unberührt.
#
#   scripts/vermessung-postgres.sh [container] [sekunden]     (Standard: cqrs-postgres, 15)
set -euo pipefail

CONTAINER="${1:-cqrs-postgres}"
DAUER="${2:-15}"
DB="cqrs_vermessung_tmp"
PSQL=(docker exec -i "$CONTAINER" psql -U postgres -v ON_ERROR_STOP=1 -q)

aufraeumen() {
  "${PSQL[@]}" -c "DROP DATABASE IF EXISTS $DB;" >/dev/null 2>&1 || true
  docker exec "$CONTAINER" sh -c 'rm -rf /tmp/cqrs-vermessung' >/dev/null 2>&1 || true
}
trap aufraeumen EXIT

aufraeumen
"${PSQL[@]}" -c "CREATE DATABASE $DB;"
"${PSQL[@]}" -d "$DB" <<'SQL'
CREATE TABLE checkpoint (id text PRIMARY KEY, data jsonb NOT NULL, version int NOT NULL);
CREATE TABLE readmodel  (id text PRIMARY KEY, data jsonb NOT NULL);
INSERT INTO checkpoint SELECT 'p:'||g, '{"v":1}', 1 FROM generate_series(1,200000) g;
INSERT INTO readmodel  SELECT 'r:'||g, '{"name":"x","status":1}' FROM generate_series(1,200000) g;
CREATE TABLE streams (id int PRIMARY KEY, version int NOT NULL);
INSERT INTO streams SELECT g, 0 FROM generate_series(1,200000) g;
CREATE TABLE events (seq bigserial PRIMARY KEY, stream_id int NOT NULL, version int NOT NULL, type text NOT NULL,
  data jsonb NOT NULL, ts timestamptz NOT NULL DEFAULT now(), causation text, UNIQUE (stream_id, version));
ANALYZE;
SQL

docker exec "$CONTAINER" mkdir -p /tmp/cqrs-vermessung
schreibe() { docker exec -i "$CONTAINER" sh -c "cat > /tmp/cqrs-vermessung/$1.sql"; }

# Weckung wie heute je (Projektion, Stream): Tracker lesen, Tail lesen, Effekt + Checkpoint in einer Transaktion.
schreibe weckung <<'SQL'
\set i random(1, 200000)
SELECT version FROM checkpoint WHERE id = 'p:' || :i;
SELECT data FROM readmodel WHERE id = 'r:' || :i;
BEGIN;
UPDATE readmodel SET data = jsonb_set(data,'{status}', to_jsonb(:i)) WHERE id = 'r:' || :i;
INSERT INTO checkpoint VALUES ('p:' || :i, '{"v":2}', 2) ON CONFLICT (id) DO UPDATE SET data = excluded.data, version = checkpoint.version + 1;
COMMIT;
SQL
# Leere Weckung (Poll-Heilung, nichts zu tun): zwei Lese-Queries.
schreibe leer <<'SQL'
\set i random(1, 200000)
SELECT version FROM checkpoint WHERE id = 'p:' || :i;
SELECT data FROM readmodel WHERE id = 'r:' || :i;
SQL
# Batch: 500 Effekte + 500 Checkpoints in einer Transaktion.
schreibe batch <<'SQL'
\set i random(1, 199000)
BEGIN;
UPDATE readmodel SET data = jsonb_set(data,'{status}', to_jsonb(:i)) WHERE id IN (SELECT 'r:'||g FROM generate_series(:i, :i+499) g);
INSERT INTO checkpoint SELECT 'p:'||g, '{"v":2}', 2 FROM generate_series(:i, :i+499) g ON CONFLICT (id) DO UPDATE SET data = excluded.data, version = checkpoint.version + 1;
COMMIT;
SQL
# Group-Commit: 256 Appends je Transaktion, je Append ein Event — ohne Inbox-Marke.
schreibe append_ohne_marke <<'SQL'
\set i random(1, 199700)
BEGIN;
WITH u AS (UPDATE streams SET version = version + 1 WHERE id BETWEEN :i AND :i+255 RETURNING id, version)
INSERT INTO events (stream_id, version, type, data, causation)
SELECT id, version, 'TeilGefertigt', '{"teil":"A-4711","linie":3,"station":12,"dauerMs":5321,"ergebnis":"iO","werker":"w-17"}'::jsonb, gen_random_uuid()::text FROM u;
COMMIT;
SQL
# Dasselbe mit Inbox-Marke je Command (KommandoVerarbeitet).
schreibe append_mit_marke <<'SQL'
\set i random(1, 199700)
BEGIN;
WITH u AS (UPDATE streams SET version = version + 2 WHERE id BETWEEN :i AND :i+255 RETURNING id, version)
INSERT INTO events (stream_id, version, type, data, causation)
SELECT id, version - 1, 'TeilGefertigt', '{"teil":"A-4711","linie":3,"station":12,"dauerMs":5321,"ergebnis":"iO","werker":"w-17"}'::jsonb, gen_random_uuid()::text FROM u
UNION ALL
SELECT id, version, 'KommandoVerarbeitet', '{"commandId":"0b6f0a8e-3c1d-4c55-9a8e-1f2d3c4b5a69"}'::jsonb, gen_random_uuid()::text FROM u;
COMMIT;
SQL

lauf() {  # name verbindungen
  local r
  r=$(docker exec "$CONTAINER" pgbench -U postgres -n -f "/tmp/cqrs-vermessung/$1.sql" -c "$2" -j 4 -T "$DAUER" "$DB" 2>&1)
  printf '%-20s c=%-3s %s  %s\n' "$1" "$2" "$(grep -E '^tps' <<<"$r" | awk '{printf "tps=%.0f", $3}')" \
    "$(grep -E 'latency average' <<<"$r" | awk '{print "latenz=" $4 " ms"}')"
}

echo "== pgbench, $DAUER s je Lauf, Container $CONTAINER"
lauf weckung 4;  lauf weckung 16
lauf leer 4;     lauf leer 16
lauf batch 4
lauf append_ohne_marke 4
lauf append_mit_marke 4

echo "== Lesepfade (Ausführungszeit in der Datenbank)"
"${PSQL[@]}" -d "$DB" <<'SQL' | grep -E "Execution Time|^ *-- "
INSERT INTO streams VALUES (999999, 100000);
INSERT INTO events (stream_id, version, type, data)
  SELECT 999999, g, 'Takt', '{"teil":"A-4711","linie":3,"station":12,"dauerMs":5321}'::jsonb FROM generate_series(1,100000) g;
CREATE TABLE stand (projektion text, stream_id int, version int, PRIMARY KEY (projektion, stream_id));
INSERT INTO stand SELECT 'p1', id, version FROM streams;
UPDATE stand SET version = version - 1 WHERE stream_id % 50 = 0;
ANALYZE;
\echo '  -- Poll-Fenster: 30 000 Events mit Payload'
EXPLAIN ANALYZE SELECT seq, stream_id, version, type, data FROM events WHERE seq > (SELECT max(seq) - 30000 FROM events) ORDER BY seq;
\echo '  -- nachhängende Streams als eine Abfrage (Kopf > Stand)'
EXPLAIN ANALYZE SELECT s.id FROM streams s JOIN stand t ON t.projektion = 'p1' AND t.stream_id = s.id WHERE s.version > t.version;
\echo '  -- langer Stream (100 000 Events) ab 0'
EXPLAIN ANALYZE SELECT seq, version, type, data FROM events WHERE stream_id = 999999 AND version >= 0 ORDER BY version;
\echo '  -- langer Stream ab Cursor (letzte 10)'
EXPLAIN ANALYZE SELECT seq, version, type, data FROM events WHERE stream_id = 999999 AND version > 99990 ORDER BY version;
SQL
