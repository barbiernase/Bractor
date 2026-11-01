# Vermessung funktionen-vorher (e2e)

Commit `4dee5c0` (sauber), Branch `HEAD`, 2026-11-01T08:17:10.5947470+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.87 0.83 0.34 2/716 4009152
 · CPU jetzt: 100 dotnet; 2 dotnet; 1 rcu_preempt  
Fremdlast nachher: Last 0.90 1.15 0.78 2/691 4017597
 · CPU jetzt: 3 consul; 2 containerd; 2 redis-server; 1 dockerd; 1 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1951 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 9,0 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1412 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1411 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1541 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4872 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4936 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 14,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 372 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 851 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 850 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 851 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7682 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 128 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,6 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1332 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3118 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 4603 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 11,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 140 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1498 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 17868 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 911 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 876 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 876 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 876 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 735 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 735 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 9013 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 51,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Erster Vorgang sichtbar nach (ms) | 1530 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2884 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3841 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 15,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 144 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 772 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 7314 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 187 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 344 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 906 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 811 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 906 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 341 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 341 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 5602 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 291 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 2130 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1896 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Erster Vorgang sichtbar nach (ms) | 19,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 423 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 584 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 7,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 833 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 14479 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 204 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1535 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1535 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1535 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4739 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4739 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1364 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1301 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1363 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 93,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Erster Vorgang sichtbar nach (ms) | 174 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 6,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 41,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 13,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1730 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 15693 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 370 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1527 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1527 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1527 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2738 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2738 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2627 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 49,0 |
| S4 Datensatz einfrieren | Queries (/s) | 7773 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 737 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 737 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 372 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 358 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 372 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 114 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 13,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 22,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 49,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2018 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 17782 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 339 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 810 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 806 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 810 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6924 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 607 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2086 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2737 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 15,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 807 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1121 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 25,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 22,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1411 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 39483 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 428 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 851 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 851 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 851 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1251 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1124 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1246 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 304 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 13,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 18,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 65,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 34,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1229 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 39176 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 347 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 825 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 825 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 825 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2871 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2871 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2871 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 23,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 22,3 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 31,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 4,5 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 15,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 2502 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 35710 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 456 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1530 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1530 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1541 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 4936 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 4936 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 400 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 400 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 400 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 24,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 19,3 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 28,6 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 1,7 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 5,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 802 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 6358 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 215 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1524 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1524 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1541 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 4811 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 4811 |

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
