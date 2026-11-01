# Vermessung funktionen-4c-s5-parallel (e2e)

Commit `b539a7a` (sauber), Branch `HEAD`, 2026-11-01T09:54:08.3265385+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.63 1.73 1.53 2/717 4160407
 · CPU jetzt: 100 dotnet; 1 rcu_preempt; 1 consul; 1 dotnet  
Fremdlast nachher: Last 0.42 1.16 1.34 2/688 4163663
 · CPU jetzt: 2 consul; 2 redis-server; 2 redis-server; 2 dotnet  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 70,4 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 442 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 1,2 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4,7 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 793 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 731 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 794 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9411 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9411 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 409 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 407 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 409 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 17,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 29,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 22,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 25,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2374 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10120 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 207 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 745 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 745 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 745 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3256 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3256 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5889 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1033 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 473 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2474 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 5,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 27,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 422 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 5848 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 26,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 17,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 223 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7772 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 45,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 789 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 789 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 789 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9411 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9411 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1715 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1419 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1700 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 517 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 16,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 18,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 54,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 18,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1480 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 26675 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 245 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 768 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 768 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 768 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6302 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6302 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
