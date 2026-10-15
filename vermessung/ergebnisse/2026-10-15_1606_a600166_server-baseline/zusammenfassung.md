# Vermessung server-baseline (e2e)

Commit `a600166` (sauber), Branch `HEAD`, 2026-10-15T14:06:09.8348311+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.25 2.66 1.56 1/946 3195880
 · CPU-stärkste: 41.5 dotnet; 2.6 docker-proxy; 2.4 dotnet; 2.1 dotnet; 2.1 dotnet  
Fremdlast nachher: Last 9.10 5.57 2.88 2/957 3199780
 · CPU-stärkste: 19.6 dotnet; 9.3 docker-proxy; 9.2 dotnet; 7.8 postgres; 3.1 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 291 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 21,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2781 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2780 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2810 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 377 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2096 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 162 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1177 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1161 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1381 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27708 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27708 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 6095 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 51,9 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 11,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5707 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 11362 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 221 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1469 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11385 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 752 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1888 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1880 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1888 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 6538 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2141 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4115 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 193 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1327 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 8892 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 448 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 279 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1076 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1076 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1076 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2198 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 85,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 218 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1922 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3968 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8255 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 33,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1213 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7230 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1026 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2800 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2800 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2800 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 436 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 152 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 228 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1302 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3445 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4178 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1373 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7572 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 917 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2546 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2478 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2546 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1282 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,2 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 34,7 |
| S4 Datensatz einfrieren | Queries (/s) | 3391 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 125 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1892 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1892 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1892 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 223 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 220 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 222 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 32,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 35,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 67,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2218 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13973 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 717 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 994 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 994 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 994 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6577 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6577 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5675 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 191 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 15,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 10655 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 15112 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 34,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 66,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1209 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12547 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 724 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1381 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1316 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1381 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19437 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19437 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 760 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 292 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 307 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1849 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2680 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5734 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2154 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 16554 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 737 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1103 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1090 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1103 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12577 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12577 |

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
