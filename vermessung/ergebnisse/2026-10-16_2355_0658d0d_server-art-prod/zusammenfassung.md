# Vermessung server-art-prod (e2e)

Commit `0658d0d` (sauber), Branch `HEAD`, 2026-10-16T21:55:40.5931780+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 0.58 0.22 0.13 1/719 1076404
 · CPU-stärkste: 397 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.4 systemd  
Fremdlast nachher: Last 1.24 1.56 0.79 3/693 1082765
 · CPU-stärkste: 23.1 dotnet; 3.9 dotnet; 2.6 postgres; 2.0 consul; 1.5 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 47,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 726 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,6 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1654 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1654 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1893 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 15,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1723 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,5 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1227 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1163 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1227 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 6317 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 10,5 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 91,5 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 6,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3305 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 6461 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 24,9 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 148 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 2254 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13365 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 1003 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1323 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1276 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1323 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 3603 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 91,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 2,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 1221 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 2144 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 25,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 129 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 2222 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 11367 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 375 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 450 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1080 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1022 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1080 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2679 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 226 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1178 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1831 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 755 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 960 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1035 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 17088 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 412 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1683 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1683 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1893 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 789 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 731 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 787 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 146 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 17,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 72,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1257 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12922 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 432 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1893 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1625 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1893 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 962 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,3 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 22,1 |
| S4 Datensatz einfrieren | Queries (/s) | 2575 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 246 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1576 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1576 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1576 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 292 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 286 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 291 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 61,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 16,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 40,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2125 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10297 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 475 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 887 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 882 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 990 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3911 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 518 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1115 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2603 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1149 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1895 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 827 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 25,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 760 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 26848 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 691 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1147 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1147 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1147 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1181 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 927 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1027 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 647 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 211 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 420 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 30,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1959 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 28583 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 659 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 995 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 995 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 995 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

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
