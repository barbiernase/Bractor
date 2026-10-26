## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-26_0545) |
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
| S5-64 | Durchsatz bis sichtbar (/s) | 1.89e+03 |
| S5-64 | angenommen (/s) | 2.48e+03 |
| S5-64 | sichtbar im Fenster (/s) | 2.32e+03 |
| S5-64 | Latenz p50 (ms) | 187 |
| S5-64 | CPU Host (Kerne) | 5.55 |
| S5-64 | CPU Postgres-Backends (Kerne) | 5.77 |
| S5-64 | Rechner ausgelastet (%) | 64.7 |
| S5-64 | CPU-Druck (Anteil) | 0.345 |
| S5-64 | Postgres aktiv (Verbindungen) | 8.22 |
| S5-64 | davon auf CPU | 2.7 |
| S5-64 | davon WAL (Sync, Write, Insert) | 2.38 |
| S5-64 | davon Sperren (Lock) | 0 |
| S5-64 | idle in transaction | 1.48 |
| S5-64 | WAL-Syncs (/s) | 710 |
| S5-64 | Npgsql belegt (Mittel) | 40.4 |
| S5-64 | Threadpool-Warteschlange (höchst) | 204 |

# Engpass-Sonde — 2026-10-26_0545_b1af87e_server-sonde-profil-pubsub-basis

Commit `b1af87e` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## S5-64-w1 (163.6 s)

- CPU: pg-backend 5.77, host-pruefdomaene 5.55, treiber 0.5, sonst 0.28, pg-backend-treiber 0.26, cqrs-vermessung-redis 0.08, pg-sonst 0.04, python 0.03, consul 0.01
- Rechner: 64.7 % ausgelastet, iowait 0.8 %, CPU-Druck 0.345, IO-Druck 0.022, Kerne über 90 %: 1.0, P-Kerne 64.0 %, E-Kerne 65.7 %; je Kern [62, 64, 65, 63, 65, 62, 65, 63, 66, 63, 67, 63, 69, 68, 68, 68, 63, 63, 63, 63]
- Host-Threads über 5 %: 83, höchster Einzelwert 67.4 %; heißeste: .NET TP Worker 17.8 %, .NET TP Worker 16.7 %, .NET TP Worker 16.5 %, .NET TP Worker 16.3 %, .NET TP Worker 16.1 %, .NET TP Worker 16.1 %
- Postgres: 87.6 Verbindungen, davon aktiv 8.22, idle in transaction 1.48; idle 77.91, active Client:ClientRead 2.96, active CPU 2.7, active LWLock:WALWrite 1.87, idle in transaction 1.48, active IO:WALSync 0.51, active IO:DataFileWrite 0.04, active IO:DataFileRead 0.02, active Timeout:SpinDelay 0.02, active LWLock:BufferMapping 0.02, active IO:DataFileExtend 0.01, active LWLock:BufferContent 0.01
  - 2.94 × active: `COMMIT`
  - 2.16 × active: `select coalesce((select k.version from es.mt_events k where k.stream_i`
  - 1.04 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
  - 0.91 × idle in transaction: `select es.mt_upsert_emittentencursordoc($1,$2,$3,$4)`
  - 0.86 × active: `insert into es.mt_events (data, type, mt_dotnet_type, id, stream_id, v`
  - 0.32 × active: `update es.mt_streams set version = $1 where id = $2 and version = $3 r`
  - 0.28 × idle in transaction: `insert into es.cqrs_inbox (command_id, stream_id, abgelehnt) select * `
  - 0.18 × active: `select es.mt_upsert_prozessoffen($1,$2,$3,$4)`
- WAL je s: 104541.2 Records, 16.2 MB, 714.7 Writes, 709.6 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 32.914, höchst 100.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 13.914, höchst 243.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 44.644, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 40.448, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.036, höchst 0.14; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 783.681, höchst 1501.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 204.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.147, höchst 4.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.099, höchst 0.182; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.675, höchst 34.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.104, höchst 1.0

## L5-nachlauf-w1 (52.1 s)

