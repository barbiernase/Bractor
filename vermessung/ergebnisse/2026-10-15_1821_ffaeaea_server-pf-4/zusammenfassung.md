# Vermessung server-pf-4 (e2e)

Commit `ffaeaea` (sauber), Branch `HEAD`, 2026-10-15T16:21:22.5020550+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.18 4.99 5.52 2/713 3396258
 · CPU-stärkste: 375 dotnet; 1.6 consul; 1.3 redis-server; 0.9 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 7.54 5.61 5.61 1/686 3400810
 · CPU-stärkste: 31.6 dotnet; 7.3 postgres; 6.4 dotnet; 1.6 consul; 1.4 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,7 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2310 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2309 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2420 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 390 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1932 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 140 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1471 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1339 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1533 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27630 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27630 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5648 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,3 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5296 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10447 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 218 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1586 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12142 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 768 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1738 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1709 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1738 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 3611 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1890 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3535 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1554 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10263 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 327 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 420 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1213 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1213 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1213 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2533 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 95,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 230 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1925 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 7,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3561 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7922 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1275 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7387 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 933 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2384 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2306 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2420 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 498 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 176 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 249 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1292 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3181 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4031 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1476 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7958 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 882 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2420 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2220 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2420 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1830 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 45,4 |
| S4 Datensatz einfrieren | Queries (/s) | 4442 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 263 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 256 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 263 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 83,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 29,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 57,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2585 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 16415 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 662 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1048 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1034 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1196 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6842 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6842 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5965 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 240 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 12,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 7576 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 11928 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 40,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 54,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1373 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12956 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 788 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1496 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1496 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1496 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18842 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18842 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1007 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 371 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 341 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1894 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2522 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5761 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2460 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 18244 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 756 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1153 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1136 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1196 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12842 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12842 |

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
