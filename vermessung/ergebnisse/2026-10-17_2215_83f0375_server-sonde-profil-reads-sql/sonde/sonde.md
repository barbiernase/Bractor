## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-17_2215) |
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
| S5-1 | Durchsatz bis sichtbar (/s) | 335 |
| S5-1 | angenommen (/s) | 335 |
| S5-1 | sichtbar im Fenster (/s) | 330 |
| S5-1 | Latenz p50 (ms) | 13 |
| S5-1 | CPU Host (Kerne) | 3.83 |
| S5-1 | CPU Postgres-Backends (Kerne) | 1.3 |
| S5-1 | Rechner ausgelastet (%) | 33.6 |
| S5-1 | CPU-Druck (Anteil) | 0.014 |
| S5-1 | Postgres aktiv (Verbindungen) | 3.9 |
| S5-1 | davon auf CPU | 1.22 |
| S5-1 | davon WAL (Sync, Write, Insert) | 1.82 |
| S5-1 | davon Sperren (Lock) | 0 |
| S5-1 | idle in transaction | 0.27 |
| S5-1 | WAL-Syncs (/s) | 1.54e+03 |
| S5-1 | Npgsql belegt (Mittel) | 6.11 |
| S5-1 | Threadpool-Warteschlange (höchst) | 2 |
| S5-8 | Durchsatz bis sichtbar (/s) | 1.01e+03 |
| S5-8 | angenommen (/s) | 1.02e+03 |
| S5-8 | sichtbar im Fenster (/s) | 930 |
| S5-8 | Latenz p50 (ms) | 38.9 |
| S5-8 | CPU Host (Kerne) | 4.98 |
| S5-8 | CPU Postgres-Backends (Kerne) | 2.95 |
| S5-8 | Rechner ausgelastet (%) | 53.2 |
| S5-8 | CPU-Druck (Anteil) | 0.162 |
| S5-8 | Postgres aktiv (Verbindungen) | 5.83 |
| S5-8 | davon auf CPU | 2.13 |
| S5-8 | davon WAL (Sync, Write, Insert) | 1.46 |
| S5-8 | davon Sperren (Lock) | 0 |
| S5-8 | idle in transaction | 0.53 |
| S5-8 | WAL-Syncs (/s) | 1.15e+03 |
| S5-8 | Npgsql belegt (Mittel) | 17.3 |
| S5-8 | Threadpool-Warteschlange (höchst) | 1 |
| S5-64 | Durchsatz bis sichtbar (/s) | 1.78e+03 |
| S5-64 | angenommen (/s) | 2.15e+03 |
| S5-64 | sichtbar im Fenster (/s) | 1.21e+03 |
| S5-64 | Latenz p50 (ms) | 300 |
| S5-64 | CPU Host (Kerne) | 4.7 |
| S5-64 | CPU Postgres-Backends (Kerne) | 4.28 |
| S5-64 | Rechner ausgelastet (%) | 60 |
| S5-64 | CPU-Druck (Anteil) | 0.279 |
| S5-64 | Postgres aktiv (Verbindungen) | 8.1 |
| S5-64 | davon auf CPU | 3.5 |
| S5-64 | davon WAL (Sync, Write, Insert) | 1.85 |
| S5-64 | davon Sperren (Lock) | 0 |
| S5-64 | idle in transaction | 0.95 |
| S5-64 | WAL-Syncs (/s) | 789 |
| S5-64 | Npgsql belegt (Mittel) | 46 |
| S5-64 | Threadpool-Warteschlange (höchst) | 32 |

# Engpass-Sonde — 2026-10-17_2215_83f0375_server-sonde-profil-reads-sql

Commit `83f0375` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## S5-1-w1 (9.0 s)

