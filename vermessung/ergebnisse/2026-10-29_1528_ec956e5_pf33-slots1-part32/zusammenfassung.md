# Vermessung pf33-slots1-part32 (e2e)

Commit `ec956e5` (sauber), Branch `HEAD`, 2026-10-29T14:28:26.4031220+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.76 2.13 1.18 1/677 162776
 · CPU jetzt: 3 dotnet; 3 consul; 2 redis-server; 1 dockerd; 1 containerd  
Fremdlast nachher: Last 0.45 1.35 1.03 2/644 172987
 · CPU jetzt: 3 dotnet; 2 consul; 1 dockerd; 1 sshd; 1 containerd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 45,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 2165 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 24,5 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 921 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 921 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 963 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9535 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9535 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 416 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 412 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 415 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 28,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 16,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 49,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2269 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19710 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 353 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 918 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 918 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 918 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3535 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3535 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6982 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 598 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1948 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2743 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 39,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 797 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1179 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 136 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 22,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1637 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 36847 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 257 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 962 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 962 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 962 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9535 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9535 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1737 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1395 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1721 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 591 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 21,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 22,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 61,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 34,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1615 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 50509 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 363 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 937 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 937 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 937 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6535 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6535 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
