# Vermessung server-auto-prepare (e2e)

Commit `7a752af` (sauber), Branch `HEAD`, 2026-10-17T21:32:50.4101294+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.32 0.65 0.32 3/720 2471201
 · CPU-stärkste: 400 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.3 systemd  
Fremdlast nachher: Last 0.80 1.01 0.61 2/694 2477337
 · CPU-stärkste: 24.3 dotnet; 4.0 dotnet; 2.9 postgres; 2.0 consul; 1.4 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 43,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 719 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1451 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1451 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1634 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10283 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 20,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1787 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,6 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1122 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 995 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1294 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7874 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 60,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,9 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5939 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9856 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 172 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1344 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10274 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 637 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1255 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1163 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1255 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 9911 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 46,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 3054 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4198 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 22,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 146 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1035 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6728 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 243 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 270 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1130 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 964 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1130 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2710 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 327 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1057 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1759 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 921 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 1188 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 955 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 16193 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 257 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1399 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1399 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1634 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10283 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1059 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 980 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1056 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 149 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 36,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1478 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 16532 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 260 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1634 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1348 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1634 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6282 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6282 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2883 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 61,7 |
| S4 Datensatz einfrieren | Queries (/s) | 8393 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 399 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 395 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 398 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 29,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 10,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 26,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2734 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13563 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 414 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1116 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1050 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1116 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2996 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1495 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2619 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1503 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 133 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 413 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 100 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 27,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1738 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 57695 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 715 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1286 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1286 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1286 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1456 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1216 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1443 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 494 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 25,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 53,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2681 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 40560 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 610 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1110 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1110 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1116 |
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
