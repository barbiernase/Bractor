# Vermessung server-s4-ab-b1 (e2e)

Commit `a78c21d` (sauber), Branch `HEAD`, 2026-10-16T22:19:27.0852395+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.60 1.81 1.34 2/711 1107789
 · CPU-stärkste: 395 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.46 1.30 1.22 2/680 1110536
 · CPU-stärkste: 45.0 dotnet; 3.5 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 53,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 506 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1942 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1942 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1942 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4567 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 89,6 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3422 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6463 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 25,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 150 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2215 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13309 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1015 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1789 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1789 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1789 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1683 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 34,1 |
| S4 Datensatz einfrieren | Queries (/s) | 3864 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
