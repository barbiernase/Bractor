## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-17_2229) |
|---|---|---:|
| S3-8 | Durchsatz bis sichtbar (/s) | 823 |
| S3-8 | angenommen (/s) | 826 |
| S3-8 | sichtbar im Fenster (/s) | 735 |
| S3-8 | Latenz p50 (ms) | 21.1 |
| S3-8 | CPU Host (Kerne) | 3.57 |
| S3-8 | CPU Postgres-Backends (Kerne) | 1.9 |
| S3-8 | Rechner ausgelastet (%) | 30.9 |
| S3-8 | CPU-Druck (Anteil) | 0.029 |
| S3-8 | Postgres aktiv (Verbindungen) | 3.1 |
| S3-8 | davon auf CPU | 1.55 |
| S3-8 | davon WAL (Sync, Write, Insert) | 0.85 |
| S3-8 | davon Sperren (Lock) | 0 |
| S3-8 | idle in transaction | 0 |
| S3-8 | WAL-Syncs (/s) | 1.03e+03 |
| S3-8 | Npgsql belegt (Mittel) | 6 |
| S3-8 | Threadpool-Warteschlange (höchst) | 0 |
| S3-64 | Durchsatz bis sichtbar (/s) | 1.12e+03 |
| S3-64 | angenommen (/s) | 3.02e+03 |
| S3-64 | sichtbar im Fenster (/s) | 352 |
| S3-64 | Latenz p50 (ms) | 713 |
| S3-64 | CPU Host (Kerne) | 2.88 |
| S3-64 | CPU Postgres-Backends (Kerne) | 2.44 |
| S3-64 | Rechner ausgelastet (%) | 30.7 |
| S3-64 | CPU-Druck (Anteil) | 0.048 |
| S3-64 | Postgres aktiv (Verbindungen) | 3.1 |
| S3-64 | davon auf CPU | 1.8 |
| S3-64 | davon WAL (Sync, Write, Insert) | 0.63 |
| S3-64 | davon Sperren (Lock) | 0 |
| S3-64 | idle in transaction | 0.53 |
| S3-64 | WAL-Syncs (/s) | 648 |
| S3-64 | Npgsql belegt (Mittel) | 3 |
| S3-64 | Threadpool-Warteschlange (höchst) | 0 |
| S5-1 | Durchsatz bis sichtbar (/s) | – |
| S5-1 | angenommen (/s) | – |
| S5-1 | sichtbar im Fenster (/s) | – |
| S5-1 | Latenz p50 (ms) | – |
| S5-1 | CPU Host (Kerne) | – |
| S5-1 | CPU Postgres-Backends (Kerne) | – |
| S5-1 | Rechner ausgelastet (%) | – |
| S5-1 | CPU-Druck (Anteil) | – |
| S5-1 | Postgres aktiv (Verbindungen) | – |
| S5-1 | davon auf CPU | – |
| S5-1 | davon WAL (Sync, Write, Insert) | – |
| S5-1 | davon Sperren (Lock) | – |
| S5-1 | idle in transaction | – |
| S5-1 | WAL-Syncs (/s) | – |
| S5-1 | Npgsql belegt (Mittel) | – |
| S5-1 | Threadpool-Warteschlange (höchst) | – |
| S5-8 | Durchsatz bis sichtbar (/s) | – |
| S5-8 | angenommen (/s) | – |
| S5-8 | sichtbar im Fenster (/s) | – |
| S5-8 | Latenz p50 (ms) | – |
| S5-8 | CPU Host (Kerne) | – |
| S5-8 | CPU Postgres-Backends (Kerne) | – |
| S5-8 | Rechner ausgelastet (%) | – |
| S5-8 | CPU-Druck (Anteil) | – |
| S5-8 | Postgres aktiv (Verbindungen) | – |
| S5-8 | davon auf CPU | – |
| S5-8 | davon WAL (Sync, Write, Insert) | – |
| S5-8 | davon Sperren (Lock) | – |
| S5-8 | idle in transaction | – |
| S5-8 | WAL-Syncs (/s) | – |
| S5-8 | Npgsql belegt (Mittel) | – |
| S5-8 | Threadpool-Warteschlange (höchst) | – |
| S5-64 | Durchsatz bis sichtbar (/s) | – |
| S5-64 | angenommen (/s) | – |
| S5-64 | sichtbar im Fenster (/s) | – |
| S5-64 | Latenz p50 (ms) | – |
| S5-64 | CPU Host (Kerne) | – |
| S5-64 | CPU Postgres-Backends (Kerne) | – |
| S5-64 | Rechner ausgelastet (%) | – |
| S5-64 | CPU-Druck (Anteil) | – |
| S5-64 | Postgres aktiv (Verbindungen) | – |
| S5-64 | davon auf CPU | – |
| S5-64 | davon WAL (Sync, Write, Insert) | – |
| S5-64 | davon Sperren (Lock) | – |
| S5-64 | idle in transaction | – |
| S5-64 | WAL-Syncs (/s) | – |
| S5-64 | Npgsql belegt (Mittel) | – |
| S5-64 | Threadpool-Warteschlange (höchst) | – |

