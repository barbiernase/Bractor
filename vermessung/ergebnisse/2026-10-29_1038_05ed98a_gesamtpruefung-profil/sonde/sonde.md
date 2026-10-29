## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-29_1038) |
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
| S5-1 | Durchsatz bis sichtbar (/s) | 433 |
| S5-1 | angenommen (/s) | 433 |
| S5-1 | sichtbar im Fenster (/s) | 427 |
| S5-1 | Latenz p50 (ms) | 7.13 |
| S5-1 | CPU Host (Kerne) | 3.34 |
| S5-1 | CPU Postgres-Backends (Kerne) | 1.91 |
| S5-1 | Rechner ausgelastet (%) | 28.1 |
| S5-1 | CPU-Druck (Anteil) | 0.008 |
| S5-1 | Postgres aktiv (Verbindungen) | 3.02 |
| S5-1 | davon auf CPU | 0.26 |
| S5-1 | davon WAL (Sync, Write, Insert) | 1.46 |
| S5-1 | davon Sperren (Lock) | 0 |
| S5-1 | idle in transaction | 0.14 |
| S5-1 | WAL-Syncs (/s) | 1.39e+03 |
| S5-1 | Npgsql belegt (Mittel) | 4.62 |
| S5-1 | Threadpool-Warteschlange (höchst) | 1 |
| S5-8 | Durchsatz bis sichtbar (/s) | 305 |
| S5-8 | angenommen (/s) | 2.08e+03 |
| S5-8 | sichtbar im Fenster (/s) | 423 |
| S5-8 | Latenz p50 (ms) | 3.98e+03 |
| S5-8 | CPU Host (Kerne) | 2.63 |
| S5-8 | CPU Postgres-Backends (Kerne) | 2.74 |
| S5-8 | Rechner ausgelastet (%) | 29.1 |
| S5-8 | CPU-Druck (Anteil) | 0.02 |
| S5-8 | Postgres aktiv (Verbindungen) | 3.34 |
| S5-8 | davon auf CPU | 1.63 |
| S5-8 | davon WAL (Sync, Write, Insert) | 0.99 |
| S5-8 | davon Sperren (Lock) | 0 |
| S5-8 | idle in transaction | 0.11 |
| S5-8 | WAL-Syncs (/s) | 941 |
| S5-8 | Npgsql belegt (Mittel) | 4 |
| S5-8 | Threadpool-Warteschlange (höchst) | 1 |
| S5-64 | Durchsatz bis sichtbar (/s) | 248 |
| S5-64 | angenommen (/s) | 8.38e+03 |
| S5-64 | sichtbar im Fenster (/s) | 36.3 |
| S5-64 | Latenz p50 (ms) | 6.04e+03 |
| S5-64 | CPU Host (Kerne) | 2.44 |
| S5-64 | CPU Postgres-Backends (Kerne) | 2.99 |
| S5-64 | Rechner ausgelastet (%) | 29.1 |
| S5-64 | CPU-Druck (Anteil) | 0.026 |
| S5-64 | Postgres aktiv (Verbindungen) | 3.52 |
| S5-64 | davon auf CPU | 2.3 |
| S5-64 | davon WAL (Sync, Write, Insert) | 0.64 |
| S5-64 | davon Sperren (Lock) | 0 |
| S5-64 | idle in transaction | 0.09 |
| S5-64 | WAL-Syncs (/s) | 688 |
| S5-64 | Npgsql belegt (Mittel) | 5.64 |
| S5-64 | Threadpool-Warteschlange (höchst) | 3 |

# Engpass-Sonde — 2026-10-29_1038_05ed98a_gesamtpruefung-profil

Commit `05ed98a` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## L1-nachlauf-w1 (181.4 s)

