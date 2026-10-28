# Vermessung server-pf36-sonde-a (e2e)

Commit `6a132d2` (sauber), Branch `HEAD`, 2026-10-28T17:01:00.6492336+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 0.89 0.32 0.16 2/732 1360320
 · CPU-stärkste: 367 dotnet; 3.3 python3; 2.1 consul; 1.3 redis-server; 1.2 redis-server  
Fremdlast nachher: Last 0.70 0.52 0.27 2/731 1362655
 · CPU-stärkste: 53.2 dotnet; 3.9 python3; 3.7 dotnet; 3.5 postgres; 2.1 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 16,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 310 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,5 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 922 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 922 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 923 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 3528 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 3528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1805 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 332 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 220 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2448 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 6343 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12015 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Nachschub p50 (ms) | 2,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Nachschub p99 (ms) | 12,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 4,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 41,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 923 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8830 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 427 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 909 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 875 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 909 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 3528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3528 |

- L5: Exactly-once grün
- S5-8: Exactly-once grün
