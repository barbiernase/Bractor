# Vermessung server-netz-host-2 (e2e)

Commit `ab58b0e` (sauber), Branch `HEAD`, 2026-10-15T14:38:04.1413036+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.06 5.91 7.29 1/672 3238218
 · CPU-stärkste: 16.3 dotnet; 1.7 redis-server; 1.6 consul; 0.6 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 8.52 6.72 7.32 2/682 3242069
 · CPU-stärkste: 42.5 postgres;11.4 dotnet; 9.2 dotnet; 7.7 postgres; 3.3 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 155 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 22,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2546 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2546 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2611 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 413 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2489 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 169 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1329 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1329 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1550 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 28495 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 28495 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 6171 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 54,3 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,9 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5396 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10843 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 220 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1535 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11824 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 780 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1949 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1944 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1949 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 6490 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 50,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2035 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3841 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 191 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1419 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9435 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 440 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 301 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1122 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1122 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1122 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2420 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 83,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 225 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1931 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3645 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8081 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 33,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1230 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7278 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 930 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2450 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2402 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2611 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 436 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 155 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 234 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1288 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 3,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3487 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 3929 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 34,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1419 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7850 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 860 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2611 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2371 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2611 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1815 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 42,6 |
| S4 Datensatz einfrieren | Queries (/s) | 4162 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 223 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 220 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 222 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 42,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 37,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 74,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2213 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13945 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 774 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1170 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1156 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1170 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6571 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6571 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5959 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 210 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 13,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9321 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13701 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 50,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 65,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1356 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13450 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 781 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1550 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1496 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1550 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19336 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19336 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 897 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 331 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 326 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1892 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2680 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5819 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2388 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 17511 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 806 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1270 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1269 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1270 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12571 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12571 |

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
