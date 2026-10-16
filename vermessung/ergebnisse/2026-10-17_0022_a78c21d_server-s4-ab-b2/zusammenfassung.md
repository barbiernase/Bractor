# Vermessung server-s4-ab-b2 (e2e)

Commit `a78c21d` (sauber), Branch `HEAD`, 2026-10-16T22:22:26.6560007+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.43 1.45 1.27 1/631 1111977
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.6 dotnet; 0.1 containerd  
Fremdlast nachher: Last 0.47 1.23 1.23 1/642 1114752
 · CPU-stärkste: 3.8 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.9 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 53,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 506 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1739 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1734 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1783 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4313 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 21,6 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 90,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 597 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3380 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6476 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 25,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 150 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2255 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13515 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1023 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1664 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1664 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1664 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1724 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 35,5 |
| S4 Datensatz einfrieren | Queries (/s) | 4015 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 803 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1771 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1771 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1771 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
