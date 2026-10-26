# Vermessung server-pubsub-phase4-diag-slots16 (e2e)

Commit `a2b812b` (1 geänderte Dateien (nicht committet)), Branch `HEAD`, 2026-10-26T17:52:14.4165207+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.15 0.98 1.39 2/650 1888973
 · CPU-stärkste: 75.0 consul; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 0.42 0.82 1.28 2/661 1891295
 · CPU-stärkste: 5.4 dotnet; 3.1 postgres; 2.1 consul; 1.3 redis-server; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 16,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 272 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,3 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 890 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 847 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 890 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9507 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9507 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 389 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 387 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 389 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 15,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 18,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 50,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2045 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 18716 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 405 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 787 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 743 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 787 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3507 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4418 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1353 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1294 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2081 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 505 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1623 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 253 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 21,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 682 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 25817 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 336 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 890 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 890 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 890 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9507 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1626 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1367 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1617 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 478 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 15,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 37,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 37,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1389 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 49600 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 377 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 778 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 778 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 787 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6507 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6507 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