# Engpass-Sonde — 2026-10-17_2229_e93bfbb_server-sonde-profil-s1-s3

Commit `e93bfbb` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## S3-8-w1 (2.0 s)

- CPU: host-beispiel 3.57, pg-backend 1.9, treiber 0.33, sonst 0.11, pg-backend-treiber 0.06, cqrs-vermessung-redis 0.04, python 0.02
- Rechner: 30.9 % ausgelastet, iowait 1.5 %, CPU-Druck 0.029, IO-Druck 0.024, Kerne über 90 %: 0.0, P-Kerne 28.8 %, E-Kerne 34.2 %; je Kern [33, 22, 28, 26, 30, 22, 29, 26, 32, 28, 28, 41, 46, 40, 39, 37, 29, 29, 26, 28]
- Host-Threads über 5 %: 43, höchster Einzelwert 46.8 %; heißeste: .NET Tiered Com 38.3 %, .NET TP Worker 18.9 %, .NET TP Worker 17.4 %, .NET TP Worker 15.9 %, .NET TP Worker 14.9 %, .NET TP Worker 14.4 %
- Postgres: 45.0 Verbindungen, davon aktiv 3.1, idle in transaction 0; idle 41.9, active CPU 1.55, active Client:ClientRead 0.65, active IO:WALSync 0.6, active LWLock:WALWrite 0.25, active IO:DataFileExtend 0.05
  - 1.1 × active: `COMMIT`
  - 0.6 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.4 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.4 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.2 × active: `select d.id, d.data from es.mt_doc_snapshot_teilauftrag as d where id `
  - 0.15 × active: `insert into es.mt_streams (id, type, version, tenant_id) values ($1,$2`
  - 0.1 × active: `select es.mt_upsert_prozessoffen($1,$2,$3,$4)`
  - 0.05 × active: `select count(*) from es.mt_events where seq_id > $1 and seq_id > ($2::`
- WAL je s: 31630.4 Records, 4.1 MB, 1029.0 Writes, 1029.0 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 3.0, höchst 5.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 34.0, höchst 34.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 6.0, höchst 6.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 3.0, höchst 3.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.041, höchst 0.045; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 133.0, höchst 141.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.018, höchst 0.02; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S3-64-w1 (3.0 s)

