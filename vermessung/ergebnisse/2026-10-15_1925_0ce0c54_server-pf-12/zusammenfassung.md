# Vermessung server-pf-12 (e2e)

Commit `0ce0c54` (sauber), Branch `HEAD`, 2026-10-15T17:25:16.4317934+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.31 4.23 6.62 2/711 3488731
 · CPU-stärkste: 50.3 dotnet; 1.6 consul; 1.1 redis-server; 0.9 redis-server; 0.4 dotnet  
Fremdlast nachher: Last 6.08 5.03 6.42 2/704 3493224
 · CPU-stärkste: 26.2 dotnet; 7.4 postgres; 6.8 dotnet; 1.6 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 3045 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2859 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 3045 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,3 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 3,6 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1736 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1735 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1751 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4318 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 7,2 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,3 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5182 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10211 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 217 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1631 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12449 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 798 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1679 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1659 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1679 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4255 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 54,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1889 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3496 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 189 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1563 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 10309 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 391 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 329 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1235 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1147 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1235 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1634 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 61,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 208 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1925 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 2991 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8485 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 31,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1068 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 6459 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 969 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 3043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 3043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 3043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 486 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 172 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 248 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1292 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 3,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3164 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 3953 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1478 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7792 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 971 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2500 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2316 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2500 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1712 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 40,4 |
| S4 Datensatz einfrieren | Queries (/s) | 3949 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 231 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1768 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1768 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1768 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 250 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 245 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 250 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 69,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 29,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 56,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 9,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 62,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2418 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15292 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 682 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1120 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1099 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1120 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 438 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 273 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 337 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1128 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 2,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 2253 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 7282 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 4,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 3315 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 49,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1412 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 16646 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 1049 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1751 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1751 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1751 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 511 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 376 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 378 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 797 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 895 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2214 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 5,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 53,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1870 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 19652 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 954 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1634 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1634 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1634 |
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
