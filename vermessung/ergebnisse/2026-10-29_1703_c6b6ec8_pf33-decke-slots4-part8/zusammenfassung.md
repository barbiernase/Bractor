# Vermessung pf33-decke-slots4-part8 (e2e)

Commit `c6b6ec8` (sauber), Branch `HEAD`, 2026-10-29T16:03:41.2316292+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.60 2.08 1.33 3/743 274847
 · CPU jetzt: 100 dotnet; 1 rcu_preempt; 1 dotnet; 1 containerd-shim  
Fremdlast nachher: Last 3.28 3.43 2.08 2/696 287710
 · CPU jetzt: 3 dockerd; 2 dotnet; 2 containerd; 2 consul; 2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 42,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 579 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,7 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1304 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1304 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1365 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 71684 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 71684 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8505 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 396 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1758 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 38136 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 18,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 24,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 12773 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 19727 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 4,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 44,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 22,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1719 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 39785 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 341 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1364 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1364 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1364 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 71684 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 71684 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1570 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1473 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1569 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1938 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 9,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 16,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 54,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 33,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1604 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 50501 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 518 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1012 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1012 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1012 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 31684 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 31684 |

- L5: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
