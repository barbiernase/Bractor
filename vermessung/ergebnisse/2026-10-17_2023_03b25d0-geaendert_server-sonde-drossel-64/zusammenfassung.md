# Vermessung server-sonde-drossel-64 (e2e)

Commit `03b25d0` (1 geänderte Dateien (nicht committet)), Branch `HEAD`, 2026-10-17T18:23:10.8286250+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.77 1.82 1.48 2/706 2243244
 · CPU-stärkste: 187 dotnet; 2.2 python3; 2.0 consul; 1.4 redis-server; 1.2 redis-server  
Fremdlast nachher: Last 3.92 2.04 1.67 1/684 2258072
 · CPU-stärkste: 4.2 postgres; 3.5 dotnet; 3.5 python3; 2.6 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 26,1 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 468 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,6 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 924 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 923 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 964 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 15,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1722 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 2,1 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1305 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1242 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1309 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2162 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 1459 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 2021 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 690 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 137 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 227 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 527 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 26431 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 511 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 914 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 914 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 919 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 702 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 636 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 701 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 188 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 18,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 124 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1155 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 11865 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 490 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 919 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 908 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 919 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 286 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 281 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 286 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 51,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 17,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 41,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2055 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 9994 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 467 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1005 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 987 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1005 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2612 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 805 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1212 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1923 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 942 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1455 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 5,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 242 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1441 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 28692 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 683 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1304 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1304 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1304 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1123 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 877 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 995 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 673 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 231 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 516 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1814 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 27255 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 689 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1124 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1124 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1124 |
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
