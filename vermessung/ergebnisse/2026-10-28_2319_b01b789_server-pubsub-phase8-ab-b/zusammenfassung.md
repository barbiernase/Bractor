# Vermessung server-pubsub-phase8-ab-b (e2e)

Commit `b01b789` (sauber), Branch `HEAD`, 2026-10-28T22:19:53.2698021+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.41 2.22 3.30 1/706 2139067
 · CPU-stärkste: 364 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.2 containerd  
Fremdlast nachher: Last 0.21 1.22 2.61 2/680 2143954
 · CPU-stärkste: 24.1 dotnet; 2.7 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1492 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1069 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1069 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1111 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4721 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4721 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7968 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 147 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,0 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2852 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3916 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 122 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1428 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 17905 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 760 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 860 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 860 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 860 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4636 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 614 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1513 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1735 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 561 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 880 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 737 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 13256 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 418 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1100 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1100 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1100 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4721 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4721 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1345 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1274 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1334 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 105 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 55,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1694 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13705 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 269 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1083 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1083 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1083 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2720 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2720 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
