# Vermessung server-schreibweg-ab-a2 (e2e)

Commit `3483a31` (sauber), Branch `HEAD`, 2026-10-15T20:23:34.0736628+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.63 3.99 3.96 1/673 3715155
 · CPU-stärkste: 13.7 dotnet; 1.7 consul; 1.7 redis-server; 1.0 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 4.34 4.03 3.97 2/646 3718469
 · CPU-stärkste: 8.5 dotnet; 7.8 postgres; 1.7 redis-server; 1.7 consul; 1.2 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 7,5 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 38,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 8,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1775 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1775 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1833 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 12,7 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 121 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 9,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1425 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1390 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1429 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 3440 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 22,9 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 596 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5158 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10463 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 218 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1589 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12209 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 809 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1642 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1642 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1642 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2141 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 62,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 441 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1942 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2128 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3590 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2248 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 13414 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 264 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1762 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1762 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1833 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 922 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 321 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 425 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1304 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1506 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2532 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2422 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12841 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 309 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1833 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1711 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1833 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1743 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 43,9 |
| S4 Datensatz einfrieren | Queries (/s) | 4292 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 266 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 262 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 266 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 52,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 25,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 54,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 65,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2702 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 17143 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 635 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1101 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1076 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1101 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 1491 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 442 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 451 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2110 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 4,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1684 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 4483 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 4,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 855 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 53,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 2084 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 23304 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 769 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1429 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1429 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1429 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 586 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 476 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 468 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 562 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 1059 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2090 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 53,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2390 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 24343 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 808 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1319 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1319 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1319 |
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
