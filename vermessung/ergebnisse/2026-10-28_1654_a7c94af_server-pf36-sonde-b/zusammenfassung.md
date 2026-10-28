# Vermessung server-pf36-sonde-b (e2e)

Commit `a7c94af` (sauber), Branch `HEAD`, 2026-10-28T15:54:10.0569161+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.59 0.51 0.25 1/648 1296892
 · CPU-stärkste: 2.8 python3; 2.1 consul; 1.3 redis-server; 1.2 redis-server; 0.9 postgres  
Fremdlast nachher: Last 0.64 0.60 0.32 2/688 1299211
 · CPU-stärkste: 4.1 python3; 4.0 dotnet; 3.8 postgres; 2.1 consul; 1.5 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 18,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 312 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 939 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 939 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 939 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 3458 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 3458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1589 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 254 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 255 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2521 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 9,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 6483 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 10555 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Nachschub p50 (ms) | 2,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Nachschub p99 (ms) | 13,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 4,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 41,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1093 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 10365 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 450 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 937 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 892 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 937 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 3458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3458 |

- L5: Exactly-once grün
- S5-8: Exactly-once grün
