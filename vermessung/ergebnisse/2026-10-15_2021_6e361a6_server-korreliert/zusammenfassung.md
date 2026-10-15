# Vermessung server-korreliert (e2e)

Commit `6e361a6` (sauber), Branch `HEAD`, 2026-10-15T18:21:04.4812378+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.69 3.32 3.03 1/719 3567867
 · CPU-stärkste: 67.0 dotnet; 1.6 consul; 1.2 redis-server; 0.9 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 5.69 3.88 3.26 2/687 3572175
 · CPU-stärkste: 36.2 dotnet; 7.7 postgres; 7.1 dotnet; 1.6 consul; 1.3 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 7,5 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 38,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 7,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2078 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2077 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2150 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 8,5 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 41,5 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 6,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1394 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1394 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1394 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5772 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,5 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5072 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10415 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,3 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 216 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1584 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12085 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 822 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1876 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1827 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1876 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 6772 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1851 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3349 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1618 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10692 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 395 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 335 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1219 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1180 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1219 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2075 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 66,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 424 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1936 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 3,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2157 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 3769 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 2149 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 12790 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 272 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2073 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2073 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2150 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 881 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 302 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 407 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1315 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 2,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1600 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 2635 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 2419 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12836 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 328 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2150 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2022 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2150 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1775 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 42,3 |
| S4 Datensatz einfrieren | Queries (/s) | 4134 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 389 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1922 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1922 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1922 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 263 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 266 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 40,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 25,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 56,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 65,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2674 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 17027 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 640 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1059 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1024 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1059 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 1924 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 396 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 399 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2383 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 6,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 3474 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 6032 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 21,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 3490 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 54,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1651 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 21070 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 688 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1391 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1391 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1391 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 515 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 421 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 437 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 549 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 842 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2497 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 54,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2199 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 22823 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 759 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1266 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1262 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1266 |
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
