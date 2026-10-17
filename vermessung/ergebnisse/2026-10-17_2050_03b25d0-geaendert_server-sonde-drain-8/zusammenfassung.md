# Vermessung server-sonde-drain-8 (e2e)

Commit `03b25d0` (2 geänderte Dateien (nicht committet)), Branch `HEAD`, 2026-10-17T18:50:03.4630434+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.16 1.50 1.59 1/650 2275726
 · CPU-stärkste: 2.2 python3; 2.0 consul; 1.4 redis-server; 1.2 redis-server; 1.0 postgres  
Fremdlast nachher: Last 1.08 1.38 1.43 1/686 2290908
 · CPU-stärkste: 4.1 postgres; 3.5 dotnet; 3.4 python3; 2.8 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 56,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 619 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 979 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 979 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 979 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 15,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 3018 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 2,0 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1331 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1329 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1358 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2798 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 302 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1276 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1784 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 711 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 867 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 17,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1788 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 19676 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 495 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 920 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 920 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 929 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 748 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 707 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 746 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 184 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 14,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 165 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 2,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 18,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1998 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13045 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 563 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 929 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 919 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 929 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 315 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 307 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 315 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 86,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 15,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 35,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 8,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 36,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2490 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 11007 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 527 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1039 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1039 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1039 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4690 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 544 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1254 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2652 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1030 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1671 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 4,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 28,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1918 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 29586 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 741 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1323 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 958 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1007 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 840 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 498 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 743 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2735 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 28582 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 758 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1158 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1158 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1158 |
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
