# Vermessung server-sonde-auto-prepare-20 (e2e)

Commit `03b25d0` (2 geänderte Dateien (nicht committet)), Branch `HEAD`, 2026-10-17T18:36:39.9118814+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.06 2.17 1.72 2/711 2259687
 · CPU-stärkste: 192 dotnet; 2.5 python3; 2.0 consul; 1.4 redis-server; 1.2 redis-server  
Fremdlast nachher: Last 1.20 1.52 1.60 1/684 2274232
 · CPU-stärkste: 4.3 postgres; 3.5 dotnet; 3.4 python3; 3.0 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 25,1 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 581 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,3 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 10,0 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,2 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 990 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 990 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 990 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 17,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1821 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 7,3 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,8 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1212 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1169 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1212 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3040 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 427 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1255 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1719 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 692 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 943 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1173 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 19231 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 502 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 977 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 977 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 977 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 785 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 690 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 783 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 168 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 14,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 169 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1204 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12375 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 566 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 905 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 905 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 905 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 337 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 326 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 336 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 98,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 14,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 36,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2384 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 11617 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 525 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 984 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 929 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 984 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4964 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 736 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1399 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2384 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 768 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1511 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 4,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 188 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 27,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1654 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 32987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 777 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1162 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1162 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1162 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1437 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1103 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1139 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 696 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 360 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 606 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2380 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 31696 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 723 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1083 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1083 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1083 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
