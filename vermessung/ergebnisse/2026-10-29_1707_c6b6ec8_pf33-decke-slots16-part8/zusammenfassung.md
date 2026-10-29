# Vermessung pf33-decke-slots16-part8 (e2e)

Commit `c6b6ec8` (sauber), Branch `HEAD`, 2026-10-29T16:07:11.5102660+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.93 3.77 2.21 1/685 289904
 · CPU jetzt: 3 dockerd; 2 dotnet; 2 containerd; 2 consul; 1 ksoftirqd/15  
Fremdlast nachher: Last 3.21 4.20 2.74 2/693 303209
 · CPU jetzt: 3 dotnet; 2 consul; 2 redis-server; 2 redis-server; 1 dockerd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 45,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 581 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1273 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1273 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1452 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 71503 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 71503 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3933 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1149 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2028 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 28316 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 26,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 7022 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 14638 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 5,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 357 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 18,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1023 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 36419 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 341 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1452 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1287 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1452 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 71503 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 71503 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1561 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1488 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1561 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1469 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 12,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 14,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 42,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 36,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1579 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 54813 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 537 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1011 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1011 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1011 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 31503 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 31503 |

- L5: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
