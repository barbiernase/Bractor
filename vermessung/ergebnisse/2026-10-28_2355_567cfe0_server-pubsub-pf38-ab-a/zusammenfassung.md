# Vermessung server-pubsub-pf38-ab-a (e2e)

Commit `567cfe0` (sauber), Branch `HEAD`, 2026-10-28T22:55:34.8425802+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.48 1.97 1.61 1/630 2184966
 · CPU-stärkste: 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.5 dotnet; 0.2 containerd  
Fremdlast nachher: Last 0.34 0.95 1.26 2/640 2189764
 · CPU-stärkste: 2.3 dotnet; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.4 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1485 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 904 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 896 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 904 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 719 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8435 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 155 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2791 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3811 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 91,8 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1511 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 14149 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 807 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 883 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 883 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 883 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |

- L1: Exactly-once grün
- S1: Exactly-once grün
