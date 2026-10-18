# Vermessung server-wachstum (e2e)

Commit `7127fa0` (sauber), Branch `HEAD`, 2026-10-18T01:31:54.0053911+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.05 1.68 1.31 1/714 2744771
 · CPU-stärkste: 399 dotnet; 2.0 consul; 1.2 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.66 1.08 1.15 2/683 2750862
 · CPU-stärkste: 23.0 dotnet; 3.8 dotnet; 3.1 postgres; 2.0 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 44,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 718 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,5 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,6 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1809 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1809 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1825 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10283 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 21,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1785 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,5 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 985 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 985 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1003 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8949 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 61,3 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5852 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9619 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 172 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1389 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10537 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 609 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1234 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1234 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1234 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 7178 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2988 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4215 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 23,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 149 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6831 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 210 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 300 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1026 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1026 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1058 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3624 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 538 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1277 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1703 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 582 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 1015 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1252 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 18642 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 262 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1776 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1776 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1825 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10283 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1165 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1078 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1161 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 150 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 74,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1602 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 18107 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 304 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1825 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1825 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1825 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6282 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6282 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2718 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 59,7 |
| S4 Datensatz einfrieren | Queries (/s) | 8127 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 408 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 395 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 407 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 94,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 28,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2815 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 14158 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 387 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1003 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 784 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1003 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3222 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1845 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2844 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1282 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 145 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 347 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 92,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1641 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 59377 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 454 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 962 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 962 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1003 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1319 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1448 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 286 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 24,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 58,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 31,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2800 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 41920 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 485 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 866 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 866 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1003 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
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
