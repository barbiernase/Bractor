# Vermessung gesamtpruefung-profil (e2e)

Commit `05ed98a` (sauber), Branch `HEAD`, 2026-10-29T09:38:06.7832017+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.27 8.64 7.71 1/651 4018885
 · CPU jetzt: 7 dotnet; 4 python3; 3 postgres; 1 containerd-shim; 1 containerd-shim  
Fremdlast nachher: Last 0.78 2.81 5.16 2/677 4027284
 · CPU jetzt: 7 python3; 6 containerd; 4 dockerd; 4 postgres; 3 dotnet  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1481 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1262 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1262 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1298 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 859 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 923 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 57,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 593 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 817 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 817 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 922 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8328 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 145 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,1 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1599 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2968 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 4051 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 104 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1389 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 14751 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 821 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1021 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 969 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1021 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 433 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 427 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 433 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 39,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 17,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2299 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19625 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 362 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 799 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 799 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 850 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8381 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 36,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 248 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 11,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 68,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 6038 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 11607 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 23,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 38,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 948 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 9327 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 284 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 922 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 809 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 922 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2078 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 423 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 305 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2390 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 44,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3982 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8323 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1137 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11832 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 277 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 856 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 798 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 856 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2375 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2375 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2375 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 29,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 23,1 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 114 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 5,1 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 18,7 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,5 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 1967 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 29545 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 313 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1210 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1210 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1298 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 923 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 923 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 388 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 388 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 388 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 23,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 19,2 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 33,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 1,7 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 10,5 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 760 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 6113 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 250 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1298 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1298 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1298 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 791 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 791 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
- S6-64: Exactly-once grün
- S6-8: Exactly-once grün
