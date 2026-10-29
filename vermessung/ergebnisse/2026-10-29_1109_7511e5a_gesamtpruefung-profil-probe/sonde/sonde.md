## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-29_1109) |
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
| S5-1 | Durchsatz bis sichtbar (/s) | 431 |
| S5-1 | angenommen (/s) | 432 |
| S5-1 | sichtbar im Fenster (/s) | 427 |
| S5-1 | Latenz p50 (ms) | 7.09 |
| S5-1 | CPU Host (Kerne) | 3.33 |
| S5-1 | CPU Postgres-Backends (Kerne) | 1.95 |
| S5-1 | Rechner ausgelastet (%) | 28.3 |
| S5-1 | CPU-Druck (Anteil) | 0.01 |
| S5-1 | Postgres aktiv (Verbindungen) | 2.77 |
| S5-1 | davon auf CPU | 0.2 |
| S5-1 | davon WAL (Sync, Write, Insert) | 1.48 |
| S5-1 | davon Sperren (Lock) | 0 |
| S5-1 | idle in transaction | 0.18 |
| S5-1 | WAL-Syncs (/s) | 1.36e+03 |
| S5-1 | Npgsql belegt (Mittel) | 5 |
| S5-1 | Threadpool-Warteschlange (höchst) | 0 |
| S5-8 | Durchsatz bis sichtbar (/s) | 298 |
| S5-8 | angenommen (/s) | 2.03e+03 |
| S5-8 | sichtbar im Fenster (/s) | 356 |
| S5-8 | Latenz p50 (ms) | 3.89e+03 |
| S5-8 | CPU Host (Kerne) | 2.67 |
| S5-8 | CPU Postgres-Backends (Kerne) | 2.87 |
| S5-8 | Rechner ausgelastet (%) | 29.9 |
| S5-8 | CPU-Druck (Anteil) | 0.023 |
| S5-8 | Postgres aktiv (Verbindungen) | 3.85 |
| S5-8 | davon auf CPU | 2.13 |
| S5-8 | davon WAL (Sync, Write, Insert) | 0.86 |
| S5-8 | davon Sperren (Lock) | 0 |
| S5-8 | idle in transaction | 0.09 |
| S5-8 | WAL-Syncs (/s) | 827 |
| S5-8 | Npgsql belegt (Mittel) | 6 |
| S5-8 | Threadpool-Warteschlange (höchst) | 0 |
| S5-64 | Durchsatz bis sichtbar (/s) | 269 |
| S5-64 | angenommen (/s) | 8.28e+03 |
| S5-64 | sichtbar im Fenster (/s) | 105 |
| S5-64 | Latenz p50 (ms) | 6.05e+03 |
| S5-64 | CPU Host (Kerne) | 2.74 |
| S5-64 | CPU Postgres-Backends (Kerne) | 3.35 |
| S5-64 | Rechner ausgelastet (%) | 32.6 |
| S5-64 | CPU-Druck (Anteil) | 0.028 |
| S5-64 | Postgres aktiv (Verbindungen) | 3.98 |
| S5-64 | davon auf CPU | 2.68 |
| S5-64 | davon WAL (Sync, Write, Insert) | 0.83 |
| S5-64 | davon Sperren (Lock) | 0 |
| S5-64 | idle in transaction | 0.09 |
| S5-64 | WAL-Syncs (/s) | 733 |
| S5-64 | Npgsql belegt (Mittel) | 5.15 |
| S5-64 | Threadpool-Warteschlange (höchst) | 8 |

# Engpass-Sonde — 2026-10-29_1109_7511e5a_gesamtpruefung-profil-probe

Commit `7511e5a` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## L1-nachlauf-w1 (181.4 s)