- CPU: host-beispiel 2.88, pg-backend 2.44, treiber 0.34, pg-backend-treiber 0.12, sonst 0.08, cqrs-vermessung-redis 0.05, python 0.02, consul 0.01
- Rechner: 30.7 % ausgelastet, iowait 1.5 %, CPU-Druck 0.048, IO-Druck 0.023, Kerne über 90 %: 0.0, P-Kerne 29.6 %, E-Kerne 32.4 %; je Kern [29, 28, 28, 29, 30, 32, 31, 27, 32, 26, 30, 35, 38, 36, 35, 36, 29, 29, 29, 27]
- Host-Threads über 5 %: 27, höchster Einzelwert 56.5 %; heißeste: .NET Tiered Com 47.3 %, .NET TP Worker 16.9 %, .NET TP Worker 15.2 %, .NET TP Worker 14.9 %, .NET TP Worker 14.6 %, .NET TP Worker 14.6 %
- Postgres: 59.6 Verbindungen, davon aktiv 3.1, idle in transaction 0.53; idle 55.93, active CPU 1.8, active Client:ClientRead 0.67, idle in transaction 0.53, active IO:WALSync 0.33, active LWLock:WALWrite 0.3
  - 0.8 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.73 × active: `COMMIT`
  - 0.7 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.43 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.4 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.1 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.1 × active: `select es.mt_upsert_prozessoffen($1,$2,$3,$4)`
  - 0.1 × active: `select d.id, d.data from es.mt_doc_snapshot_teilauftrag as d where id `
- WAL je s: 36356.5 Records, 4.9 MB, 648.5 Writes, 647.5 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 1.0, höchst 2.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 63.0, höchst 66.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 3.0, höchst 5.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 3.0, höchst 3.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.031, höchst 0.079; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 81.667, höchst 223.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.019, höchst 0.029; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 14.333, höchst 43.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## L1-nachlauf-w1 (43.1 s)

- CPU: sonst 0.22, host-beispiel 0.16, pg-backend 0.08, python 0.04, treiber 0.03, pg-backend-treiber 0.03, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 3.2 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.001, Kerne über 90 %: 0.1, P-Kerne 4.5 %, E-Kerne 1.1 %; je Kern [3, 2, 9, 0, 6, 2, 6, 1, 6, 1, 17, 0, 2, 2, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 15, höchster Einzelwert 29.9 %; heißeste: .NET EventPipe 9.7 %, .NET EventPipe 1.0 %, .NET TP Worker 0.6 %, .NET TP Worker 0.4 %, .NET Tiered Com 0.3 %, .NET TP Worker 0.3 %
- Postgres: 71.0 Verbindungen, davon aktiv 0.02, idle in transaction 0; idle 70.98, active CPU 0.02
- WAL je s: 208.5 Records, 0.0 MB, 7.3 Writes, 7.1 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 0.023, höchst 1.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 65.977, höchst 66.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 0.023, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 3.0, höchst 3.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.002, höchst 0.038; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.419, höchst 3.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.767, höchst 2.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.002, höchst 0.023; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.419, höchst 0.0

## L1-w1 (65.2 s)

- CPU: pg-backend 0.05, sonst 0.05, python 0.04, host-beispiel 0.03, consul 0.02, treiber 0.01, cqrs-redis 0.01, cqrs-vermessung-redis 0.01
- Rechner: 1.3 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.7 %, E-Kerne 0.9 %; je Kern [2, 0, 5, 0, 4, 0, 4, 0, 3, 0, 2, 0, 1, 1, 1, 1, 1, 0, 1, 1]
- Host-Threads über 5 %: 0, höchster Einzelwert 0 %; heißeste: 
- Postgres: 71.1 Verbindungen, davon aktiv 0.09, idle in transaction 0.01; idle 70.96, active LWLock:WALWrite 0.07, idle in transaction 0.01
  - 0.07 × active: `COMMIT`
- WAL je s: 1.5 Records, 0.0 MB, 0.1 Writes, 0.1 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 0.016, höchst 1.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 65.984, höchst 66.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 0.016, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 3.0, höchst 3.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.006; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.359, höchst 7.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel 0.016, höchst 11.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.003; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.062, höchst 4.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 4.0