- CPU: sonst 0.07, pg-backend 0.06, python 0.04, host-beispiel 0.03, treiber 0.02, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.6 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 2.0 %, E-Kerne 1.0 %; je Kern [3, 0, 3, 1, 4, 1, 3, 1, 4, 0, 4, 0, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 2, höchster Einzelwert 9.0 %; heißeste: .NET Tiered Com 0.1 %, .NET EventPipe 0.0 %
- Postgres: 99.0 Verbindungen, davon aktiv 0.03, idle in transaction 0.0; idle 98.97, active CPU 0.03
- WAL je s: 117.0 Records, 0.0 MB, 2.0 Writes, 2.0 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 96.0, höchst 96.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 1.0, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.012; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.363, höchst 26.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel -0.005, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.11, höchst 8.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.001, höchst 0.046; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 14.28, höchst 2593.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel -0.005, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.209, höchst 3.0

## L1-w1 (96.2 s)

- CPU: host-pruefdomaene 0.29, pg-backend 0.07, sonst 0.06, host-beispiel 0.04, python 0.04, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01, treiber 0.01
- Rechner: 2.1 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 2.5 %, E-Kerne 1.6 %; je Kern [2, 1, 5, 1, 5, 2, 4, 1, 3, 1, 4, 1, 2, 2, 2, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 2, höchster Einzelwert 5.9 %; heißeste: .NET TP Worker 0.1 %, .NET TP Worker 0.1 %
- Postgres: 93.8 Verbindungen, davon aktiv 0.03, idle in transaction 0.01; idle 93.72, active CPU 0.02
- WAL je s: 90.9 Records, 0.1 MB, 2.2 Writes, 1.9 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 96.0, höchst 96.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 1.0, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.006; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.048, höchst 2.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.081, höchst 6.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.003; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.156, höchst 16.0

## S5-1-w1 (8.0 s)

- CPU: host-pruefdomaene 3.34, pg-backend 1.91, treiber 0.16, pg-backend-treiber 0.08, sonst 0.07, python 0.03, consul 0.01
- Rechner: 28.1 % ausgelastet, iowait 2.4 %, CPU-Druck 0.008, IO-Druck 0.034, Kerne über 90 %: 0.2, P-Kerne 27.0 %, E-Kerne 29.8 %; je Kern [22, 25, 26, 23, 36, 21, 30, 28, 24, 41, 32, 16, 38, 34, 33, 31, 27, 26, 26, 24]
- Host-Threads über 5 %: 20, höchster Einzelwert 99.3 %; heißeste: .NET Tiered Com 50.8 %, .NET TP Worker 25.0 %, .NET TP Worker 24.0 %, .NET TP Worker 23.2 %, .NET TP Worker 22.8 %, .NET TP Worker 22.6 %
- Postgres: 103.0 Verbindungen, davon aktiv 3.02, idle in transaction 0.14; idle 99.84, active Client:ClientRead 1.29, active LWLock:WALWrite 0.79, active IO:WALSync 0.66, active CPU 0.26, idle in transaction 0.14, active IO:DataFileExtend 0.01, active IO:WALWrite 0.01
  - 1.62 × active: `COMMIT`
  - 0.48 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.3 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
  - 0.28 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.14 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.14 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.09 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
- WAL je s: 25858.6 Records, 3.2 MB, 1461.8 Writes, 1392.2 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 1.75, höchst 5.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 95.375, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 4.625, höchst 9.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.012, höchst 0.018; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 1.125, höchst 8.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.018, höchst 0.036; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.375, höchst 3.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-8-w1 (11.0 s)

- CPU: pg-backend 2.74, host-pruefdomaene 2.63, treiber 0.16, pg-backend-treiber 0.12, sonst 0.07, python 0.03, consul 0.01
- Rechner: 29.1 % ausgelastet, iowait 1.4 %, CPU-Druck 0.02, IO-Druck 0.025, Kerne über 90 %: 0.0, P-Kerne 28.0 %, E-Kerne 30.7 %; je Kern [26, 31, 32, 22, 32, 29, 33, 22, 35, 17, 32, 25, 39, 35, 34, 35, 25, 26, 27, 25]
- Host-Threads über 5 %: 23, höchster Einzelwert 43.8 %; heißeste: .NET TP Worker 25.1 %, .NET TP Worker 23.6 %, .NET TP Worker 23.4 %, .NET TP Worker 20.3 %, .NET TP Worker 16.0 %, .NET TP Worker 14.4 %
- Postgres: 103.0 Verbindungen, davon aktiv 3.34, idle in transaction 0.11; idle 99.55, active CPU 1.63, active Client:ClientRead 0.71, active LWLock:WALWrite 0.52, active IO:WALSync 0.47, idle in transaction 0.11
  - 1.74 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 1.07 × active: `COMMIT`
  - 0.15 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.13 × active: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.1 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.09 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
- WAL je s: 21430.0 Records, 2.8 MB, 988.5 Writes, 941.2 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 3.636, höchst 8.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 96.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 4.0, höchst 8.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.024, höchst 0.072; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 2.273, höchst 13.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.028, höchst 0.06; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.545, höchst 3.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 0.0

## S5-64-w1 (14.0 s)

- CPU: pg-backend 2.99, host-pruefdomaene 2.44, treiber 0.12, pg-backend-treiber 0.09, sonst 0.06, python 0.03, consul 0.01
- Rechner: 29.1 % ausgelastet, iowait 1.1 %, CPU-Druck 0.026, IO-Druck 0.02, Kerne über 90 %: 0.0, P-Kerne 26.5 %, E-Kerne 33.2 %; je Kern [30, 24, 32, 22, 31, 19, 32, 22, 34, 19, 34, 19, 39, 39, 37, 37, 27, 27, 29, 30]
- Host-Threads über 5 %: 25, höchster Einzelwert 46.6 %; heißeste: .NET TP Worker 21.4 %, .NET TP Worker 19.5 %, .NET TP Worker 19.4 %, .NET TP Worker 19.0 %, .NET TP Worker 18.1 %, .NET TP Worker 17.9 %
- Postgres: 103.0 Verbindungen, davon aktiv 3.52, idle in transaction 0.09; idle 99.39, active CPU 2.3, active Client:ClientRead 0.57, active IO:WALSync 0.34, active LWLock:WALWrite 0.3, idle in transaction 0.09
  - 2.41 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.76 × active: `COMMIT`
  - 0.14 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.09 × active: `select pg_snapshot_xmin(pg_current_snapshot())::text, (select seq_id f`
  - 0.08 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.06 × active: `select pg_current_xact_id()::text, pg_snapshot_xmin(pg_current_snapsho`
- WAL je s: 15526.8 Records, 2.0 MB, 719.8 Writes, 688.0 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 4.5, höchst 14.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 94.357, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 5.643, höchst 18.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.02, höchst 0.035; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 1.357, höchst 7.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 3.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.786, höchst 0.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.02, höchst 0.043; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.214, höchst 2.0

## L5-nachlauf-w1 (57.1 s)

- CPU: sonst 0.1, python 0.07, pg-backend 0.05, host-pruefdomaene 0.03, consul 0.02, treiber 0.02, cqrs-redis 0.01, cqrs-vermessung-redis 0.01, pg-backend-treiber 0.01
- Rechner: 1.9 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.1, P-Kerne 2.5 %, E-Kerne 0.9 %; je Kern [4, 0, 4, 0, 3, 0, 9, 0, 4, 0, 4, 0, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 3, höchster Einzelwert 30.8 %; heißeste: .NET EventPipe 0.5 %, .NET EventPipe 0.4 %, .NET Tiered Com 0.1 %
- Postgres: 103.0 Verbindungen, davon aktiv 0, idle in transaction 0; idle 103.0
- WAL je s: 22.5 Records, 0.0 MB, 1.5 Writes, 1.5 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.439, höchst 19.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.14, höchst 12.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.006; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.018, höchst 1.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.14, höchst 4.0

## L5-w1 (63.1 s)

- CPU: pg-backend 0.1, python 0.08, host-pruefdomaene 0.06, sonst 0.05, consul 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01, treiber 0.01
- Rechner: 2.0 % ausgelastet, iowait 0.0 %, CPU-Druck 0.004, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 2.3 %, E-Kerne 1.4 %; je Kern [2, 1, 4, 1, 4, 1, 4, 1, 5, 1, 4, 2, 2, 2, 2, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 7, höchster Einzelwert 8.9 %; heißeste: .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %
- Postgres: 103.0 Verbindungen, davon aktiv 0.15, idle in transaction 0.01; idle 102.84, active LWLock:WALWrite 0.12, active Client:ClientRead 0.01, idle in transaction 0.01
  - 0.14 × active: `COMMIT`
- WAL je s: 8.4 Records, 0.0 MB, 0.4 Writes, 0.4 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 99.968, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.032, höchst 2.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.016; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 4.81, höchst 299.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.143, höchst 15.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.005; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.063, höchst 1.0

