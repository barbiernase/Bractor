# Vermessung server-quick (e2e)

Commit `cf30aa0` (sauber), Branch `HEAD`, 2026-10-15T20:07:04.3368803+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.74 4.45 3.34 2/709 3689808
 · CPU-stärkste: 375 dotnet; 1.7 consul; 1.0 redis-server; 1.0 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 4.81 4.16 3.41 2/683 3693942
 · CPU-stärkste: 33.2 dotnet; 5.6 dotnet; 5.3 postgres; 1.7 consul; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 7,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2125 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2124 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2132 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 6,6 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 85,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 8,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1281 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1281 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1389 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 3700 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 30,8 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 62,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 595 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 4863 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9408 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 192 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1756 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11899 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 820 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1799 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1794 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1799 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4697 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 60,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1758 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3187 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 166 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1707 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9916 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 356 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 385 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1193 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1169 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1193 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2209 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 90,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 510 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1918 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1813 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3034 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 24,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2569 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 11742 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 261 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2127 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2127 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2127 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1018 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 326 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 515 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1359 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 1,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1285 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 1914 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 25,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2967 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12213 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 359 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2076 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2075 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2076 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1634 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 40,4 |
| S4 Datensatz einfrieren | Queries (/s) | 3913 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 297 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 290 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 296 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 64,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 22,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 50,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 52,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 3081 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15453 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 671 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 997 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 967 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 997 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2723 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 401 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 457 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2558 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 5,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 3163 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 5459 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 11,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 2671 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 43,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1944 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 19457 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 707 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1389 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1389 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1389 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 496 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 438 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 451 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 354 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 610 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2593 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 45,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2352 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 19625 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 771 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1200 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1184 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1200 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S2: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
