# Vermessung pf33-slots1-part8 (e2e)

Commit `ec956e5` (sauber), Branch `HEAD`, 2026-10-29T14:18:32.3722092+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.09 0.85 0.37 2/719 118622
 · CPU jetzt: 100 dotnet; 2 consul; 1 rcu_preempt; 1 dockerd; 1 dotnet  
Fremdlast nachher: Last 1.35 1.05 0.51 2/690 127007
 · CPU jetzt: 3 dotnet; 3 consul; 1 migration/17; 1 containerd; 1 containerd-shim  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 12,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 370 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 921 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 921 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 921 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 444 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 439 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 443 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 33,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 8,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 15,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 45,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2364 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19660 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 323 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8726 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 2,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 807 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2999 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 3,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 61,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1967 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 3396 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 45,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 2,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 23,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1819 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 17457 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 210 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 920 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 920 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 920 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2138 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 651 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 800 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2087 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 2,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 22,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 1320 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 2714 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 27,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1896 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 20993 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 260 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 900 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 900 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 900 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