- CPU: host-pruefdomaene 3.83, pg-backend 1.3, pg-sonst 1.03, treiber 0.27, pg-backend-treiber 0.08, sonst 0.07, cqrs-vermessung-redis 0.06, python 0.04
- Rechner: 33.6 % ausgelastet, iowait 2.5 %, CPU-Druck 0.014, IO-Druck 0.04, Kerne über 90 %: 0.2, P-Kerne 30.7 %, E-Kerne 37.9 %; je Kern [26, 32, 29, 25, 32, 24, 26, 50, 35, 27, 37, 26, 47, 44, 43, 38, 34, 33, 31, 33]
- Host-Threads über 5 %: 27, höchster Einzelwert 97.7 %; heißeste: .NET Tiered Com 41.7 %, .NET TP Worker 24.3 %, .NET TP Worker 22.8 %, .NET TP Worker 22.0 %, .NET TP Worker 21.0 %, .NET TP Worker 20.2 %
- Postgres: 59.0 Verbindungen, davon aktiv 3.9, idle in transaction 0.27; idle 54.83, active CPU 1.22, active LWLock:WALWrite 1.02, active Client:ClientRead 0.84, active IO:WALSync 0.8, idle in transaction 0.27, active IO:DataFileExtend 0.01
  - 1.92 × active: `COMMIT`
  - 0.8 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.41 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.34 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.17 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.1 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.08 × active: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
  - 0.06 × idle in transaction: `delete from es.mt_doc_prozessoffen as d where id = $1`
- WAL je s: 22311.8 Records, 2.7 MB, 1536.6 Writes, 1536.1 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 1.778, höchst 7.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 51.222, höchst 63.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 6.111, höchst 8.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.019, höchst 0.026; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 76.667, höchst 107.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.222, höchst 2.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.014, höchst 0.032; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-8-w1 (3.0 s)

- CPU: host-pruefdomaene 4.98, pg-backend 2.95, pg-sonst 1.59, treiber 0.33, pg-backend-treiber 0.13, sonst 0.09, cqrs-vermessung-redis 0.08, python 0.03
- Rechner: 53.2 % ausgelastet, iowait 1.4 %, CPU-Druck 0.162, IO-Druck 0.028, Kerne über 90 %: 0.0, P-Kerne 51.1 %, E-Kerne 56.3 %; je Kern [49, 48, 45, 55, 50, 52, 51, 52, 51, 56, 54, 50, 61, 62, 58, 59, 51, 53, 53, 54]
- Host-Threads über 5 %: 30, höchster Einzelwert 42.5 %; heißeste: .NET Tiered Com 28.4 %, .NET TP Worker 23.8 %, .NET TP Worker 23.5 %, .NET TP Worker 23.2 %, .NET TP Worker 22.5 %, .NET TP Worker 22.2 %
- Postgres: 75.1 Verbindungen, davon aktiv 5.83, idle in transaction 0.53; idle 68.7, active CPU 2.13, active Client:ClientRead 2.13, active LWLock:WALWrite 0.73, active IO:WALSync 0.7, idle in transaction 0.53, active LWLock:ProcArray 0.07, active IO:DataFileExtend 0.03, active IO:WALWrite 0.03
  - 2.0 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 1.9 × active: `COMMIT`
  - 0.6 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.43 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.37 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.2 × active: `select d.id, d.data from es.mt_doc_snapshot_auftrag as d where id = $1`
  - 0.17 × idle in transaction: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
  - 0.13 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
- WAL je s: 52612.1 Records, 7.0 MB, 1146.8 Writes, 1145.3 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 11.0, höchst 20.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 68.667, höchst 82.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 17.333, höchst 24.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.072, höchst 0.077; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 306.0, höchst 336.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel -0.333, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.057, höchst 0.08; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 3.0, höchst 9.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-64-w1 (2.0 s)