- CPU: sonst 0.08, pg-backend 0.06, treiber 0.04, python 0.04, host-beispiel 0.03, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.7 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 2.2 %, E-Kerne 1.0 %; je Kern [2, 1, 5, 0, 4, 0, 4, 1, 4, 2, 4, 0, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 2, höchster Einzelwert 25.9 %; heißeste: .NET EventPipe 0.2 %, .NET Tiered Com 0.0 %
- Postgres: 103.0 Verbindungen, davon aktiv 0.04, idle in transaction 0.0; idle 102.96, active CPU 0.04
- WAL je s: 95.2 Records, 0.0 MB, 1.0 Writes, 1.0 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 1.0, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.006; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.116, höchst 11.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.166, höchst 9.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.019; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.21, höchst 12.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 5.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.21, höchst 1.0

## L1-w1 (96.2 s)

- CPU: host-pruefdomaene 0.25, pg-backend 0.06, sonst 0.05, python 0.04, host-beispiel 0.03, consul 0.02, cqrs-redis 0.01, cqrs-vermessung-redis 0.01, treiber 0.01
- Rechner: 2.1 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.001, Kerne über 90 %: 0.0, P-Kerne 2.5 %, E-Kerne 1.5 %; je Kern [2, 1, 5, 1, 4, 1, 4, 1, 4, 1, 5, 2, 2, 2, 2, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 6, höchster Einzelwert 7.9 %; heißeste: .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET Tiered Com 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %
- Postgres: 90.5 Verbindungen, davon aktiv 0.08, idle in transaction 0.0; idle 90.43, active CPU 0.04, active LWLock:WALWrite 0.03
- WAL je s: 123.5 Records, 0.1 MB, 3.4 Writes, 3.0 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 91.21, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 0.742, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.001; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.065, höchst 4.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel 0.048, höchst 8.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.002; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.082, höchst 5.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.144, höchst 13.0

## S5-1-w1 (8.0 s)

- CPU: host-pruefdomaene 3.33, pg-backend 1.95, treiber 0.18, pg-backend-treiber 0.08, sonst 0.06, python 0.02, consul 0.01, pg-walwriter 0.01
- Rechner: 28.3 % ausgelastet, iowait 2.4 %, CPU-Druck 0.01, IO-Druck 0.035, Kerne über 90 %: 0.1, P-Kerne 26.2 %, E-Kerne 31.5 %; je Kern [21, 28, 27, 21, 28, 23, 31, 22, 22, 47, 31, 13, 38, 37, 36, 35, 27, 28, 25, 26]
- Host-Threads über 5 %: 20, höchster Einzelwert 98.7 %; heißeste: .NET Tiered Com 51.0 %, .NET TP Worker 25.0 %, .NET TP Worker 24.7 %, .NET TP Worker 23.9 %, .NET TP Worker 23.6 %, .NET TP Worker 23.5 %
- Postgres: 93.0 Verbindungen, davon aktiv 2.77, idle in transaction 0.18; idle 90.05, active Client:ClientRead 1.1, active LWLock:WALWrite 0.75, active IO:WALSync 0.7, active CPU 0.2, idle in transaction 0.18, active IO:WALWrite 0.03
  - 1.64 × active: `COMMIT`
  - 0.39 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.16 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.15 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
  - 0.15 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.09 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.09 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.05 × active: `BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED`
- WAL je s: 25306.4 Records, 3.2 MB, 1423.4 Writes, 1364.0 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 2.625, höchst 11.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 85.0, höchst 90.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 5.0, höchst 10.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.012, höchst 0.017; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 2.0, höchst 12.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.02, höchst 0.032; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-8-w1 (11.0 s)

- CPU: pg-backend 2.87, host-pruefdomaene 2.67, treiber 0.16, pg-backend-treiber 0.12, sonst 0.05, python 0.03, consul 0.01
- Rechner: 29.9 % ausgelastet, iowait 1.3 %, CPU-Druck 0.023, IO-Druck 0.025, Kerne über 90 %: 0.0, P-Kerne 28.1 %, E-Kerne 32.6 %; je Kern [28, 21, 32, 21, 36, 28, 34, 22, 33, 26, 34, 22, 40, 37, 37, 35, 28, 29, 27, 28]
- Host-Threads über 5 %: 25, höchster Einzelwert 41.7 %; heißeste: .NET TP Worker 24.5 %, .NET TP Worker 24.5 %, .NET TP Worker 21.5 %, .NET TP Worker 17.8 %, .NET TP Worker 17.7 %, .NET TP Worker 16.9 %
- Postgres: 93.0 Verbindungen, davon aktiv 3.85, idle in transaction 0.09; idle 89.06, active CPU 2.13, active Client:ClientRead 0.86, active IO:WALSync 0.42, active LWLock:WALWrite 0.42, idle in transaction 0.09, active IO:WALWrite 0.02
  - 2.25 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.92 × active: `COMMIT`
  - 0.18 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.12 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.11 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
  - 0.05 × active: `select pg_current_xact_id()::text, pg_snapshot_xmin(pg_current_snapsho`
  - 0.05 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
- WAL je s: 19466.6 Records, 2.5 MB, 867.4 Writes, 826.7 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 4.091, höchst 11.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 84.0, höchst 90.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 6.0, höchst 17.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.025, höchst 0.084; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 3.091, höchst 21.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.909, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.027, höchst 0.048; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.636, höchst 7.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-64-w1 (13.0 s)

- CPU: pg-backend 3.35, host-pruefdomaene 2.74, treiber 0.13, pg-backend-treiber 0.09, sonst 0.08, python 0.03, consul 0.01
- Rechner: 32.6 % ausgelastet, iowait 1.2 %, CPU-Druck 0.028, IO-Druck 0.023, Kerne über 90 %: 0.0, P-Kerne 29.1 %, E-Kerne 38.0 %; je Kern [30, 26, 35, 23, 34, 20, 38, 20, 34, 28, 39, 22, 44, 45, 43, 42, 32, 32, 33, 33]
- Host-Threads über 5 %: 24, höchster Einzelwert 42.6 %; heißeste: .NET TP Worker 22.5 %, .NET TP Worker 22.0 %, .NET TP Worker 21.0 %, .NET TP Worker 20.8 %, .NET TP Worker 20.4 %, .NET TP Worker 18.5 %
- Postgres: 93.0 Verbindungen, davon aktiv 3.98, idle in transaction 0.09; idle 88.93, active CPU 2.68, active Client:ClientRead 0.47, active LWLock:WALWrite 0.42, active IO:WALSync 0.41, idle in transaction 0.09
  - 2.65 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.85 × active: `COMMIT`
  - 0.15 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.08 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.08 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.05 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
- WAL je s: 16764.4 Records, 2.1 MB, 765.9 Writes, 733.4 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 4.846, höchst 17.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 84.846, höchst 90.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 5.154, höchst 17.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.02, höchst 0.042; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 1.077, höchst 5.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 8.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.154, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.024, höchst 0.051; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.154, höchst 2.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.154, höchst 0.0

## L5-nachlauf-w1 (58.1 s)

- CPU: sonst 0.1, pg-backend 0.05, python 0.04, host-pruefdomaene 0.03, consul 0.02, treiber 0.02, cqrs-redis 0.01, cqrs-vermessung-redis 0.01, pg-backend-treiber 0.01
- Rechner: 1.7 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 2.2 %, E-Kerne 1.0 %; je Kern [2, 0, 4, 1, 4, 0, 5, 1, 3, 0, 3, 1, 2, 2, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 2, höchster Einzelwert 35.7 %; heißeste: .NET EventPipe 0.6 %, .NET EventPipe 0.2 %
- Postgres: 93.0 Verbindungen, davon aktiv 0.02, idle in transaction 0.0; idle 92.98, active CPU 0.01, active Client:ClientRead 0.01
- WAL je s: 27.3 Records, 0.0 MB, 1.7 Writes, 1.7 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 90.0, höchst 90.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.004; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.345, höchst 10.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.155, höchst 16.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.005; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.052, höchst 3.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.138, höchst 4.0

## L5-w1 (63.1 s)

- CPU: pg-backend 0.1, host-pruefdomaene 0.06, sonst 0.05, python 0.04, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01, treiber 0.01
- Rechner: 1.8 % ausgelastet, iowait 0.0 %, CPU-Druck 0.004, IO-Druck 0.001, Kerne über 90 %: 0.0, P-Kerne 2.0 %, E-Kerne 1.5 %; je Kern [2, 1, 4, 1, 3, 1, 3, 0, 4, 1, 3, 1, 2, 2, 2, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 0, höchster Einzelwert 0 %; heißeste: 
- Postgres: 93.0 Verbindungen, davon aktiv 0.12, idle in transaction 0.02; idle 92.85, active LWLock:WALWrite 0.07, active Client:ClientRead 0.03, idle in transaction 0.02, active CPU 0.02
  - 0.12 × active: `COMMIT`
- WAL je s: 3.4 Records, 0.0 MB, 0.1 Writes, 0.1 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 89.677, höchst 90.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.323, höchst 20.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.007; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 6.403, höchst 394.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.145, höchst 12.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.003; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.063, höchst 1.0

