## Übersicht (Median über die Wiederholungen)

| Block | Kennzahl | keiner (2026-10-18_0019) |
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

# Engpass-Sonde — 2026-10-18_0019_60e561a_server-sonde-horizont-2

Commit `60e561a` (sauber), Knopf: **keiner**. CPU in Kernen (1 = ein voller Kern), Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).

## L1-nachlauf-w1 (49.1 s)

- CPU: host-beispiel 0.12, pg-backend 0.08, sonst 0.07, python 0.05, treiber 0.03, consul 0.02, pg-backend-treiber 0.02, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 2.2 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.001, Kerne über 90 %: 0.0, P-Kerne 2.7 %, E-Kerne 1.6 %; je Kern [3, 1, 4, 2, 5, 1, 5, 1, 4, 1, 4, 1, 2, 2, 2, 2, 1, 1, 1, 1]
- Host-Threads über 5 %: 27, höchster Einzelwert 25.9 %; heißeste: .NET TP Worker 0.5 %, .NET TP Worker 0.5 %, .NET TP Worker 0.5 %, .NET TP Worker 0.4 %, .NET TP Worker 0.3 %, .NET TP Worker 0.3 %
- Postgres: 49.0 Verbindungen, davon aktiv 0.03, idle in transaction 0.0; idle 48.97, active CPU 0.02
- WAL je s: 228.5 Records, 0.0 MB, 18.6 Writes, 18.5 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.041, höchst 1.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 40.959, höchst 41.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.041, höchst 1.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 5.0, höchst 5.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.001, höchst 0.007; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel -0.51, höchst 2.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.007; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel -0.367, höchst 0.0

## L1-w1 (64.2 s)

- CPU: pg-backend 0.07, sonst 0.06, python 0.05, host-beispiel 0.04, consul 0.02, treiber 0.01, cqrs-vermessung-redis 0.01, cqrs-redis 0.01
- Rechner: 1.6 % ausgelastet, iowait 0.0 %, CPU-Druck 0.003, IO-Druck 0.0, Kerne über 90 %: 0.0, P-Kerne 1.9 %, E-Kerne 1.2 %; je Kern [2, 0, 4, 1, 4, 0, 4, 0, 3, 1, 3, 1, 2, 2, 2, 1, 1, 1, 1, 1]
- Host-Threads über 5 %: 5, höchster Einzelwert 10.8 %; heißeste: .NET TP Worker 0.2 %, .NET TP Worker 0.2 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %, .NET TP Worker 0.1 %
- Postgres: 48.9 Verbindungen, davon aktiv 0.02, idle in transaction 0.01; idle 48.91
- WAL je s: 3.5 Records, 0.0 MB, 1.1 Writes, 1.1 Syncs
- Zähler: host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql laufende Befehle ({command})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql höchstens ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung]: Mittel 100.0, höchst 100.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=idle]: Mittel 41.0, höchst 41.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;Command Timeout=30;No Reset On Close=True;Max Auto Prepare=20;state=used]: Mittel 0.0, höchst 0.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=idle]: Mittel 5.0, höchst 5.0; host-beispiel Npgsql-Verbindungen ({connection})[pool.name=Host=localhost;Port=5432;Username=postgres;Database=cqrs_vermessung;state=used]: Mittel 0.0, höchst 0.0; host-beispiel GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.005; host-beispiel Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.079, höchst 2.0; host-beispiel Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 0.0; host-beispiel Threadpool-Threads ({thread} / 1 sec): Mittel 0.016, höchst 6.0; treiber GC-Pause (s je s) (s / 1 sec): Mittel 0.0, höchst 0.002; treiber Lock-Konflikte (je s) ({contention} / 1 sec): Mittel 0.016, höchst 1.0; treiber Threadpool-Warteschlange ({work_item} / 1 sec): Mittel 0.0, höchst 1.0; treiber Threadpool-Threads ({thread} / 1 sec): Mittel 0.0, höchst 9.0

