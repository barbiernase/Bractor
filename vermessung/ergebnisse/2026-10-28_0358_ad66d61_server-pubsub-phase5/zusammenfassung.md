# Vermessung server-pubsub-phase5 (e2e)

Commit `ad66d61` (sauber), Branch `HEAD`, 2026-10-28T02:58:30.1397635+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.06 2.87 2.21 2/721 3892587
 · CPU-stärkste: 376 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.30 1.08 1.54 2/641 3904542
 · CPU-stärkste: 2.6 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.6 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1491 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1263 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1263 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1405 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 5068 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 5068 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 0,0 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 2,3 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,1 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 907 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 907 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 988 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9528 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9528 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 9241 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 162 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2555 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3640 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 89,8 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1525 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 14238 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 841 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 861 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 861 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 861 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 1043 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1043 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 7103 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 56,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2562 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3487 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 11,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 106 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 643 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 5988 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 210 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 258 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 802 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 625 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 802 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 645 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 645 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 5083 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 508 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1392 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1800 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 696 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 1036 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 720 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 12824 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 460 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1332 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1332 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1405 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 5068 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5068 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1346 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1193 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1316 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 227 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 27,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 111 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1728 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13460 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 315 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1397 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1397 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1397 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 3067 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3067 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2823 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 53,1 |
| S4 Datensatz einfrieren | Queries (/s) | 8402 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 1055 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 1055 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 397 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 385 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 395 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 92,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 8,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 26,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 127 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 1101 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 28,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3528 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 7403 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 101 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 306 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2959 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 9,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5229 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 9425 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 253 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 36,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1095 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 11085 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 399 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 988 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 900 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 988 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9528 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1962 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 328 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 311 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2499 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4166 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 7987 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1166 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11980 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 362 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 913 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 874 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 913 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6528 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6528 |

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
