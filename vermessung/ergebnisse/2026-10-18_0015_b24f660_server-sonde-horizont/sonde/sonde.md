## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-18_0015) |
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

# Engpass-Sonde — 2026-10-18_0015_b24f660_server-sonde-horizont

Commit `b24f660` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## L1-nachlauf-w1 (50.1 s)

- CPU: host-beispiel 0.14, pg-backend 0.08, sonst 0.06, python 0.04, treiber 0.03, consul 0.02, pg-backend-treiber 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 2.3 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.001, Kerne über 90 %: 0.0, P-Kerne 2.7 %, E-Kerne 1.6 %; je Kern [3, 2, 4, 1, 4, 1, 4, 2, 4, 1, 4, 2, 2, 2, 2, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 34, höchster Einzelwert 25.9 %; heißeste: .NET TP Worker 0.5 %, .NET TP Worker 0.5 %, .NET TP Worker 0.5 %, .NET TP Worker 0.4 %, .NET TP Worker 0.4 %, .NET TP Worker 0.4 %
- Postgres: 48.0 Verbindungen, davon aktiv 0.03, idle in transaction 0.0; idle 47.97, active CPU 0.02, active LWLock:WALWrite 0.01
- WAL je s: 224.8 Records, 0.0 MB, 16.5 Writes, 16.3 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.02, höchst 1.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 39.98, höchst 40.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.02, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 5.98, höchst 6.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.02, höchst 1.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.007; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.14, höchst 6.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.42, höchst 4.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.001, höchst 0.013; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.36, höchst 0.0

## L1-w1 (64.2 s)

- CPU: sonst 0.05, pg-backend 0.05, python 0.04, host-beispiel 0.03, consul 0.02, treiber 0.01, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.4 % ausgelastet, iowait 0.0 %, CPU-Druck 0.002, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.7 %, E-Kerne 0.8 %; je Kern [3, 0, 3, 0, 3, 0, 2, 2, 3, 0, 3, 0, 1, 1, 1, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 0, höchster Einzelwert 0 %; heißeste: 
- Postgres: 48.0 Verbindungen, davon aktiv 0.01, idle in transaction 0.04; idle 47.97, idle in transaction 0.04
- WAL je s: 2.4 Records, 0.0 MB, 0.1 Writes, 0.1 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 40.0, höchst 40.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 6.0, höchst 6.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.005; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.095, höchst 5.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.016, höchst 7.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.003; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 6.0

