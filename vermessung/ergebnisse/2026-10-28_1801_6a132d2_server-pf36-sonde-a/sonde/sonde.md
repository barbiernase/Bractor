## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-28_1801) |
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
| S5-8 | Durchsatz bis sichtbar (/s) | 220 |
| S5-8 | angenommen (/s) | 1.81e+03 |
| S5-8 | sichtbar im Fenster (/s) | 332 |
| S5-8 | Latenz p50 (ms) | 6.34e+03 |
| S5-8 | CPU Host (Kerne) | 4.36 |
| S5-8 | CPU Postgres-Backends (Kerne) | 2.25 |
| S5-8 | Rechner ausgelastet (%) | 35.6 |
| S5-8 | CPU-Druck (Anteil) | 0.027 |
| S5-8 | Postgres aktiv (Verbindungen) | 2.76 |
| S5-8 | davon auf CPU | 1.46 |
| S5-8 | davon WAL (Sync, Write, Insert) | 0.74 |
| S5-8 | davon Sperren (Lock) | 0 |
| S5-8 | idle in transaction | 0.16 |
| S5-8 | WAL-Syncs (/s) | 764 |
| S5-8 | Npgsql belegt (Mittel) | 7.07 |
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

# Engpass-Sonde — 2026-10-28_1801_6a132d2_server-pf36-sonde-a

Commit `6a132d2` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## S5-8-w1 (14.0 s)

- CPU: host-pruefdomaene 4.36, pg-backend 2.25, treiber 0.22, pg-backend-treiber 0.07, sonst 0.06, cqrs-vermessung-redis 0.04, python 0.04
- Rechner: 35.6 % ausgelastet, iowait 1.2 %, CPU-Druck 0.027, IO-Druck 0.022, Kerne über 90 %: 0.0, P-Kerne 32.9 %, E-Kerne 39.6 %; je Kern [30, 28, 31, 33, 37, 33, 38, 26, 36, 36, 38, 30, 46, 45, 47, 44, 32, 33, 33, 35]
- Host-Threads über 5 %: 26, höchster Einzelwert 94.6 %; heißeste: .NET Tiered Com 39.3 %, .NET TP Worker 23.7 %, .NET TP Worker 23.1 %, .NET TP Worker 22.8 %, .NET TP Worker 20.2 %, .NET TP Worker 20.0 %
- Postgres: 77.0 Verbindungen, davon aktiv 2.76, idle in transaction 0.16; idle 74.08, active CPU 1.46, active Client:ClientRead 0.54, active IO:WALSync 0.42, active LWLock:WALWrite 0.32, idle in transaction 0.16
  - 1.48 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.81 × active: `COMMIT`
  - 0.15 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.12 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.08 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.06 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
- WAL je s: 15235.9 Records, 1.9 MB, 799.0 Writes, 764.4 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 4.286, höchst 12.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 66.929, höchst 73.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 7.071, höchst 23.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.026, höchst 0.065; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 82.143, höchst 395.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.015, höchst 0.028; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.643, höchst 5.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## L5-nachlauf-w1 (16.0 s)

- CPU: sonst 0.32, host-pruefdomaene 0.11, pg-backend 0.07, python 0.04, treiber 0.03, pg-backend-treiber 0.03, consul 0.02, cqrs-vermessung-redis 0.01
- Rechner: 3.5 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.001, Kerne über 90 %: 0.2, P-Kerne 4.9 %, E-Kerne 1.4 %; je Kern [5, 1, 6, 0, 8, 0, 24, 3, 5, 0, 5, 2, 2, 2, 1, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 10, höchster Einzelwert 36.1 %; heißeste: .NET EventPipe 2.3 %, .NET EventPipe 2.0 %, .NET TP Worker 1.1 %, .NET TP Worker 1.0 %, .NET TP Worker 0.7 %, .NET TP Worker 0.7 %
- Postgres: 77.0 Verbindungen, davon aktiv 0.01, idle in transaction 0.01; idle 76.99
- WAL je s: 171.9 Records, 0.0 MB, 2.9 Writes, 2.7 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 74.0, höchst 74.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.005; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.188, höchst 3.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -1.75, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.001, höchst 0.01; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -1.0, höchst 0.0

## L5-w1 (61.1 s)

- CPU: pg-backend 0.06, sonst 0.05, python 0.04, consul 0.02, host-pruefdomaene 0.02, treiber 0.01, cqrs-redis 0.01, cqrs-vermessung-redis 0.01
- Rechner: 1.3 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.6 %, E-Kerne 0.9 %; je Kern [2, 0, 3, 0, 3, 0, 4, 0, 3, 0, 2, 0, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 0, höchster Einzelwert 0 %; heißeste: 
- Postgres: 77.0 Verbindungen, davon aktiv 0.0, idle in transaction 0; idle 77.0
- WAL je s: 7.5 Records, 0.0 MB, 0.5 Writes, 0.5 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.016, höchst 1.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 73.984, höchst 74.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.016, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.006; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.164, höchst 6.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.115, höchst 7.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.003; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.295, höchst 18.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.197, höchst 14.0

