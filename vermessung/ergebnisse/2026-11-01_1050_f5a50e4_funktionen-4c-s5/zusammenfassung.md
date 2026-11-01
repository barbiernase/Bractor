# Vermessung funktionen-4c-s5 (e2e)

Commit `f5a50e4` (sauber), Branch `HEAD`, 2026-11-01T09:50:17.6983719+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.85 2.13 1.62 2/716 4154959
 · CPU jetzt: 100 dotnet; 1 redis-server; 1 redis-server; 1 dotnet  
Fremdlast nachher: Last 0.68 1.48 1.45 2/681 4157706
 · CPU jetzt: 2 consul; 2 dotnet; 1 containerd; 1 redis-server; 1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 29,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 142 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 1,3 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4,7 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 692 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 692 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 692 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9242 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9242 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 450 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 450 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 135 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 10,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 19,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 25,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2575 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 11035 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 227 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 646 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 646 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 646 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3240 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3240 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8168 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 49,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 300 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2982 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 15,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5698 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 9592 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 1,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 5,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 4,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 23,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1255 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 6747 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 114 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 691 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 690 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 691 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9242 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9242 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2388 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 550 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 310 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2309 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 11,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 1794 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8375 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 4,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 23,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1313 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 7078 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 99,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 672 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 672 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 672 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6242 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6242 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
