# Vermessung server-baseline (e2e)

Commit `ee5fced` (sauber), Branch `HEAD`, 2026-10-15T14:52:47.1642890+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.15 2.67 4.75 1/718 3257339
 · CPU-stärkste: 385 dotnet; 1.6 consul; 1.2 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 8.52 5.19 5.37 2/686 3260827
 · CPU-stärkste: 43.1 dotnet; 8.9 dotnet; 6.4 postgres; 1.6 consul; 1.4 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 351 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 24,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2143 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1895 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2655 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,7 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 381 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2293 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 145 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1442 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1384 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1563 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27983 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27983 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4805 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,3 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5363 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10163 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 218 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1633 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12507 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 786 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1705 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1699 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1705 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 3900 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 50,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2087 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3942 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 191 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1410 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9423 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 435 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 318 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1166 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1166 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1166 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2559 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 101 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 229 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1921 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 7,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3614 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7929 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1262 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7311 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 949 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2408 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2407 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2655 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 426 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 149 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 230 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1301 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3515 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4033 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 34,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1375 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7645 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 922 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2655 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2402 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2655 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1654 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 39,6 |
| S4 Datensatz einfrieren | Queries (/s) | 3866 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 455 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1791 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1791 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1791 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 224 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 222 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 224 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 31,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 37,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 75,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2228 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 14038 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 776 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1085 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1085 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1085 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6579 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6579 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6347 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 206 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 14,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9706 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13983 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 52,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 64,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1331 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13298 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 739 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1563 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1451 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1563 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19184 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19184 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 912 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 304 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 362 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2001 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2327 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 4942 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 54,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2360 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 19058 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 842 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12579 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12579 |

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
