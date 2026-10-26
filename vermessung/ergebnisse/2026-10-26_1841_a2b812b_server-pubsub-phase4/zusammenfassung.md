# Vermessung server-pubsub-phase4 (e2e)

Commit `a2b812b` (sauber), Branch `HEAD`, 2026-10-26T17:41:57.4139290+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.52 1.82 2.01 1/1036 1877058
 · CPU-stärkste: 23.4 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.7 dotnet  
Fremdlast nachher: Last 0.96 1.02 1.45 2/648 1886323
 · CPU-stärkste: 3.0 dotnet; 2.1 consul; 1.9 postgres; 1.3 redis-server; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1491 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1118 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1095 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1119 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 5064 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 5064 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 2,0 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 257 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,3 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 654 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 652 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 778 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9507 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9507 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7049 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 159 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2606 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3694 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 89,8 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1524 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 14164 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 841 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 915 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 881 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 915 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 1051 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1051 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 5050 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 667 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1469 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1736 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 667 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 968 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 789 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 14467 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 478 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1107 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1107 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1107 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 5064 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5064 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1305 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1172 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1260 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 204 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 23,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 89,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1683 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13093 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 468 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1106 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1106 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1106 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 3063 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3063 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 387 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 379 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 385 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 68,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 8,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 25,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 126 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 1092 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 28,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 778 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 654 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 778 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3507 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7532 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 82,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 302 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2967 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5210 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 9590 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 185 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 37,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1060 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 10813 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 399 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 776 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 653 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 778 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9507 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1994 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 360 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 306 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2459 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3671 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8514 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 38,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1109 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11722 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 323 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 745 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 638 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 778 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6507 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6507 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
