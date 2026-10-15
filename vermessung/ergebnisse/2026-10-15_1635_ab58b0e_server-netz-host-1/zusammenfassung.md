# Vermessung server-netz-host-1 (e2e)

Commit `ab58b0e` (sauber), Branch `HEAD`, 2026-10-15T14:35:07.5801734+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 8.72 5.40 7.48 1/678 3233086
 · CPU-stärkste: 29.8 dotnet; 1.6 consul; 0.9 redis-server; 0.9 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 7.49 5.96 7.32 2/682 3236794
 · CPU-stärkste: 16.8 dotnet; 9.4 dotnet; 7.7 postgres; 3.2 postgres; 1.8 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 347 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 17,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2055 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2055 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2525 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 417 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2567 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 172 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1344 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1344 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1607 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 28501 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 28501 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4073 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,4 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5338 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10328 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 219 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1595 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12296 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 796 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1722 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1642 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1722 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 3032 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 50,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2084 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3922 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 191 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1408 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9362 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 429 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 295 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1084 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1084 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1084 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2262 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 98,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 227 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1913 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 7,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3571 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7927 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1264 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7349 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 976 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2343 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2525 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 430 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 149 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 226 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1306 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3707 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4199 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 34,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1371 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7599 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 919 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2525 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2322 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2525 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1828 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 45,4 |
| S4 Datensatz einfrieren | Queries (/s) | 4432 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 237 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 233 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 237 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 52,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 31,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 68,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2361 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 14989 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 698 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1085 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1081 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1085 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6597 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6597 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4893 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 206 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 14,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9643 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13914 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 53,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 65,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1297 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13108 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 765 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1607 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1509 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1607 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19261 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19261 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 936 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 345 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 335 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1894 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2580 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5671 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2455 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 18252 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 811 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1213 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1213 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1213 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12597 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12597 |

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
