## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-17_2135) |
|---|---|---:|
| S3-8 | Durchsatz bis sichtbar (/s) | – |
| S3-8 | angenommen (/s) | – |
| S3-8 | sichtbar im Fenster (/s) | – |
| S3-8 | Latenz p50 (ms) | – |
| S3-8 | CPU Host (Kerne) | – |
| S3-8 | CPU Postgres-Backends (Kerne) | – |
| S3-8 | Rechner ausgelastet (%) | – |
| S3-8 | CPU-Druck (Anteil) | – |
| S3-8 | Postgres aktiv (Verbindungen) | – |
| S3-8 | davon auf CPU | – |
| S3-8 | davon WAL (Sync, Write, Insert) | – |
| S3-8 | davon Sperren (Lock) | – |
| S3-8 | idle in transaction | – |
| S3-8 | WAL-Syncs (/s) | – |
| S3-8 | Npgsql belegt (Mittel) | – |
| S3-8 | Threadpool-Warteschlange (höchst) | – |
| S3-64 | Durchsatz bis sichtbar (/s) | – |
| S3-64 | angenommen (/s) | – |
| S3-64 | sichtbar im Fenster (/s) | – |
| S3-64 | Latenz p50 (ms) | – |
| S3-64 | CPU Host (Kerne) | – |
| S3-64 | CPU Postgres-Backends (Kerne) | – |
| S3-64 | Rechner ausgelastet (%) | – |
| S3-64 | CPU-Druck (Anteil) | – |
| S3-64 | Postgres aktiv (Verbindungen) | – |
| S3-64 | davon auf CPU | – |
| S3-64 | davon WAL (Sync, Write, Insert) | – |
| S3-64 | davon Sperren (Lock) | – |
| S3-64 | idle in transaction | – |
| S3-64 | WAL-Syncs (/s) | – |
| S3-64 | Npgsql belegt (Mittel) | – |
| S3-64 | Threadpool-Warteschlange (höchst) | – |
| S5-1 | Durchsatz bis sichtbar (/s) | 284 |
| S5-1 | angenommen (/s) | 284 |
| S5-1 | sichtbar im Fenster (/s) | 275 |
| S5-1 | Latenz p50 (ms) | 17.4 |
| S5-1 | CPU Host (Kerne) | 4.51 |
| S5-1 | CPU Postgres-Backends (Kerne) | 2.21 |
| S5-1 | Rechner ausgelastet (%) | 36.9 |
| S5-1 | CPU-Druck (Anteil) | 0.015 |
| S5-1 | Postgres aktiv (Verbindungen) | 3.45 |
| S5-1 | davon auf CPU | 1.39 |
| S5-1 | davon WAL (Sync, Write, Insert) | 1.27 |
| S5-1 | davon Sperren (Lock) | 0 |
| S5-1 | idle in transaction | 0.33 |
| S5-1 | WAL-Syncs (/s) | 1.32e+03 |
| S5-1 | Npgsql belegt (Mittel) | 3.36 |
| S5-1 | Threadpool-Warteschlange (höchst) | 5 |
| S5-8 | Durchsatz bis sichtbar (/s) | 973 |
| S5-8 | angenommen (/s) | 1.19e+03 |
| S5-8 | sichtbar im Fenster (/s) | 897 |
| S5-8 | Latenz p50 (ms) | 382 |
| S5-8 | CPU Host (Kerne) | 5.8 |
| S5-8 | CPU Postgres-Backends (Kerne) | 3.68 |
| S5-8 | Rechner ausgelastet (%) | 53.1 |
| S5-8 | CPU-Druck (Anteil) | 0.127 |
| S5-8 | Postgres aktiv (Verbindungen) | 4.97 |
| S5-8 | davon auf CPU | 1.97 |
| S5-8 | davon WAL (Sync, Write, Insert) | 1.66 |
| S5-8 | davon Sperren (Lock) | 0 |
| S5-8 | idle in transaction | 0.63 |
| S5-8 | WAL-Syncs (/s) | 1.23e+03 |
| S5-8 | Npgsql belegt (Mittel) | 39.3 |
| S5-8 | Threadpool-Warteschlange (höchst) | 124 |
| S5-64 | Durchsatz bis sichtbar (/s) | 977 |
| S5-64 | angenommen (/s) | 2.37e+03 |
| S5-64 | sichtbar im Fenster (/s) | 763 |
| S5-64 | Latenz p50 (ms) | 1.12e+03 |
| S5-64 | CPU Host (Kerne) | 6.83 |
| S5-64 | CPU Postgres-Backends (Kerne) | 4.36 |
| S5-64 | Rechner ausgelastet (%) | 60.8 |
| S5-64 | CPU-Druck (Anteil) | 0.164 |
| S5-64 | Postgres aktiv (Verbindungen) | 5.8 |
| S5-64 | davon auf CPU | 2.27 |
| S5-64 | davon WAL (Sync, Write, Insert) | 1.38 |
| S5-64 | davon Sperren (Lock) | 0 |
| S5-64 | idle in transaction | 1.85 |
| S5-64 | WAL-Syncs (/s) | 746 |
| S5-64 | Npgsql belegt (Mittel) | 9.25 |
| S5-64 | Threadpool-Warteschlange (höchst) | 46 |

