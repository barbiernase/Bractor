# Vermessung server-s4-ab-a2 7a1f9f9 (e2e)

Commit `0658d0d` (sauber), Branch `HEAD`, 2026-10-16T22:12:34.4241621+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.60 1.31 0.97 1/632 1098380
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.6 dotnet; 0.1 containerd  
Fremdlast nachher: Last 1.06 1.54 1.14 2/638 1101095
 · CPU-stärkste: 3.8 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.9 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 52,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 504 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1289 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1284 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1715 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,7 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5666 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 9,4 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 92,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,4 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3282 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6384 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 25,1 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 147 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2304 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13542 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1103 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1296 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1296 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1296 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 969 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,3 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 22,5 |
| S4 Datensatz einfrieren | Queries (/s) | 2622 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 470 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1606 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1606 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1606 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
