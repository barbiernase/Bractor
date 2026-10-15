# Vermessung server-pf-12 (e2e)

Commit `562e793` (sauber), Branch `HEAD`, 2026-10-15T16:53:38.7875997+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.40 1.70 5.17 1/705 3440866
 · CPU-stärkste: 43.6 dotnet; 1.6 consul; 1.3 redis-server; 0.9 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 5.08 3.19 5.00 2/693 3445470
 · CPU-stärkste: 23.9 dotnet; 7.1 postgres; 6.7 dotnet; 1.6 consul; 1.4 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2552 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2552 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2588 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 706 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 51,7 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1119 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1118 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1293 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4944 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5247 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10354 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 217 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1587 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12124 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 807 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 2037 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 2034 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 2037 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4536 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1846 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3475 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1566 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10323 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 384 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 338 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1085 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1085 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1085 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2638 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 73,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 230 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1944 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 7,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3552 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7954 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1265 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7357 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 953 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2552 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2505 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2588 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 481 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 174 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 246 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1275 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3210 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 3970 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1462 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7900 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 920 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2588 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2546 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2588 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1775 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 39,2 |
| S4 Datensatz einfrieren | Queries (/s) | 3834 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 389 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 2136 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 2136 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 2136 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 254 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 248 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 254 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 69,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 31,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 67,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2469 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15750 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 711 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 991 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 991 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1082 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6065 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 236 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 12,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 7964 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 12185 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 39,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 55,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1405 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12997 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 777 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1293 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1293 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1293 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 979 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 348 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 336 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1933 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2626 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5835 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2454 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 18213 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 807 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1109 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1109 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1109 |
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
