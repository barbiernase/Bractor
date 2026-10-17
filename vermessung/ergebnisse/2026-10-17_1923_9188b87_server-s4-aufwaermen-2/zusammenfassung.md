# Vermessung server-s4-aufwaermen-2 (e2e)

Commit `9188b87` (sauber), Branch `HEAD`, 2026-10-17T17:23:23.2947178+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.45 0.95 0.99 1/632 2175279
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 0.52 0.84 0.95 1/640 2178066
 · CPU-stärkste: 3.8 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.7 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 47,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 495 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,2 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1761 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1760 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1761 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2270 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 9301 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,1 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6183 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10629 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 26,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 173 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1486 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 9666 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 703 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1356 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1356 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1356 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1847 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 50,2 |
| S4 Datensatz einfrieren | Queries (/s) | 5635 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
