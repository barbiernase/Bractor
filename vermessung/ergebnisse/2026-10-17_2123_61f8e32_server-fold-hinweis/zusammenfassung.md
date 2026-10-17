# Vermessung server-fold-hinweis (e2e)

Commit `61f8e32` (sauber), Branch `HEAD`, 2026-10-17T19:23:50.9388082+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.49 1.75 1.41 2/717 2318932
 · CPU-stärkste: 403 dotnet; 2.0 consul; 1.4 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 1.04 1.43 1.37 1/694 2325226
 · CPU-stärkste: 23.4 dotnet; 3.9 dotnet; 2.6 postgres; 2.0 consul; 1.5 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 42,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 716 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1469 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1469 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1588 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10283 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 15,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1750 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,8 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1442 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1441 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1442 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 9748 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6289 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10606 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 26,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 174 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1486 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 9682 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 635 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1156 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1141 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1156 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 6278 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 46,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2930 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4167 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 26,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 148 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1224 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6795 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 260 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 273 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1051 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1023 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1051 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2500 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 320 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1029 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1744 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 1,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 795 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 1156 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 965 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 15847 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 357 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1522 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1522 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1522 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10283 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 773 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 687 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 771 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 221 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 18,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 72,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1168 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12171 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 454 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1496 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1496 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1511 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6282 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6282 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1935 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 49,7 |
| S4 Datensatz einfrieren | Queries (/s) | 5572 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 296 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 293 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 296 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 32,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 16,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 38,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2118 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10327 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 472 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1112 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1103 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1112 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4345 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 762 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1188 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2474 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1051 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1749 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 10,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 697 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 786 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 27644 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 728 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1437 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1437 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1437 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1140 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 918 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1014 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 583 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 231 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 388 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1793 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 26826 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 631 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1221 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1221 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1221 |
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
