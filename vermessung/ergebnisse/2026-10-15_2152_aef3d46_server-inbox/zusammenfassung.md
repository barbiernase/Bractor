# Vermessung server-inbox (e2e)

Commit `aef3d46` (sauber), Branch `HEAD`, 2026-10-15T19:52:20.0976415+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.07 2.81 1.53 2/716 3663904
 · CPU-stärkste: 390 dotnet; 1.7 consul; 1.3 redis-server; 1.0 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 3.32 3.07 1.87 2/690 3668170
 · CPU-stärkste: 36.3 dotnet; 5.8 dotnet; 5.1 postgres; 1.7 consul; 1.5 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 6,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2104 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2104 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2104 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 7,4 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 91,3 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 9,7 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1273 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1263 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1323 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4731 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 60,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5043 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9650 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 216 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1708 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13026 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 803 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1710 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1689 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1710 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 3662 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 58,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1780 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3361 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1652 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10904 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 362 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 338 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1241 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1080 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1241 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2158 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 73,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 398 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1932 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 4,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2268 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 4093 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 29,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1995 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 11217 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 267 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2104 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2104 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2104 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 926 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 305 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 389 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1342 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 3,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1515 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2962 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 31,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2245 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 11532 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 329 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2091 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2058 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2091 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1714 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 40,8 |
| S4 Datensatz einfrieren | Queries (/s) | 3985 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 255 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1803 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1803 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1803 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 280 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 273 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 279 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 73,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 24,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 52,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2776 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 17534 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 629 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1120 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1005 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1120 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 1050 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 451 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2074 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 3432 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 5362 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 5,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 2465 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 45,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1878 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 20017 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 666 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 828 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 518 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 485 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1125 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 2,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 1071 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2775 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 51,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2548 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 24944 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 720 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1180 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1180 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1180 |
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
