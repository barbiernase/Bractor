# Vermessung pf40-b-slots16-part8 (e2e)

Commit `89a4cae` (sauber), Branch `HEAD`, 2026-10-29T16:42:39.0914287+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.30 3.62 2.55 1/1043 373774
 · CPU jetzt: 2 dockerd; 2 dotnet; 2 consul; 2 redis-server; 1 containerd  
Fremdlast nachher: Last 4.54 4.10 2.98 2/1049 386698
 · CPU jetzt: 2 dockerd; 2 dotnet; 2 containerd; 2 consul; 1 containerd-shim  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 47,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 584 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,7 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1229 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1229 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1350 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 71068 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 71068 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5625 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 861 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2404 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 33877 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 34,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 6166 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 11680 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 5,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 267 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 21,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1283 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 50645 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 403 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1350 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1292 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1350 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 71068 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 71068 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1540 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1395 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1539 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2888 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 24,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 15,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 40,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 36,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1513 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 51565 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 507 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 947 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 947 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 947 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 31068 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 31068 |

- L5: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
