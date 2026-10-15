# Vermessung server-pf-4-ab-a1 (e2e)

Commit `365a3b0` (sauber), Branch `HEAD`, 2026-10-15T15:37:41.2330589+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.06 4.74 6.59 2/717 3320057
 · CPU-stärkste: 56.7 dotnet; 1.7 consul; 1.3 redis-server; 1.0 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 7.59 5.60 6.56 2/689 3323829
 · CPU-stärkste: 35.5 dotnet; 9.0 dotnet; 7.8 postgres; 3.0 postgres; 1.7 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 321 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 20,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2566 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2566 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2662 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 429 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2594 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 175 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1275 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1268 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1576 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 28663 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 28663 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4692 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 54,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 11,0 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5555 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10886 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 219 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1524 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11758 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 794 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1937 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1937 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1937 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 5523 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 50,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1987 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3903 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 190 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1454 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9639 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 439 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 291 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1118 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1118 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1118 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2256 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 98,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 225 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1913 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3675 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8013 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1245 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7245 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 955 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2545 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2528 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2662 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 447 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 157 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 234 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1296 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3369 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4052 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1427 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7889 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 896 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2662 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2465 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2662 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1773 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 43,2 |
| S4 Datensatz einfrieren | Queries (/s) | 4222 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 221 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 219 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 220 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 31,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 38,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 76,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2199 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13862 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 826 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1102 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1085 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1102 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6591 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6591 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6118 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 211 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 13,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9320 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13618 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 50,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 64,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1349 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 774 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1576 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1442 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1576 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19252 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19252 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 750 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 284 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 317 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1863 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2567 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5400 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2206 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 16905 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 878 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1202 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1189 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1202 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12591 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12591 |

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
