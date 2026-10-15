# Vermessung server-schreibweg-ab-b2 (e2e)

Commit `cf30aa0` (sauber), Branch `HEAD`, 2026-10-15T20:20:46.8569900+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.16 4.64 4.15 1/673 3710706
 · CPU-stärkste: 20.0 dotnet; 1.7 consul; 1.7 redis-server; 1.0 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 3.54 3.99 3.96 3/686 3713943
 · CPU-stärkste: 13.9 dotnet; 7.1 dotnet; 5.4 postgres; 1.7 consul; 1.7 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 7,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2043 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2040 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2043 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 10,7 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 111 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 8,6 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1340 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1301 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1366 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5771 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 9,6 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 59,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,9 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 4972 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9850 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,3 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 193 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1675 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11435 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 854 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1664 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1664 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1664 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2128 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 88,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 504 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1917 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1783 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3031 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 24,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2496 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 11391 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 270 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1033 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 324 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 504 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1373 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1262 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 1964 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 25,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2918 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12053 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 364 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1988 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1988 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1988 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1767 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 43,4 |
| S4 Datensatz einfrieren | Queries (/s) | 4195 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 301 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 297 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 300 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 38,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 21,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 47,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 52,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 3093 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15530 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 635 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1079 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1016 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1079 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 905 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 469 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 487 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1445 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 2,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 2144 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 2936 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 873 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 42,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 2473 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 20266 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 704 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1366 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1366 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1366 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 729 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 526 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 496 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 835 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 1264 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2145 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 44,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2513 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 21092 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 810 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1247 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1247 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1247 |
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
