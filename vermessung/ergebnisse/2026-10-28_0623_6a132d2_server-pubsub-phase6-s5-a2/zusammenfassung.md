# Vermessung server-pubsub-phase6-s5-a2 (e2e)

Commit `6a132d2` (sauber), Branch `HEAD`, 2026-10-28T05:23:10.5143990+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.11 2.76 4.49 2/717 4085455
 · CPU-stärkste: 362 dotnet; 2.3 redis-server; 2.1 consul; 1.3 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.33 1.19 3.05 2/680 4093468
 · CPU-stärkste: 15.5 dotnet; 2.5 dotnet; 2.1 consul; 2.0 redis-server; 1.5 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 0,0 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 1,2 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,1 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 828 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 828 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 965 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9528 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9528 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 416 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 408 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 413 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 52,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 23,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 127 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 1104 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 26,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 821 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 806 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 821 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3528 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7239 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 65,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 363 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2973 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 7,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5061 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 7868 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 51,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 36,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1257 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12963 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 461 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 965 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 823 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 965 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1992 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 385 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 415 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2420 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3308 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 6122 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 38,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 88,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 957 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 25,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 845 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 789 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 845 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6528 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
