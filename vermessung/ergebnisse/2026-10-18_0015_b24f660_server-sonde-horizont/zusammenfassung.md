# Vermessung server-sonde-horizont (e2e)

Commit `b24f660` (sauber), Branch `HEAD`, 2026-10-17T22:15:18.5131928+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.85 1.64 2.11 1/751 2527413
 · CPU-stärkste: 407 dotnet; 2.5 python3; 2.0 consul; 1.5 redis-server; 1.2 redis-server  
Fremdlast nachher: Last 0.96 1.57 2.02 3/724 2530210
 · CPU-stärkste: 47.7 dotnet; 3.9 dotnet; 3.5 postgres; 3.4 python3; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 49,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 421 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,7 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1283 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1282 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1332 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2254 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8271 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 60,6 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5913 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9720 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 172 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1354 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10311 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 610 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1332 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1325 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1332 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |

- L1: Exactly-once grün
- S1: Exactly-once grün
