# Vermessung server-bu-ab-b2 (e2e)

Commit `2390f1e` (sauber), Branch `HEAD`, 2026-10-16T00:01:51.4440722+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.72 5.44 4.67 1/685 3972442
 · CPU-stärkste: 27.4 dotnet; 1.9 redis-server; 1.8 consul; 1.0 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 3.30 4.41 4.36 2/702 3975243
 · CPU-stärkste: 19.3 dotnet; 6.6 dotnet; 5.5 postgres; 2.6 postgres; 1.9 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1467 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1465 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1467 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 9,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 110 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 8,5 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1148 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1147 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1148 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2841 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 263 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1235 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1815 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 688 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 929 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1037 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 17440 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 414 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1467 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1464 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1467 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 754 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 653 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 753 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 267 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 15,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 192 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1172 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 11970 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 540 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1419 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1419 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1419 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 293 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 286 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 293 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 73,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 16,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 37,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2141 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10408 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 480 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 892 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 885 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 967 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4011 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 670 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1338 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2499 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 951 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1602 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 5,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 348 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 25,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1114 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 30572 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 736 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1126 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1126 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1126 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1219 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 923 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1033 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 727 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 203 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 519 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 30,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2055 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 29799 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 675 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1002 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1002 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1002 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
