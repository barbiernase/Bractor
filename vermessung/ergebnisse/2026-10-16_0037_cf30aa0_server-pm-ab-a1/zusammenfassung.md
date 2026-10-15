# Vermessung server-pm-ab-a1 (e2e)

Commit `cf30aa0` (sauber), Branch `HEAD`, 2026-10-15T22:37:10.0928093+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 12.06 10.47 5.71 1/713 3870631
 · CPU-stärkste: 373 dotnet; 2.1 redis-server; 1.7 consul; 1.0 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 5.89 8.94 5.57 2/686 3872169
 · CPU-stärkste: 77.5 dotnet; 6.9 dotnet; 3.5 postgres; 1.9 redis-server; 1.7 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 6,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2124 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2061 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2134 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4715 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 60,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 4935 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9750 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 193 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1709 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11629 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 854 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1807 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1799 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1807 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2063 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 79,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 515 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1923 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 2,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1729 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 2913 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 24,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2636 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 12179 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 306 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2120 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2120 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2134 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1066 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 349 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 499 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1345 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1245 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2123 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 25,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2817 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 11637 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 360 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2134 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2099 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2134 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1714 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 44,2 |
| S4 Datensatz einfrieren | Queries (/s) | 4277 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 486 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1812 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1812 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1812 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
