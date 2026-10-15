# Vermessung server-pf-12 (e2e)

Commit `5bda1bb` (sauber), Branch `HEAD`, 2026-10-15T17:40:22.1671053+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.89 2.53 4.80 3/715 3511528
 · CPU-stärkste: 50.2 dotnet; 1.6 consul; 1.1 redis-server; 0.9 redis-server; 0.4 dotnet  
Fremdlast nachher: Last 6.32 3.74 4.74 2/689 3515950
 · CPU-stärkste: 26.4 dotnet; 7.5 postgres; 6.9 dotnet; 1.6 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 6,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 3011 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2894 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 3011 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,5 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 3,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1520 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1520 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1612 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5803 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5309 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10546 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 218 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1575 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12082 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 814 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1730 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1650 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1730 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4025 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1824 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3462 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1613 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10615 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 408 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 337 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1117 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1117 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1117 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1666 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 60,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 207 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1927 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2937 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8563 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1071 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 6470 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1001 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2996 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2996 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2996 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 505 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 171 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 248 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1322 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3122 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4090 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1476 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7744 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 901 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2489 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2432 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2489 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1710 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 41,8 |
| S4 Datensatz einfrieren | Queries (/s) | 4079 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 417 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1759 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1759 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1759 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 252 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 249 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 252 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 35,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 30,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 61,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 62,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2481 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15569 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 697 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1069 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1069 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1099 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 538 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 348 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1203 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 3,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 2501 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 5160 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 5,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 2017 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 50,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1544 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 17036 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 1047 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1564 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1522 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1612 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 479 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 362 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 368 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 736 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 894 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2015 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 53,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1855 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 19202 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 993 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1612 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1594 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1612 |
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
