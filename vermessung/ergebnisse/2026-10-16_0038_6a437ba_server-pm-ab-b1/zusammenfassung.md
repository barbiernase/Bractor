# Vermessung server-pm-ab-b1 (e2e)

Commit `6a437ba` (sauber), Branch `HEAD`, 2026-10-15T22:38:38.0228127+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.80 8.82 5.57 1/677 3873424
 · CPU-stärkste: 70.6 dotnet; 1.8 redis-server; 1.7 consul; 1.0 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 3.52 7.47 5.34 2/684 3874972
 · CPU-stärkste: 41.4 dotnet; 7.9 dotnet; 2.4 postgres; 1.9 postgres; 1.7 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 6,8 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1987 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1987 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2213 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4375 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 29,2 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 88,5 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 596 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3372 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6640 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 21,1 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 145 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1839 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12617 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1025 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1836 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1830 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1836 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3230 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 102 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 494 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1937 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1584 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3416 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 2,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 18,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1054 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 8779 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 221 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2213 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1986 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2213 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 991 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 308 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 509 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1379 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 1,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1353 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 1909 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 3,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 19,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1486 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 9450 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 313 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2207 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2207 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2207 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1765 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 28,9 |
| S4 Datensatz einfrieren | Queries (/s) | 3965 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
