# Vermessung server-pubsub-phase8-ab-a (e2e)

Commit `567cfe0` (sauber), Branch `HEAD`, 2026-10-28T22:14:28.2073415+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.78 4.63 4.36 2/708 2132303
 · CPU-stärkste: 355 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.2 containerd  
Fremdlast nachher: Last 0.34 1.91 3.26 1/680 2137212
 · CPU-stärkste: 25.1 dotnet; 2.7 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1491 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 1,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1119 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1119 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1154 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4721 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4721 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7229 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 158 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2621 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3716 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 90,5 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1487 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13900 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 839 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1024 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1024 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1024 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3540 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 627 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1466 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1646 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 632 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 807 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 744 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 13791 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 472 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1109 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1109 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1154 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4721 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4721 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1348 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1223 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1334 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 186 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 14,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 76,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1725 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13781 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 283 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1135 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1135 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1135 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2720 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2720 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
