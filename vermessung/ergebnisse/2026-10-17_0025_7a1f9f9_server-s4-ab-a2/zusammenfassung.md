# Vermessung server-s4-ab-a2 (e2e)

Commit `7a1f9f9` (sauber), Branch `HEAD`, 2026-10-16T22:25:35.6230466+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.27 1.60 1.36 2/718 1116349
 · CPU-stärkste: 393 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.80 1.44 1.35 1/689 1119095
 · CPU-stärkste: 46.0 dotnet; 3.5 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 53,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 403 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 3,8 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1874 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1874 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1874 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2267 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 6631 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 11,1 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 85,4 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,9 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3509 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6834 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 21,1 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 145 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1776 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12230 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 964 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1720 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1715 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1720 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1349 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,2 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 21,6 |
| S4 Datensatz einfrieren | Queries (/s) | 2971 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 620 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1720 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1720 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1720 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |

- L1: Exactly-once grün
- S1: Exactly-once grün
- S4: Exactly-once grün
