# Vermessung server-pf-22 (e2e)

Commit `8948277` (sauber), Branch `HEAD`, 2026-10-17T21:58:06.3354214+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.36 2.09 1.71 2/717 2504877
 · CPU-stärkste: 108 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 0.99 1.51 1.58 1/693 2510913
 · CPU-stärkste: 39.5 dotnet; 3.8 dotnet; 3.2 postgres; 2.0 consul; 1.4 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 43,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 715 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,9 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1565 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1564 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1605 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10283 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 20,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1768 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,7 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1046 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 983 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1090 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8048 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 61,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5919 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9697 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 172 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1382 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10497 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 622 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1168 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1160 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1168 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 5672 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2909 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4153 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 22,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 147 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1060 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6936 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 205 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 292 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1001 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1001 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1001 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3467 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 397 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1093 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1771 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 826 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 1246 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1166 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 16719 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 253 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1591 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1591 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1591 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10283 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1235 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1126 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1233 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 176 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 60,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1645 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 19063 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 330 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1590 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1548 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1590 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6282 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6282 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2878 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 62,6 |
| S4 Datensatz einfrieren | Queries (/s) | 8525 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 126 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1521 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1521 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1521 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 405 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 388 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 405 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 125 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 26,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2720 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13755 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 389 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 862 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 862 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 992 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3268 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1466 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2771 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1654 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 156 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 497 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 128 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1616 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 59684 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 559 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1081 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1081 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1081 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1302 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1446 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 320 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 25,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 52,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2799 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 42131 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 476 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 968 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 968 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 992 |
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