# Engpass-Sonde — 2026-10-17_2135_61f8e32_server-sonde-profil

Commit `61f8e32` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## S5-1-w1 (11.0 s)

- CPU: host-pruefdomaene 4.51, pg-backend 2.21, treiber 0.29, sonst 0.08, pg-backend-treiber 0.07, cqrs-vermessung-redis 0.06, python 0.03, consul 0.01
- Rechner: 36.9 % ausgelastet, iowait 2.2 %, CPU-Druck 0.015, IO-Druck 0.038, Kerne über 90 %: 0.0, P-Kerne 31.3 %, E-Kerne 45.4 %; je Kern [29, 30, 34, 27, 34, 29, 36, 28, 38, 26, 37, 26, 55, 51, 49, 47, 42, 41, 40, 38]
- Host-Threads über 5 %: 22, höchster Einzelwert 76.7 %; heißeste: .NET Tiered Com 33.4 %, .NET TP Worker 29.4 %, .NET TP Worker 28.6 %, .NET TP Worker 27.8 %, .NET TP Worker 27.5 %, .NET TP Worker 26.2 %
- Postgres: 47.1 Verbindungen, davon aktiv 3.45, idle in transaction 0.33; idle 43.28, active CPU 1.39, active Client:ClientRead 0.77, active IO:WALSync 0.67, active LWLock:WALWrite 0.58, idle in transaction 0.33, active IO:WALWrite 0.02
  - 1.48 × active: `COMMIT`
  - 0.81 × active: `select data, type, mt_dotnet_type, seq_id, id, stream_id, version, tim`
  - 0.33 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.29 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.28 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.21 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.05 × active: `BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED`
  - 0.05 × active: `select es.mt_upsert_prozessoffen($1,$2,$3,$4)`
- WAL je s: 17759.4 Records, 2.2 MB, 1317.5 Writes, 1317.0 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 2.727, höchst 5.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 41.636, höchst 45.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 3.364, höchst 9.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.026, höchst 0.036; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 65.182, höchst 84.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel -2.273, höchst 5.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.011, höchst 0.026; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-8-w1 (3.0 s)

- CPU: host-pruefdomaene 5.8, pg-backend 3.68, treiber 0.36, sonst 0.12, pg-backend-treiber 0.11, cqrs-vermessung-redis 0.06, python 0.03, consul 0.01
- Rechner: 53.1 % ausgelastet, iowait 1.2 %, CPU-Druck 0.127, IO-Druck 0.025, Kerne über 90 %: 0.0, P-Kerne 49.2 %, E-Kerne 59.0 %; je Kern [48, 52, 52, 47, 49, 50, 49, 44, 50, 48, 52, 48, 64, 63, 63, 64, 53, 55, 55, 55]
- Host-Threads über 5 %: 46, höchster Einzelwert 26.7 %; heißeste: .NET TP Worker 24.8 %, .NET TP Worker 22.5 %, .NET TP Worker 21.5 %, .NET TP Worker 21.5 %, .NET TP Worker 19.2 %, .NET TP Worker 16.5 %
- Postgres: 80.2 Verbindungen, davon aktiv 4.97, idle in transaction 0.63; idle 74.57, active CPU 1.97, active Client:ClientRead 1.33, active LWLock:WALWrite 1.03, idle in transaction 0.63, active IO:WALSync 0.63
  - 1.67 × active: `COMMIT`
  - 1.4 × active: `select data, type, mt_dotnet_type, seq_id, id, stream_id, version, tim`
  - 0.93 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.37 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.23 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.23 × active: `select d.id, d.data from es.mt_doc_snapshot_auftrag as d where id = $1`
  - 0.23 × idle in transaction: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
  - 0.17 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
- WAL je s: 42845.7 Records, 5.7 MB, 1227.7 Writes, 1226.2 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 42.667, höchst 98.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 5.5, höchst 11.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 59.0, höchst 89.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 39.333, höchst 93.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.07, höchst 0.104; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 172.0, höchst 220.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 77.667, höchst 124.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 6.333, höchst 19.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.04, höchst 0.048; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 1.667, höchst 3.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-64-w1 (4.0 s)

