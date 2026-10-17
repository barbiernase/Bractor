# Vermessung server-s4-aufwaermen-1 (e2e)

Commit `9188b87` (sauber), Branch `HEAD`, 2026-10-17T17:20:30.1206195+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.12 0.63 0.91 1/718 2171217
 · CPU-stärkste: 401 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.4 systemd  
Fremdlast nachher: Last 0.78 0.80 0.95 2/692 2173945
 · CPU-stärkste: 100 ps;47.7 dotnet; 3.7 dotnet; 2.0 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 47,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 495 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1538 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1538 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1568 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2270 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8060 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6322 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10605 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 26,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 174 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1478 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 9646 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 674 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1124 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1098 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1124 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1942 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 52,6 |
| S4 Datensatz einfrieren | Queries (/s) | 5899 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
