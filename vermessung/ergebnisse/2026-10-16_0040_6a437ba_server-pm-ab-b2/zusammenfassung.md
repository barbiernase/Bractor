# Vermessung server-pm-ab-b2 (e2e)

Commit `6a437ba` (sauber), Branch `HEAD`, 2026-10-15T22:40:03.6241072+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.20 7.49 5.37 1/675 3876174
 · CPU-stärkste: 39.3 dotnet; 1.7 consul; 1.7 redis-server; 1.0 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 3.66 6.58 5.22 2/683 3877698
 · CPU-stärkste: 28.3 dotnet; 8.0 dotnet; 3.8 postgres; 1.7 consul; 1.7 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 5,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2126 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2126 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2134 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4804 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 85,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,9 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3430 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6932 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 20,9 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 145 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1775 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12313 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1054 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1887 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1804 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1887 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3335 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 112 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 568 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1933 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 2,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1407 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 2921 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 2,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 18,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1202 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 10003 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 241 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2114 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2114 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2134 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 931 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 291 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 463 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1375 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1479 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2137 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 3,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 19,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1342 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 8539 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 342 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2134 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2091 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2134 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1797 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 29,7 |
| S4 Datensatz einfrieren | Queries (/s) | 4077 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 766 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1907 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1907 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1907 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