- CPU: host-pruefdomaene 4.7, pg-backend 4.28, pg-sonst 1.61, treiber 0.51, pg-backend-treiber 0.17, sonst 0.12, cqrs-vermessung-redis 0.07, python 0.03, consul 0.01
- Rechner: 60.0 % ausgelastet, iowait 0.8 %, CPU-Druck 0.279, IO-Druck 0.019, Kerne über 90 %: 0.0, P-Kerne 58.9 %, E-Kerne 61.4 %; je Kern [64, 70, 56, 63, 55, 54, 57, 61, 60, 56, 57, 56, 63, 66, 67, 66, 57, 57, 57, 59]
- Host-Threads über 5 %: 39, höchster Einzelwert 46.7 %; heißeste: .NET Tiered Com 36.8 %, .NET TP Worker 20.9 %, .NET TP Worker 18.9 %, .NET TP Worker 18.4 %, .NET TP Worker 17.4 %, .NET TP Worker 17.4 %
- Postgres: 96.7 Verbindungen, davon aktiv 8.1, idle in transaction 0.95; idle 87.65, active CPU 3.5, active Client:ClientRead 2.65, active LWLock:WALWrite 1.3, idle in transaction 0.95, active IO:WALSync 0.5, active IPC:XactGroupUpdate 0.05, active LWLock:XactSLRU 0.05, active IO:WALWrite 0.05
  - 3.1 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 2.4 × active: `COMMIT`
  - 1.1 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.65 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.6 × idle in transaction: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
  - 0.35 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.2 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.2 × active: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
- WAL je s: 94048.0 Records, 13.1 MB, 794.2 Writes, 789.2 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 42.0, höchst 85.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 57.5, höchst 115.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 54.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 46.0, höchst 92.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.077, höchst 0.13; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 259.0, höchst 403.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel -0.5, höchst 32.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.069, höchst 0.094; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 9.0, höchst 18.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## L5-nachlauf-w1 (17.0 s)

- CPU: host-pruefdomaene 0.49, pg-backend 0.29, sonst 0.28, pg-sonst 0.09, pg-backend-treiber 0.08, treiber 0.07, python 0.04, consul 0.02, cqrs-vermessung-redis 0.01
- Rechner: 7.2 % ausgelastet, iowait 0.1 %, CPU-Druck 0.015, IO-Druck 0.001, Kerne über 90 %: 0.1, P-Kerne 9.3 %, E-Kerne 4.1 %; je Kern [6, 11, 10, 6, 10, 8, 27, 3, 9, 6, 11, 4, 5, 5, 5, 4, 4, 3, 4, 3]
- Host-Threads über 5 %: 38, höchster Einzelwert 35.9 %; heißeste: .NET EventPipe 11.1 %, .NET Tiered Com 3.3 %, .NET TP Worker 2.9 %, .NET TP Worker 2.1 %, .NET EventPipe 2.1 %, .NET TP Worker 1.9 %
- Postgres: 103.0 Verbindungen, davon aktiv 0.19, idle in transaction 0; idle 102.81, active CPU 0.16, active Client:ClientRead 0.02, active IO:WALSync 0.02
  - 0.09 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
- WAL je s: 2044.7 Records, 0.3 MB, 33.2 Writes, 33.0 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel -0.882, höchst 0.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 99.882, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 0.118, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.001, höchst 0.014; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.009, höchst 0.045; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.235, höchst 0.0

## L5-w1 (61.1 s)

- CPU: pg-backend 0.06, sonst 0.05, python 0.04, host-pruefdomaene 0.02, consul 0.02, treiber 0.01, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.4 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.8 %, E-Kerne 0.9 %; je Kern [1, 2, 3, 1, 3, 0, 4, 0, 3, 1, 2, 1, 1, 1, 1, 1, 0, 1, 1, 0]
- Host-Threads über 5 %: 2, höchster Einzelwert 22.7 %; heißeste: .NET TP Worker 0.4 %, .NET TP Worker 0.4 %
- Postgres: 103.0 Verbindungen, davon aktiv 0.0, idle in transaction 0; idle 103.0
- WAL je s: 0.6 Records, 0.0 MB, 0.2 Writes, 0.2 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel -1.0, höchst -1.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=idle]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 0.983, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.017, höchst 1.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.267, höchst 13.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.467, höchst 9.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.0; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.213, höchst 13.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.049, höchst 12.0

