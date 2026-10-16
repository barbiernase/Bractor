# Vermessung server-s4-ab-b1 a78c21d (e2e)

Commit `0658d0d` (sauber), Branch `HEAD`, 2026-10-16T22:06:41.4284180+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 0.80 1.02 0.76 1/630 1090303
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 0.47 0.94 0.79 2/669 1093105
 · CPU-stärkste: 50.0 runc:[2:INIT];50.0 runc:[2:INIT]; 3.3 dotnet; 2.0 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 51,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 502 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1665 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1665 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1666 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4749 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 7,9 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 89,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3365 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6566 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 25,1 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 148 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2226 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13159 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1076 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1327 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1327 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1327 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1005 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,3 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 23,6 |
| S4 Datensatz einfrieren | Queries (/s) | 2683 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 756 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1425 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1425 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1425 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
