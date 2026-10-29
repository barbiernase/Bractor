# Vermessung gesamtpruefung-profil-probe (e2e)

Commit `7511e5a` (sauber), Branch `HEAD`, 2026-10-29T10:09:31.3194107+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.29 0.82 1.76 2/742 4055248
 · CPU jetzt: 100 dotnet; 5 dotnet-counters; 3 dotnet; 2 python3; 1 rcu_preempt  
Fremdlast nachher: Last 0.91 1.28 1.68 2/718 4064168
 · CPU jetzt: 4 dotnet; 4 postgres; 3 python3; 2 consul; 2 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1481 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,5 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1341 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1341 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1348 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 1610 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 1674 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 58,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 594 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 833 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 833 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 895 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7589 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 147 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,0 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1446 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2808 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3999 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 103 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1428 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 15047 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 879 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 833 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 833 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 833 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 432 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 427 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 431 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 34,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 17,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 17,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2270 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19369 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 352 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 762 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 762 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 853 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8285 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 105 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 269 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2962 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 10,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 89,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 6047 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 10809 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 121 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 38,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1029 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 10103 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 298 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 895 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 784 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 895 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2027 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 356 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 298 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2473 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 38,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3893 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8735 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1128 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11580 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 293 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 786 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 762 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 853 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2422 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2422 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2422 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 17,2 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 26,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 49,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 6,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 25,3 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,3 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 1922 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 29717 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 463 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1287 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1287 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1287 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 1674 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 1674 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 397 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 397 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 397 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 39,7 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 19,2 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 32,2 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 1,7 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 10,5 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 781 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 6269 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 226 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1216 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1196 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1216 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 850 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 850 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
- S6-64: Exactly-once grün
- S6-8: Exactly-once grün
