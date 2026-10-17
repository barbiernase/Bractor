# Vermessung server-sonde-keiner (e2e)

Commit `03b25d0` (sauber), Branch `HEAD`, 2026-10-17T19:03:58.8144655+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.21 1.40 1.43 1/649 2292331
 · CPU-stärkste: 2.3 python3; 2.0 consul; 1.4 redis-server; 1.2 redis-server; 0.9 postgres  
Fremdlast nachher: Last 1.01 1.10 1.21 2/684 2307250
 · CPU-stärkste: 4.1 postgres; 3.6 dotnet; 3.5 python3; 2.7 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 25,1 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 461 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,6 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 929 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 924 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 983 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 14,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 2774 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1264 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1263 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1264 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2627 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 320 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1139 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1751 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 786 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 994 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1067 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 17713 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 438 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 959 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 959 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 959 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 657 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 615 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 656 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 117 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 16,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 177 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1054 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 10905 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 524 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 907 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 907 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 907 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 287 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 282 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 286 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 45,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 17,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 42,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2073 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10032 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 482 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 998 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 998 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 998 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3317 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 590 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1209 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2486 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 940 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1596 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 110 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 27,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1723 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 30755 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 703 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1235 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1235 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1235 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1130 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 917 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1006 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 608 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 291 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 452 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1823 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 26993 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 642 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1130 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1130 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1130 |
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
