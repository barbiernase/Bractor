# Vermessung server-sonde-horizont-2 (e2e)

Commit `60e561a` (sauber), Branch `HEAD`, 2026-10-17T22:19:05.0272531+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.11 2.15 2.18 2/761 2532855
 · CPU-stärkste: 402 dotnet; 2.0 consul; 1.9 python3; 1.4 redis-server; 1.2 redis-server  
Fremdlast nachher: Last 0.75 1.47 1.91 1/745 2535670
 · CPU-stärkste: 47.8 dotnet; 4.1 dotnet; 4.0 python3; 3.5 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 49,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1275 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 20,9 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1377 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1313 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1377 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2254 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7338 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 61,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5911 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9535 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 171 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1379 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10421 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 632 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1377 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1377 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1377 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |

- L1: Exactly-once grün
- S1: Exactly-once grün
