# Vermessung server-pubsub-phase6-s5-b2 (e2e)

Commit `4f43172` (sauber), Branch `HEAD`, 2026-10-28T05:18:43.2665480+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 0.97 4.31 5.45 2/646 4079717
 · CPU-stärkste: 33.3 consul; 2.8 redis-server; 2.1 consul; 1.3 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 0.86 2.60 4.49 2/646 4083880
 · CPU-stärkste: 3.8 dotnet; 2.3 redis-server; 2.1 consul; 1.9 postgres; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 11,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 305 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 774 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 774 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 834 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9458 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9458 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 428 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 435 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 52,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 16,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2294 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19780 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 354 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 709 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 700 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 834 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3458 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7684 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 99,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 334 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2961 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 8,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 4846 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 8585 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 50,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 36,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1161 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12040 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 393 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 808 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 771 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 834 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1949 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 305 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 293 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2531 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3961 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8657 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 40,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 96,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 986 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 28,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 759 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 659 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 834 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6458 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
