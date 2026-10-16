# Vermessung server-s4-ab-a1 7a1f9f9 (e2e)

Commit `0658d0d` (sauber), Branch `HEAD`, 2026-10-16T22:03:44.6835326+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.00 1.17 0.75 1/630 1086223
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.8 systemd; 0.5 dotnet  
Fremdlast nachher: Last 0.35 0.96 0.74 2/639 1088961
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
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1545 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1545 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1545 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5598 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 18,7 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 91,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 598 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3320 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6486 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 25,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 148 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2262 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13357 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1047 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1309 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1309 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1309 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1019 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,3 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 23,8 |
| S4 Datensatz einfrieren | Queries (/s) | 2773 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 866 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1314 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1314 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1314 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
