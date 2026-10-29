# Vermessung pf36-vorrat-a1 (e2e)

Commit `90798a7` (sauber), Branch `HEAD`, 2026-10-29T11:49:55.6286158+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.71 0.45 0.20 2/720 4151661
 · CPU-stärkste: 320 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.2 containerd  
Fremdlast nachher: Last 1.26 0.85 0.39 2/704 4153904
 · CPU-stärkste: 46.8 dotnet; 5.1 dotnet; 3.9 postgres; 2.1 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 2,0 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 360 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 824 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 824 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 937 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 433 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 422 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 433 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 77,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 17,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2291 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19738 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 350 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 812 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 812 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 812 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 56,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 297 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2980 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5694 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 9760 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 23,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 37,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1093 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 10839 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 309 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 937 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 824 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 937 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1685 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 334 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 300 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2406 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3826 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8243 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1101 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11568 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 290 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 859 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 812 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 859 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
