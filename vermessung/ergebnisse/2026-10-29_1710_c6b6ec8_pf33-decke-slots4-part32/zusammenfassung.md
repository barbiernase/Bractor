# Vermessung pf33-decke-slots4-part32 (e2e)

Commit `c6b6ec8` (sauber), Branch `HEAD`, 2026-10-29T16:10:41.4976765+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.88 4.08 2.72 1/712 305367
 · CPU jetzt: 3 consul; 2 dockerd; 2 dotnet; 2 redis-server; 1 kworker/12:1-events_freezable  
Fremdlast nachher: Last 3.20 4.56 3.24 2/644 318112
 · CPU jetzt: 4 consul; 3 dockerd; 3 dotnet; 2 containerd; 1 containerd-shim  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 37,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 2158 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 24,5 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,8 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1313 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1313 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1457 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 69559 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 69559 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3468 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 893 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1485 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 29703 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 15,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 36,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 11885 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 21259 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 5,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 468 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 22,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 854 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 32322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 323 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1457 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1312 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1457 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 69559 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 69559 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1451 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1355 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1450 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1909 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 18,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 15,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 44,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 42,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1511 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 57249 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 513 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 977 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 977 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 977 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 29559 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 29559 |

- L5: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
