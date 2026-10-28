## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-28_1654) |
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
| S5-8 | Durchsatz bis sichtbar (/s) | 255 |
| S5-8 | angenommen (/s) | 1.59e+03 |
| S5-8 | sichtbar im Fenster (/s) | 254 |
| S5-8 | Latenz p50 (ms) | 6.48e+03 |
| S5-8 | CPU Host (Kerne) | 4.5 |
| S5-8 | CPU Postgres-Backends (Kerne) | 2.52 |
| S5-8 | Rechner ausgelastet (%) | 37.6 |
| S5-8 | CPU-Druck (Anteil) | 0.025 |
| S5-8 | Postgres aktiv (Verbindungen) | 3.24 |
| S5-8 | davon auf CPU | 1.6 |
| S5-8 | davon WAL (Sync, Write, Insert) | 0.97 |
| S5-8 | davon Sperren (Lock) | 0 |
| S5-8 | idle in transaction | 0.21 |
| S5-8 | WAL-Syncs (/s) | 878 |
| S5-8 | Npgsql belegt (Mittel) | 5.67 |
| S5-8 | Threadpool-Warteschlange (höchst) | 1 |
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

# Engpass-Sonde — 2026-10-28_1654_a7c94af_server-pf36-sonde-b

Commit `a7c94af` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## S5-8-w1 (12.0 s)

- CPU: host-pruefdomaene 4.5, pg-backend 2.52, treiber 0.23, pg-backend-treiber 0.06, sonst 0.05, python 0.03
- Rechner: 37.6 % ausgelastet, iowait 1.6 %, CPU-Druck 0.025, IO-Druck 0.027, Kerne über 90 %: 0.0, P-Kerne 35.2 %, E-Kerne 41.1 %; je Kern [31, 38, 33, 39, 40, 31, 42, 29, 41, 29, 41, 28, 50, 51, 47, 44, 33, 35, 34, 34]
- Host-Threads über 5 %: 25, höchster Einzelwert 85.5 %; heißeste: .NET Tiered Com 42.3 %, .NET TP Worker 25.7 %, .NET TP Worker 25.5 %, .NET TP Worker 25.1 %, .NET TP Worker 25.0 %, .NET TP Worker 24.1 %
- Postgres: 77.0 Verbindungen, davon aktiv 3.24, idle in transaction 0.21; idle 73.55, active CPU 1.6, active Client:ClientRead 0.67, active IO:WALSync 0.55, active LWLock:WALWrite 0.42, idle in transaction 0.21
  - 1.6 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 1.12 × active: `COMMIT`
  - 0.19 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.11 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.1 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.09 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
  - 0.07 × active: `select pg_current_xact_id()::text, pg_snapshot_xmin(pg_current_snapsho`
- WAL je s: 17869.1 Records, 2.2 MB, 925.9 Writes, 877.5 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 3.833, höchst 9.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 68.333, höchst 74.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 5.667, höchst 13.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.027, höchst 0.05; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 3.5, höchst 22.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.019, höchst 0.036; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.583, höchst 7.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## L5-nachlauf-w1 (18.0 s)

- CPU: sonst 0.27, host-pruefdomaene 0.11, pg-backend 0.08, python 0.04, treiber 0.04, pg-backend-treiber 0.03, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 3.3 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.001, Kerne über 90 %: 0.2, P-Kerne 4.6 %, E-Kerne 1.2 %; je Kern [3, 2, 4, 3, 6, 0, 6, 0, 19, 9, 5, 0, 2, 1, 1, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 8, höchster Einzelwert 34.6 %; heißeste: .NET EventPipe 3.9 %, .NET EventPipe 1.9 %, .NET TP Worker 1.4 %, .NET TP Worker 1.1 %, .NET TP Worker 1.0 %, .NET TP Worker 0.9 %
- Postgres: 77.0 Verbindungen, davon aktiv 0.14, idle in transaction 0; idle 76.86, active CPU 0.1, active Client:ClientRead 0.04
  - 0.1 × active: `select pg_current_xact_id()::text, pg_snapshot_xmin(pg_current_snapsho`
- WAL je s: 122.3 Records, 0.0 MB, 10.4 Writes, 10.3 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 74.0, höchst 74.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.056, höchst 1.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -1.167, höchst 2.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.002, höchst 0.017; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -1.0, höchst 0.0

## L5-w1 (61.1 s)

- CPU: pg-backend 0.06, sonst 0.05, python 0.04, consul 0.02, treiber 0.02, host-pruefdomaene 0.01, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.3 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.7 %, E-Kerne 0.9 %; je Kern [2, 0, 3, 0, 3, 0, 4, 0, 3, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 0, höchster Einzelwert 0 %; heißeste: 
- Postgres: 77.0 Verbindungen, davon aktiv 0.0, idle in transaction 0; idle 77.0
- WAL je s: 8.1 Records, 0.0 MB, 0.5 Writes, 0.5 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.148, höchst 9.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 73.18, höchst 74.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.82, höchst 50.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.002; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.836, höchst 44.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.016, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.131, höchst 6.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.003; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.311, höchst 19.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.246, höchst 16.0

