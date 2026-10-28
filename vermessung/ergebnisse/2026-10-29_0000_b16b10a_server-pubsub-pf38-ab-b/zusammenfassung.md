# Vermessung server-pubsub-pf38-ab-b (e2e)

Commit `b16b10a` (sauber), Branch `HEAD`, 2026-10-28T23:00:43.0009537+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.15 1.32 1.37 2/714 2191397
 · CPU-stärkste: 390 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.32 0.77 1.14 1/678 2196196
 · CPU-stärkste: 26.0 dotnet; 2.1 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1488 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 937 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 937 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 939 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 719 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5912 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 152 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2801 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3840 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 104 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1453 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 15455 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 843 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 920 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 920 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 920 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |

- L1: Exactly-once grün
- S1: Exactly-once grün
