# Vermessung kette-rechner (e2e)

Commit `8c6df53` (sauber), Branch `HEAD`, 2026-11-03T07:08:53.6711355+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.68 2.39 1.22 3/713 2809029
 · CPU jetzt: 95 dotnet; 2 dotnet; 1 rcu_preempt; 1 kworker/18:2-mm_percpu_wq; 1 consul  
Fremdlast nachher: Last 0.64 1.80 1.33 2/687 2814023
 · CPU jetzt: 3 consul; 2 dotnet; 1 dockerd; 1 containerd; 1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 34,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 335 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,7 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1396 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1396 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1404 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 5211 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 5275 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 16,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 89,0 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,5 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3,1 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,5 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 741 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 737 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 741 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9893 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9893 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7299 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 143 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,1 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1183 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3229 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 4112 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 4,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 68,0 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 565 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 9646 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 896 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 979 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 944 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 979 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 813 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 813 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4404 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 835 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1910 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1621 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Erster Vorgang sichtbar nach (ms) | 20,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 391 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 590 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 8,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 660 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 11938 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 310 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1395 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1395 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1395 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 5075 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5075 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1335 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1295 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1333 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 60,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Erster Vorgang sichtbar nach (ms) | 162 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 6,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 18,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 9,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1713 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 11660 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 233 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1387 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1387 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1387 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2833 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2833 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2664 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 33,1 |
| S4 Datensatz einfrieren | Queries (/s) | 7818 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 103 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1177 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1177 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1177 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 817 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 817 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 411 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 407 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 411 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 32,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 27,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 16,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 25,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2260 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10079 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 209 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 680 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 680 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 680 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6081 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1498 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 3615 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2261 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 48,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 313 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 570 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 82,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 14,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 758 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 44232 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 174 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 728 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 728 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 728 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9893 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9893 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1505 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1407 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1498 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 195 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 14,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 19,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 46,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 18,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1344 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 23770 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 701 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 701 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 701 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6313 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6313 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2840 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2840 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2840 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 22,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 22,4 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 33,7 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 4,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 16,4 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,5 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 2452 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 35177 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 459 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1397 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1397 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1404 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 5275 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 5275 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 377 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 377 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 377 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 21,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 20,6 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 33,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 2,1 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 10,4 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 754 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 5976 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 214 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1402 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1402 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1402 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 5147 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 5147 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
- S6-64: Exactly-once grün
- S6-8: Exactly-once grün
