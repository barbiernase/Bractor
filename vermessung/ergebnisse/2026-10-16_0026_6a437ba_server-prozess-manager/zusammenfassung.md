# Vermessung server-prozess-manager (e2e)

Commit `6a437ba` (sauber), Branch `HEAD`, 2026-10-15T22:26:39.1601085+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.95 2.88 1.55 2/709 3853555
 · CPU-stärkste: 377 dotnet; 1.8 consul; 1.1 redis-server; 1.0 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 1.86 2.43 1.62 1/683 3857654
 · CPU-stärkste: 36.2 dotnet; 6.8 postgres; 5.6 dotnet; 1.8 consul; 1.1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 7,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2154 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1900 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2154 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 11,8 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 110 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 11,7 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1218 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1217 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1218 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 3573 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 6,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 89,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3371 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6458 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 21,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 144 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1858 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12732 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 994 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1860 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1860 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1860 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 2393 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 85,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 2,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1258 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 2253 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 21,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 127 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1790 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10579 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 397 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 591 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 891 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 891 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 973 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3199 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 117 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 541 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1927 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1480 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3067 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 2,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 18,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1107 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 9214 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 216 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2150 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2150 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2150 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 965 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 335 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 486 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1306 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1400 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2035 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 3,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 19,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1393 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 8863 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 332 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2100 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2100 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2100 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1321 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 23,7 |
| S4 Datensatz einfrieren | Queries (/s) | 3258 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 261 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1965 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1965 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1965 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 299 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 291 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 298 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 74,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 15,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 36,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2197 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10611 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 474 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1014 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 926 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1014 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4491 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 693 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1246 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2537 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1103 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1783 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 12,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 640 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1274 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 32129 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 699 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1212 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1212 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1212 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1216 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 941 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1030 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 680 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 217 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 490 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 30,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2021 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 29239 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 679 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1044 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1044 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1044 |
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
