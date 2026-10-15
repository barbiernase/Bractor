# Vermessung server-pf-12 (e2e)

Commit `e30f3c7` (sauber), Branch `HEAD`, 2026-10-15T17:12:09.2760210+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.79 5.83 8.20 1/715 3468028
 · CPU-stärkste: 63.5 dotnet; 1.6 consul; 1.3 redis-server; 0.9 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 9.18 6.70 7.97 2/694 3472656
 · CPU-stärkste: 33.3 dotnet; 7.4 postgres; 6.7 dotnet; 1.6 postgres; 1.6 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 3132 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 3131 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 3132 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3225 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 114 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 2661 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 2475 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 2838 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 6573 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,4 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5326 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10256 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 218 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1619 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12384 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 791 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1931 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1931 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1931 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4996 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 54,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1892 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3637 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 190 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1526 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10135 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 390 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 339 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1155 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1113 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1155 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1525 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 63,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 207 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1917 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2989 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8479 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1081 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 6518 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1007 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 3129 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 3129 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 3129 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 471 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 167 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 241 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1291 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3340 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4085 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1450 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7668 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 901 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2622 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2560 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2622 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1775 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 44,2 |
| S4 Datensatz einfrieren | Queries (/s) | 4320 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 390 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1983 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1983 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1983 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 246 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 244 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 246 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 25,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 29,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 57,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 65,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2413 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15803 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 737 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1151 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1067 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1151 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5177 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 286 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 7425 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 10028 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 17,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 4493 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 53,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1086 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 14975 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 1178 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 2838 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 2830 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2838 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 661 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 380 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 384 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1275 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 3,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 1538 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 3282 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2232 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 20776 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 973 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1316 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1316 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1316 |
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
