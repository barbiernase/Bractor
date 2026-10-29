# Vermessung gesamtpruefung-ab-a (e2e)

Commit `4f43172` (sauber), Branch `HEAD`, 2026-10-29T09:46:43.8900915+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.15 2.93 5.12 2/709 4029041
 · CPU-stärkste: 368 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.2 containerd  
Fremdlast nachher: Last 1.05 1.51 3.63 3/683 4036197
 · CPU-stärkste: 17.8 dotnet; 4.0 postgres; 3.2 dotnet; 2.1 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1492 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1035 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1035 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1160 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4992 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4992 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 3,0 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 297 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 871 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 870 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 975 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9458 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9458 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 9514 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 158 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2637 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3725 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 89,9 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1507 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13875 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 793 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 979 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 936 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 979 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 990 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 990 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4716 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 502 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1432 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1787 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 568 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 965 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 822 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 14244 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 382 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1149 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1149 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1160 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4992 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4992 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1259 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 987 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1253 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 432 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 14,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 84,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1549 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12305 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 408 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1160 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1160 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1160 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2991 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2991 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 429 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 413 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 428 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 107 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 18,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2288 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19713 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 357 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 865 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 857 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 865 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3458 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7868 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 86,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 334 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2967 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 8,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5348 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 8439 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 35,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 37,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1237 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12334 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 372 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 975 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 870 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 975 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1451 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 302 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 314 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2376 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 7,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4334 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 7435 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1167 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 12201 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 346 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 897 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 860 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 897 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6458 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
