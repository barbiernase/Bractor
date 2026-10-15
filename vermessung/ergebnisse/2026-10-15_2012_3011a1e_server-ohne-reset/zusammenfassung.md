# Vermessung server-ohne-reset (e2e)

Commit `3011a1e` (sauber), Branch `HEAD`, 2026-10-15T18:12:58.3013062+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.45 1.13 2.27 2/708 3554023
 · CPU-stärkste: 397 dotnet; 1.6 consul; 1.2 redis-server; 0.9 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 5.85 3.22 2.84 2/681 3558437
 · CPU-stärkste: 34.6 dotnet; 7.3 postgres; 6.3 dotnet; 1.6 consul; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,3 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 3266 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 3265 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 3266 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,5 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,8 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1780 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1717 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1811 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5125 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 61,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 4982 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9498 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 215 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1753 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13249 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 806 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1987 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1986 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1987 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 5408 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1920 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3465 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1610 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10625 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 353 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 376 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1229 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1122 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1229 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1658 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 58,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 214 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1930 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2847 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8207 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1105 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 6659 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1030 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 3247 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 3247 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 3247 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 521 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 184 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 250 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1295 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3023 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4165 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1518 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7972 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 832 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2645 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2599 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2645 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1332 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,2 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 34,3 |
| S4 Datensatz einfrieren | Queries (/s) | 3354 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 268 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 265 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 31,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 28,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 57,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 62,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2642 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 16515 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 671 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1222 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1151 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1222 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 450 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 282 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 344 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1122 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 2,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 2464 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 6986 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 4,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 2422 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 50,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1464 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 16803 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 1040 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1810 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1810 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1810 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 515 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 398 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 396 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 680 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 928 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2408 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 53,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1971 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 20520 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 938 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1518 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1518 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1518 |
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
