# Vermessung pf33-slots16-part8 (e2e)

Commit `ec956e5` (sauber), Branch `HEAD`, 2026-10-29T14:23:14.5858555+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.17 1.36 0.74 1/687 139832
 · CPU jetzt: 3 dotnet; 2 consul; 2 redis-server; 1 dockerd; 1 kworker/u40:5-writeback  
Fremdlast nachher: Last 0.62 1.15 0.74 2/703 148200
 · CPU jetzt: 2 dockerd; 2 dotnet; 2 consul; 1 sshd; 1 containerd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 15,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 373 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 979 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 916 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 979 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 406 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 393 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 406 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 94,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 11,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 16,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 50,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2198 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19513 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 349 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 861 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 857 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 861 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5043 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1007 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2326 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2401 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 38,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 453 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 852 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 219 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 20,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 866 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 37461 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 238 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 978 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 978 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 978 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1797 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1573 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1784 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 373 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 11,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 13,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 29,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 36,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1720 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 61464 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 331 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 880 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 880 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 880 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
