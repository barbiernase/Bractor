# Vermessung funktionen-4c-s5-p256 (e2e)

Commit `ef6baf8` (sauber), Branch `HEAD`, 2026-11-01T09:58:09.6300301+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.11 1.68 1.51 2/722 4166253
 · CPU jetzt: 101 dotnet; 2 dotnet; 1 rcu_preempt; 1 consul; 1 redis-server  
Fremdlast nachher: Last 3.43 1.74 1.52 2/691 4169030
 · CPU jetzt: 3 consul; 2 redis-server; 2 dotnet; 1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 47,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 335 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 1,3 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4,7 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 615 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 615 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 668 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9757 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9757 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 477 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 469 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 476 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 52,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 10,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 18,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 25,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2745 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 11826 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 227 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 668 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 593 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 668 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3258 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3258 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5047 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1388 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 3292 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2175 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 20,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 267 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 366 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 85,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 15,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 593 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 40825 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 152 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 657 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 657 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 668 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9757 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9757 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1820 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1663 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1802 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 259 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 15,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 16,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 44,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 18,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1638 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 29450 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 147 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 615 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 615 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 668 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6302 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6302 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
