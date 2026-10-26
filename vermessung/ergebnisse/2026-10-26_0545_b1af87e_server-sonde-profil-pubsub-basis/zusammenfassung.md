# Vermessung server-sonde-profil-pubsub-basis (e2e)

Commit `b1af87e` (sauber), Branch `HEAD`, 2026-10-26T04:45:47.1663326+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.77 1.63 1.89 1/651 1007352
 · CPU-stärkste: 2.6 python3; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 1.0 postgres  
Fremdlast nachher: Last 7.16 11.97 7.21 2/675 1021099
 · CPU-stärkste: 25.5 dotnet;15.0 postgres; 3.2 python3; 2.7 postgres; 2.7 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 52,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 22144 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 6,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 5047 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 5047 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 5386 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 594544 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 594594 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2475 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 2322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1891 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 18396 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 37,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 187 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 63466 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 67,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 1578 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 27,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1808 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 51014 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 575 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 5386 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 5385 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 5386 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 594594 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 594594 |

- L5: Exactly-once grün
- S5-64: Exactly-once grün
