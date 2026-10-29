# Vermessung pf33-slots16-part32 (e2e)

Commit `ec956e5` (sauber), Branch `HEAD`, 2026-10-29T14:25:35.4667217+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 0.71 1.14 0.74 1/680 150406
 · CPU jetzt: 2 dotnet; 2 consul; 2 redis-server; 1 dockerd; 1 dotnet  
Fremdlast nachher: Last 3.47 2.02 1.14 2/689 160589
 · CPU jetzt: 2 dockerd; 2 containerd; 2 consul; 2 redis-server; 1 dotnet  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 44,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 2164 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 24,5 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,4 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 901 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 901 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 938 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9535 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9535 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 397 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 383 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 396 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 103 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 8,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 17,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 50,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2192 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19214 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 356 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 825 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 825 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 825 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3535 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3535 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3114 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 819 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1533 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2211 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 52,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 636 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1397 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 819 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 25,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 664 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 34955 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 244 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 934 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 934 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 934 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9535 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9535 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1691 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1588 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1684 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 184 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 17,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 13,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 35,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 42,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1655 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 63989 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 279 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 825 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 819 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 827 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6535 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6535 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
