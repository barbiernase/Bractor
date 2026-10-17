# Vermessung server-sonde-profil-s1-s3 (e2e)

Commit `e93bfbb` (sauber), Branch `HEAD`, 2026-10-17T20:29:41.9597300+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 0.96 0.71 1.82 2/736 2411549
 · CPU-stärkste: 398 dotnet; 2.5 python3; 2.0 consul; 1.5 redis-server; 1.2 redis-server  
Fremdlast nachher: Last 0.97 1.10 1.81 3/715 2414402
 · CPU-stärkste: 46.0 dotnet; 4.5 dotnet; 3.6 python3; 3.6 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 43,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 712 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,8 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1545 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1545 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1545 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 10079 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 58,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,3 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6030 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10150 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 26,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 170 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1536 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 9836 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 672 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1259 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1259 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1259 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3019 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 352 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1123 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1767 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 713 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 1138 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 947 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 16013 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 277 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1482 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1482 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1482 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10267 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10267 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 826 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 735 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 823 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 219 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 21,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 61,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1281 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13803 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 344 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1471 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1471 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1471 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6266 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6266 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
