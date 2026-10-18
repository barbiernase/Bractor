#!/bin/bash
# Nur auf dem Messserver (scripts/server.sh lauf scripts/stoerungsserie.sh <N>): Rest-Zustand löschen (Mess- und Test-Datenbanken, beide Redis,
# Host-Logs), Commit-Zeitstempel in Postgres einschalten (einmal, mit Neustart des Containers), dann bis zu N Störungsläufe. Beim ersten roten
# Lauf sichert es den Zustand in dessen Ergebnis-Ordner (g11-diagnose.txt: Guards hinter dem Stream, die Events samt Transaktion und
# Commit-Zeit, Nachbarschaft, Partitions-Checkpoints, DLQ; dazu die Host-Logs) und hält an — bevor der nächste Lauf die Datenbanken neu anlegt
# (G-11, docs/13).
cd "$(dirname "$0")/.."
N=${1:-5}
PSQL="docker exec -i cqrs-postgres psql -U postgres -v ON_ERROR_STOP=0 -P pager=off"

echo "== Rest-Zustand löschen"
for db in cqrs_vermessung cqrs_vermessung_pruefdomaene; do $PSQL -c "drop database if exists $db with (force)"; done
for db in cqrs_events cqrs_it_geteilt; do $PSQL -c "drop database if exists $db with (force)"; $PSQL -c "create database $db"; done
docker exec cqrs-vermessung-redis redis-cli -p 6390 flushall; docker exec cqrs-redis redis-cli flushall
rm -rf /tmp/cqrs-vermessung-logs/*

echo "== Commit-Zeitstempel"
if [ "$($PSQL -At -c 'show track_commit_timestamp')" != "on" ]; then
  $PSQL -c "alter system set track_commit_timestamp = on"
  docker restart cqrs-postgres >/dev/null
  until docker exec cqrs-postgres pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done
fi
$PSQL -At -c 'show track_commit_timestamp'

diagnose() { # $1 Ergebnis-Ordner
  local o=$1 d=$o/g11-diagnose.txt
  {
    echo "### Beispieldomäne: Guards hinter dem Stream (abonnierte Events nicht angewandt)"
    $PSQL -d cqrs_vermessung -c "
      with g as (select c.id, c.data->>'ProjectionId' p, (c.data->>'StreamId')::uuid s, (c.data->>'Version')::int v, c.mt_last_modified from es.mt_doc_projectioncheckpoint c)
      select g.id, g.v guard, max(e.version) kopf, g.mt_last_modified from g join es.mt_events e on e.stream_id = g.s
       and e.type not in ('kommando_abgelehnt','kommando_verarbeitet') group by g.id, g.v, g.mt_last_modified having max(e.version) > g.v order by 1"
    echo "### Events dieser Streams: Sequenz, Transaktion, Commit-Zeit"
    $PSQL -d cqrs_vermessung -c "
      with g as (select (c.data->>'StreamId')::uuid s, (c.data->>'Version')::int v from es.mt_doc_projectioncheckpoint c),
      kaputt as (select distinct g.s from g join es.mt_events e on e.stream_id = g.s and e.type not in ('kommando_abgelehnt','kommando_verarbeitet')
                 group by g.s, g.v having max(e.version) > g.v)
      select e.stream_id, e.version, e.seq_id, e.type, e.xmin::text xid, pg_xact_commit_timestamp(e.xmin) commit, e.timestamp, e.correlation_id
        from es.mt_events e join kaputt k on k.s = e.stream_id order by e.stream_id, e.version"
    echo "### Nachbarschaft: Events ±40 Sequenzen um die betroffenen, mit Transaktion und Commit-Zeit"
    $PSQL -d cqrs_vermessung -c "
      with g as (select (c.data->>'StreamId')::uuid s, (c.data->>'Version')::int v from es.mt_doc_projectioncheckpoint c),
      kaputt as (select distinct g.s from g join es.mt_events e on e.stream_id = g.s and e.type not in ('kommando_abgelehnt','kommando_verarbeitet')
                 group by g.s, g.v having max(e.version) > g.v),
      seqs as (select e.seq_id from es.mt_events e join kaputt k on k.s = e.stream_id)
      select e.seq_id, e.stream_id, e.version, e.type, e.xmin::text xid, pg_xact_commit_timestamp(e.xmin) commit, e.timestamp
        from es.mt_events e where exists (select 1 from seqs where abs(seqs.seq_id - e.seq_id) <= 40) order by e.seq_id"
    echo "### Partitions-Checkpoints"
    $PSQL -d cqrs_vermessung -c "select id, data->>'Position' pos, data->>'Version' v, data->'Geparkt' geparkt, mt_last_modified from es.mt_doc_partitionscheckpoint order by 1"
    echo "### DLQ"
    $PSQL -d cqrs_vermessung -c "select data->>'Quelle', data->>'AggregateId', left(data->>'Grund',200), data->>'ErfasstUtc' from dlq.mt_doc_deadletter order by 4"
    echo "### Prüfdomäne: Guards/Cursor hinter dem Stream"
    $PSQL -d cqrs_vermessung_pruefdomaene -c "select count(*) from es.mt_doc_emittentencursordoc"
  } > "$d" 2>&1
  for f in /tmp/cqrs-vermessung-logs/*/*.log; do [ -f "$f" ] && gzip -c "$f" > "$o/$(basename $(dirname $f))-$(basename $f).gz"; done
}

for i in $(seq 1 $N); do
  echo "== Störungslauf $i von $N ($(date +%T))"
  scripts/vermessung-e2e.sh --art stoerung --anlass server-serie-stoerung-$i > /tmp/g11-lauf-$i.log 2>&1
  o=$(ls -d vermessung/ergebnisse/*_server-serie-stoerung-$i | tail -1)
  grep -E "^Gültig|^Exactly-once" $o/zusammenfassung.md
  grep -E "✗" $o/zusammenfassung.md
  if grep -q "Exactly-once: \*\*ROT\*\*" $o/zusammenfassung.md; then
    echo "== ROT — sichere den Zustand in $o"; diagnose $o; head -60 $o/g11-diagnose.txt; break
  fi
  rm -rf /tmp/cqrs-vermessung-logs/*
done
echo "== Ende $(date +%T)"
