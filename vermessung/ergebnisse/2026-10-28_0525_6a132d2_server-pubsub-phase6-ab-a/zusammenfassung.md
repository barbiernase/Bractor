# Vermessung server-pubsub-phase6-ab-a (e2e)

Commit `6a132d2` (sauber), Branch `HEAD`, 2026-10-28T04:25:38.4649074+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.66 2.70 2.06 2/718 4001944
 · CPU-stärkste: 330 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.2 containerd  
Fremdlast nachher: Last 0.14 0.91 1.43 2/641 4013176
 · CPU-stärkste: 2.6 dotnet; 2.1 consul; 1.6 postgres; 1.3 redis-server; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1492 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1048 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1048 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1163 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 5056 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 5056 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 0,0 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 2,4 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 596 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 596 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 792 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9528 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9528 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 9479 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 159 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2613 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3624 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 89,7 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1498 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13854 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 834 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 980 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 980 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 980 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 1043 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1043 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 5066 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 570 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1454 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1775 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 604 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 977 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 831 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 14561 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 421 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1117 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1117 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1163 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 5056 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5056 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1346 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1028 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1309 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 472 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 24,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 80,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1632 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12687 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 456 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1163 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1115 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1163 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 3055 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3055 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 402 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 391 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 400 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 79,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 8,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 21,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 127 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 1100 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 28,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 792 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 587 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 792 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3528 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7506 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 72,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 329 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2971 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 8,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5026 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 8772 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 91,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 36,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1147 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 11705 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 410 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 713 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 590 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 792 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1962 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 314 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 343 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2520 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 7,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3602 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 7155 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1259 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 13182 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 387 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 634 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 582 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 792 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6528 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