- CPU: host-pruefdomaene 0.49, pg-backend 0.23, sonst 0.04, pg-sonst 0.04, python 0.03, pg-backend-treiber 0.02, consul 0.02, treiber 0.01
- Rechner: 10.4 % ausgelastet, iowait 0.2 %, CPU-Druck 0.005, IO-Druck 0.004, Kerne über 90 %: 0.0, P-Kerne 10.0 %, E-Kerne 11.2 %; je Kern [14, 4, 16, 4, 17, 3, 17, 2, 18, 2, 17, 7, 21, 16, 11, 10, 8, 10, 7, 8]
- Host-Threads über 5 %: 21, höchster Einzelwert 41.7 %; heißeste: .NET TP Worker 9.3 %, .NET TP Worker 7.4 %, .NET TP Worker 6.7 %, .NET TP Worker 6.1 %, .NET TP Worker 4.9 %, .NET TP Worker 1.5 %
- Postgres: 13.0 Verbindungen, davon aktiv 0.85, idle in transaction 0.0; idle 12.15, active IPC:MessageQueueInternal 0.33, active IPC:BgWorkerShutdown 0.18, active CPU 0.14, active IPC:MessageQueueReceive 0.12, active IO:WALSync 0.04, active Client:ClientRead 0.03
  - 0.74 × active: `select data, type, id, stream_id, version, timestamp, correlation_id, `
- WAL je s: 337.6 Records, 0.0 MB, 77.9 Writes, 77.4 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.885, höchst 1.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 9.135, höchst 10.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.865, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.0, höchst 1.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.002, höchst 0.01; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 1.038, höchst 9.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel -0.115, höchst 12.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.004; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.346, höchst 18.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.038, höchst 16.0

## L5-w1 (73.2 s)

- CPU: host-pruefdomaene 1.07, pg-backend 0.79, pg-backend-treiber 0.06, sonst 0.05, python 0.03, consul 0.02, treiber 0.01, cqrs-redis 0.01, cqrs-vermessung-redis 0.01
- Rechner: 10.6 % ausgelastet, iowait 0.1 %, CPU-Druck 0.054, IO-Druck 0.003, Kerne über 90 %: 1.0, P-Kerne 11.1 %, E-Kerne 9.8 %; je Kern [10, 10, 12, 9, 14, 9, 13, 10, 12, 9, 15, 10, 11, 10, 10, 10, 9, 9, 10, 9]
- Host-Threads über 5 %: 71, höchster Einzelwert 99.1 %; heißeste: .NET TP Worker 3.4 %, .NET TP Worker 3.3 %, .NET TP Worker 3.2 %, .NET TP Worker 3.0 %, .NET TP Worker 3.0 %, .NET TP Worker 3.0 %
- Postgres: 21.5 Verbindungen, davon aktiv 5.93, idle in transaction 1.39; idle 14.18, active LWLock:WALWrite 4.44, idle in transaction 1.39, active Client:ClientRead 0.83, active CPU 0.28, active Timeout:SpinDelay 0.17, active IO:WALSync 0.07, active Lock:extend 0.05, active LWLock:XidGen 0.03, active LWLock:WALInsert 0.03
  - 5.37 × active: `COMMIT`
  - 1.21 × idle in transaction: `select es.mt_upsert_snapshot_auftrag($1,$2,$3,$4)`
  - 0.46 × active: `select es.mt_upsert_snapshot_auftrag($1,$2,$3,$4)`
  - 0.14 × idle in transaction: `select es.mt_upsert_snapshot_verteilung($1,$2,$3,$4)`
- WAL je s: 15325.4 Records, 2.7 MB, 113.8 Writes, 113.3 Syncs
- Zähler: host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 1.452, höchst 41.0; host-pruefdomaene Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 0.0, höchst 0.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene]: Mittel 100.0, höchst 100.0; host-pruefdomaene Npgsql wartende Anfragen ({request})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 5693.877, höchst 96422.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 7.644, höchst 22.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 10.616, höchst 100.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=idle]: Mittel 1.315, höchst 2.0; host-pruefdomaene Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung_pruefdomaene;state=used]: Mittel 0.0, höchst 0.0; host-pruefdomaene GC-Pause (s je s) (s / 1 sec): Mittel 0.017, höchst 0.342; host-pruefdomaene Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 78.685, höchst 1285.0; host-pruefdomaene Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 24.0; host-pruefdomaene Threadpool-Threads ({thread} / 1 sec): Mittel 0.342, höchst 12.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.002; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 1.0

