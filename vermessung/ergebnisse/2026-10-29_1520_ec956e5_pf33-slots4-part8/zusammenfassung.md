# Vermessung pf33-slots4-part8 (e2e)

Commit `ec956e5` (sauber), Branch `HEAD`, 2026-10-29T14:20:53.5868170+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.33 1.28 0.60 1/679 129121
 · CPU jetzt: 4 dockerd; 2 containerd; 2 containerd-shim; 1 rcu_preempt; 1 kworker/u40:4-writeback  
Fremdlast nachher: Last 1.11 1.36 0.73 3/715 137508
 · CPU jetzt: 2 dockerd; 2 dotnet; 2 consul; 1 redis-server; 1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 15,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 373 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 895 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 895 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 937 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 433 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 422 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 433 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 79,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 9,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 14,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 49,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2339 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 20883 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 344 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 899 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 899 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 899 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7270 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 681 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1996 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2719 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 16,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 836 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1167 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 28,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 23,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1543 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 37915 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 247 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 937 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 937 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 937 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1754 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1477 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1745 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 474 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 20,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 15,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 39,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 34,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1467 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 47603 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 371 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 921 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 921 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 921 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
