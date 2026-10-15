# Vermessung server-schreibweg-ab-a1 (e2e)

Commit `3483a31` (sauber), Branch `HEAD`, 2026-10-15T20:15:10.2795368+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 6.71 5.70 4.25 2/706 3701679
 · CPU-stärkste: 341 dotnet; 1.7 consul; 1.5 redis-server; 1.0 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 4.92 5.03 4.19 2/687 3705126
 · CPU-stärkste: 39.7 dotnet; 8.0 dotnet; 7.8 postgres; 1.7 consul; 1.7 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 7,5 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 38,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 7,8 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2384 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2383 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2384 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 14,9 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 127 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 10,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1394 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1374 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1394 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4504 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,6 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,3 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5099 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10173 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,3 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 216 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1623 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12377 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 827 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1917 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1917 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1917 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1976 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 81,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 438 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1918 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2107 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3549 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2213 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 13217 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 273 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2363 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2360 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2363 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 903 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 320 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 423 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1292 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1456 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2503 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2441 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12971 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 313 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2319 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2319 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2319 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1732 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 41,9 |
| S4 Datensatz einfrieren | Queries (/s) | 4087 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 284 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 281 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 283 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 33,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 24,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 52,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 65,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2831 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 18112 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 611 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1093 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1018 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1093 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 1548 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 416 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 421 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2194 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 5,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 2907 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 5440 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 9,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 2818 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 53,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1772 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 22050 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 679 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1371 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1371 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1371 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 554 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 477 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 469 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 416 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 938 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 3012 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2377 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 25003 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 725 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1232 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1232 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1232 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
