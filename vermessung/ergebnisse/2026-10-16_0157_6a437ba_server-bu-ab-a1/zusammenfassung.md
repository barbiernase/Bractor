# Vermessung server-bu-ab-a1 (e2e)

Commit `6a437ba` (sauber), Branch `HEAD`, 2026-10-15T23:57:00.6286804+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 15.85 11.31 5.76 2/723 3964278
 · CPU-stärkste: 396 dotnet; 2.1 redis-server; 1.8 consul; 1.0 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 2.75 7.53 5.11 1/701 3967171
 · CPU-stärkste: 53.5 dotnet; 7.1 postgres; 5.8 dotnet; 2.0 redis-server; 1.8 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1623 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1558 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1626 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 11,5 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 115 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 10,8 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1292 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1270 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1332 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3107 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 132 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 515 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1915 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1589 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3220 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 2,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 18,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1076 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 8958 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 233 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1613 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1613 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1626 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 776 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 253 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 437 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1347 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1395 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 1986 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 3,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 19,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1310 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 8127 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 365 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1626 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1572 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1626 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 284 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 277 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 283 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 68,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 16,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 47,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2048 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 9883 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1081 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1075 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1081 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2979 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 850 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1146 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2144 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 869 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1624 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 5,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 132 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 28,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1860 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 30341 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 712 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1315 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1315 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1315 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1236 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 931 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1041 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 740 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 530 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 31,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2100 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 30753 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 615 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1207 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1207 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1207 |
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
