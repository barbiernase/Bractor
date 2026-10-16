# Vermessung server-buendel (e2e)

Commit `2390f1e` (sauber), Branch `HEAD`, 2026-10-15T23:46:49.6410872+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.31 2.30 1.26 2/714 3947117
 · CPU-stärkste: 387 dotnet; 1.8 consul; 1.3 redis-server; 1.0 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 2.18 2.35 1.49 2/692 3951186
 · CPU-stärkste: 38.1 dotnet; 5.8 postgres; 5.7 dotnet; 2.0 postgres; 1.8 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 29,6 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 7,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1784 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1722 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1920 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 10,7 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 113 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 10,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1208 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1145 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1213 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4960 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 89,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3407 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6619 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 21,2 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 145 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1858 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12665 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 984 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1583 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1583 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1583 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4089 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 86,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 2,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1230 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 21,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 127 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1833 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10793 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 365 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 534 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1071 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1071 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1071 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3305 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 411 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1350 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1751 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 643 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 890 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1161 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 19458 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 444 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1771 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1771 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1920 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 861 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 783 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 859 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 183 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 16,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 30,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1347 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13975 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 502 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1920 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1792 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1920 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1784 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 29,5 |
| S4 Datensatz einfrieren | Queries (/s) | 4043 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 298 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 293 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 298 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 52,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 15,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 41,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2151 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10477 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 483 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1011 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 934 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1011 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 859 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1280 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2255 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 864 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1489 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 98,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 28,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1674 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 32292 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 664 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1213 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1213 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1213 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1080 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 917 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 937 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 455 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 370 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 566 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1772 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 25526 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 670 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1094 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1094 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1094 |
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
