# Vermessung server-s4-ab-b2 a78c21d (e2e)

Commit `0658d0d` (sauber), Branch `HEAD`, 2026-10-16T22:09:37.6933823+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.13 1.06 0.83 1/630 1094382
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 0.76 1.16 0.92 1/646 1097027
 · CPU-stärkste: 3.7 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.8 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 52,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 504 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1653 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1653 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1653 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4654 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 90,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3319 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6386 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 24,9 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 147 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2244 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13282 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1084 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1274 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1274 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1274 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 986 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,3 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 22,7 |
| S4 Datensatz einfrieren | Queries (/s) | 2645 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 451 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1570 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1570 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1570 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
