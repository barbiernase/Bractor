# Vermessung server-pm-ab-a2 (e2e)

Commit `cf30aa0` (sauber), Branch `HEAD`, 2026-10-15T22:41:28.9152631+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.71 6.71 5.28 1/675 3878904
 · CPU-stärkste: 27.3 dotnet; 1.7 consul; 1.7 redis-server; 1.0 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 3.72 5.99 5.14 3/684 3880476
 · CPU-stärkste: 21.3 dotnet; 8.0 dotnet; 2.9 postgres; 1.7 consul; 1.6 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 6,7 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2255 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2254 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2255 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4714 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 62,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,4 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 4917 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9323 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,3 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 192 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1760 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11937 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 847 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1926 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1926 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1926 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1928 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 55,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 485 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1942 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2008 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3074 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 24,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2382 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 10888 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 283 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1092 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 365 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 514 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1332 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1254 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2050 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 25,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 3062 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12645 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 389 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2239 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2217 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2239 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1764 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 43,5 |
| S4 Datensatz einfrieren | Queries (/s) | 4213 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