- CPU: host-pruefdomaene 6.83, pg-backend 4.36, treiber 0.33, pg-backend-treiber 0.15, sonst 0.08, cqrs-vermessung-redis 0.05, python 0.02
- Rechner: 60.8 % ausgelastet, iowait 0.9 %, CPU-Druck 0.164, IO-Druck 0.021, Kerne über 90 %: 0.0, P-Kerne 57.4 %, E-Kerne 66.1 %; je Kern [54, 58, 57, 57, 58, 58, 58, 58, 60, 55, 57, 57, 70, 71, 70, 70, 61, 61, 63, 63]
- Host-Threads über 5 %: 45, höchster Einzelwert 45.8 %; heißeste: .NET TP Worker 25.5 %, .NET TP Worker 23.5 %, .NET TP Worker 23.0 %, .NET EventPipe 20.3 %, .NET TP Worker 20.1 %, .NET TP Worker 20.1 %
- Postgres: 103.0 Verbindungen, davon aktiv 5.8, idle in transaction 1.85; idle 95.35, active CPU 2.27, active Client:ClientRead 2.15, idle in transaction 1.85, active LWLock:WALWrite 0.85, active IO:WALSync 0.5, active LWLock:WALInsert 0.03
  - 2.02 × active: `COMMIT`
  - 1.75 × active: `select data, type, mt_dotnet_type, seq_id, id, stream_id, version, tim`
  - 1.22 × idle in transaction: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
  - 0.68 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.55 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.5 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.15 × active: `select d.id, d.data from es.mt_doc_emittentencursordoc as d where id =`
  - 0.15 × active: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
- WAL je s: 51173.8 Records, 6.9 MB, 747.7 Writes, 746.3 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 9.75, höchst 23.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 90.75, höchst 99.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 9.25, höchst 16.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.032, höchst 0.047; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 118.75, höchst 206.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel -58.25, höchst 46.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.061, höchst 0.086; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 4.5, höchst 18.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## L5-nachlauf-w1 (14.0 s)

- CPU: host-pruefdomaene 0.48, sonst 0.36, pg-backend 0.33, pg-backend-treiber 0.07, treiber 0.06, python 0.03, consul 0.02
- Rechner: 7.5 % ausgelastet, iowait 0.1 %, CPU-Druck 0.005, IO-Druck 0.001, Kerne über 90 %: 0.2, P-Kerne 9.7 %, E-Kerne 4.3 %; je Kern [8, 5, 9, 29, 13, 6, 12, 6, 12, 3, 11, 3, 7, 6, 5, 4, 3, 3, 3, 3]
- Host-Threads über 5 %: 10, höchster Einzelwert 46.7 %; heißeste: .NET EventPipe 11.2 %, .NET TP Worker 4.0 %, .NET EventPipe 2.9 %, .NET TP Worker 2.7 %, .NET TP Worker 2.1 %, .NET TP Worker 1.5 %
- Postgres: 103.0 Verbindungen, davon aktiv 0.22, idle in transaction 0; idle 102.78, active CPU 0.15, active Client:ClientRead 0.04, active IO:WALSync 0.03
  - 0.16 × active: `select data, type, mt_dotnet_type, seq_id, id, stream_id, version, tim`
- WAL je s: 352.9 Records, 0.0 MB, 14.3 Writes, 14.0 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.214, höchst 1.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 99.714, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 0.286, höchst 2.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.001, höchst 0.01; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.007, höchst 0.046; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.071, höchst 0.0

## L5-w1 (61.2 s)

- CPU: sonst 0.07, pg-backend 0.06, python 0.04, consul 0.02, host-pruefdomaene 0.02, treiber 0.01, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.5 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.9 %, E-Kerne 1.0 %; je Kern [2, 2, 4, 1, 3, 0, 3, 1, 3, 0, 4, 0, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 0, höchster Einzelwert 0 %; heißeste: 
- Postgres: 103.0 Verbindungen, davon aktiv 0.06, idle in transaction 0.0; idle 102.94, active LWLock:WALWrite 0.04, active Client:ClientRead 0.01
- WAL je s: 90.8 Records, 0.0 MB, 1.1 Writes, 1.0 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.049, höchst 3.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 99.131, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 0.869, höchst 53.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 7.77, höchst 461.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.246, höchst 15.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.344, höchst 10.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.011; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.131, höchst 8.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.148, höchst 10.0

