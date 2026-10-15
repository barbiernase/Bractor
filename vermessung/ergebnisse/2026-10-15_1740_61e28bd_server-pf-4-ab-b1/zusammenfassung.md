# Vermessung server-pf-4-ab-b1 (e2e)

Commit `61e28bd` (sauber), Branch `HEAD`, 2026-10-15T15:40:42.0328336+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.31 5.64 6.56 1/701 3325205
 · CPU-stärkste: 39.6 dotnet; 1.7 consul; 1.3 redis-server; 1.0 redis-server; 0.4 dotnet  
Fremdlast nachher: Last 7.59 6.77 6.88 1/690 3328369
 · CPU-stärkste: 30.4 dotnet;11.0 dotnet; 6.6 postgres; 1.7 consul; 1.4 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,6 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2326 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2275 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2355 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 368 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1832 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 131 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1487 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1487 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1744 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27662 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27662 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5042 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 8,4 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 46,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 12,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6530 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 12709 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 226 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1319 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10465 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 850 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1733 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1713 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1733 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 10139 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 49,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2130 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3992 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 192 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1415 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9454 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 452 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 308 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1076 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1071 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1076 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2382 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 92,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 225 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1922 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3731 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7982 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 33,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1260 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7406 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1073 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2355 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2355 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2355 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 307 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 108 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 186 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1295 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 4302 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 5045 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1161 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 6152 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 1129 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2268 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2203 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2268 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1758 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 39,7 |
| S4 Datensatz einfrieren | Queries (/s) | 3886 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 220 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 216 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 219 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 51,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 37,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 84,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2174 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13669 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 811 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1277 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1277 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6607 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6607 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5381 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 237 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 12,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 8349 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 12066 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 68,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 55,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1287 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13003 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 789 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1744 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1663 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1744 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18776 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18776 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 824 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 299 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 319 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1912 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2792 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5698 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2363 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 17466 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 885 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1398 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1398 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1398 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12607 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12607 |

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
