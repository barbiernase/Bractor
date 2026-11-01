# Vermessung funktionen-nachher (e2e)

Commit `2172f4f` (sauber), Branch `HEAD`, 2026-11-01T14:01:36.4298377+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.63 1.37 1.29 3/718 242874
 · CPU jetzt: 101 dotnet; 2 consul; 1 kworker/18:1-events; 1 tmux: server; 1 dotnet  
Fremdlast nachher: Last 0.29 0.87 1.12 1/687 249159
 · CPU jetzt: 2 dockerd; 2 consul; 2 redis-server; 1 dotnet; 1 containerd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 34,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 339 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,7 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1650 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1650 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1673 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 5145 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 5209 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 16,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 104 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,5 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,6 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 545 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 544 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 656 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9784 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9784 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7221 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 135 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,4 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1471 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3048 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 4357 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 7,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 88,6 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1022 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11776 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 926 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1020 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1020 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1020 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 751 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 751 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 7690 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 37,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 5,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Erster Vorgang sichtbar nach (ms) | 1516 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 3516 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 5240 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 8,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 115 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 315 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 4222 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 171 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 201 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 997 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 997 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 997 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 282 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 282 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4867 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 840 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 2005 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1655 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Erster Vorgang sichtbar nach (ms) | 9,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 414 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 591 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 8,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 812 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 14517 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 270 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1667 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1667 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1667 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 5013 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5013 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1437 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1190 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1436 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 344 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Erster Vorgang sichtbar nach (ms) | 154 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 6,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 25,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 9,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1911 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13172 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 221 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1655 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1655 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1655 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2774 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2774 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2546 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 32,0 |
| S4 Datensatz einfrieren | Queries (/s) | 7551 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 755 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 755 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 416 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 401 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 415 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 103 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 10,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 35,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 25,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2359 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10200 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 203 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 488 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 488 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 656 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3241 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3241 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5065 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1067 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 3269 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2368 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 24,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 304 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 395 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 70,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 15,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 582 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 34627 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 157 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 523 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 523 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 656 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9784 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9784 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1856 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1621 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1838 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 380 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 10,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 16,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 47,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 18,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1657 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 29985 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 186 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 505 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 505 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 656 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6285 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6285 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2884 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2884 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2884 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 18,5 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 22,5 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 32,2 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 4,7 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 13,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 2636 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 36109 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 463 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1646 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1646 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1673 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 5209 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 5209 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 381 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 381 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 381 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 22,7 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 20,4 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 30,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 2,1 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 8,2 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 756 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 6013 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 203 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1673 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1672 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1673 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 5085 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 5085 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S2: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
- S6-64: Exactly-once grün
- S6-8: Exactly-once grün
