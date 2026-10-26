# Vermessung server-pubsub-basis (e2e)

Commit `b1af87e` (sauber), Branch `HEAD`, 2026-10-26T04:39:48.5996139+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.57 2.78 2.23 2/719 999954
 · CPU-stärkste: 401 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.36 1.64 1.93 2/695 1004944
 · CPU-stärkste: 27.9 dotnet; 4.3 dotnet; 2.4 postgres; 2.1 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 45,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 716 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,6 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1314 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1314 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1315 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10267 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 19,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1777 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,6 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 985 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 985 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1212 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7031 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 61,9 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5939 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9610 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 171 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1395 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10575 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 623 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1211 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1067 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1211 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3661 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 573 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1341 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1687 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 591 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 962 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1111 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 18943 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 290 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1305 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1305 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1305 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10267 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10267 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1066 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 906 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1064 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 301 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 48,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1427 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 15966 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 361 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1290 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1290 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1290 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6266 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6266 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 405 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 396 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 404 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 63,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 11,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 28,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2800 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13906 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 369 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1005 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 974 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1089 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2664 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1977 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2326 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 774 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 177 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 557 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 105 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1593 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 55447 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 484 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1122 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1122 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1122 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1385 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1281 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1374 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 27,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 57,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 31,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2698 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 40236 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 627 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1045 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1045 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1089 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
