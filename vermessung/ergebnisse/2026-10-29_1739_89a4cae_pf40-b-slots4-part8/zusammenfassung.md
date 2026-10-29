# Vermessung pf40-b-slots4-part8 (e2e)

Commit `89a4cae` (sauber), Branch `HEAD`, 2026-10-29T16:39:07.4915671+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.75 2.74 2.07 2/1081 358260
 · CPU jetzt: 33 dotnet; 1 dotnet; 1 consul; 1 redis-server; 1 redis-server  
Fremdlast nachher: Last 4.83 3.47 2.49 2/1055 371445
 · CPU jetzt: 2 dotnet; 2 redis-server; 1 dockerd; 1 containerd; 1 containerd-shim  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 42,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 578 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,8 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1411 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1411 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1471 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 71903 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 71903 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8826 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 438 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1830 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 38017 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 17,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 62,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 12453 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 18023 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 4,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 50,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 22,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1715 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 41191 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 328 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1471 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1470 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1471 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 71903 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 71903 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1581 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1460 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1580 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2424 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 13,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 16,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 47,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 34,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1554 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 49809 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 490 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1059 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1059 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1059 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 31903 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 31903 |

- L5: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
