# Vermessung server-bu-ab-b1 (e2e)

Commit `2390f1e` (sauber), Branch `HEAD`, 2026-10-15T23:59:28.5237883+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.00 7.43 5.11 1/686 3968388
 · CPU-stärkste: 50.4 dotnet; 1.9 redis-server; 1.8 consul; 1.0 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 2.65 5.53 4.69 2/701 3971232
 · CPU-stärkste: 28.2 dotnet; 6.5 dotnet; 5.7 postgres; 2.6 postgres; 1.9 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1851 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1850 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1851 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 11,7 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 114 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 9,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1249 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1247 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1249 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2701 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 402 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1215 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1702 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 723 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 918 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1351 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 19066 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 584 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1792 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1792 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1792 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 784 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 737 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 783 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 120 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 15,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 128 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1252 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12836 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 530 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1737 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1737 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1737 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 297 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 289 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 297 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 81,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 15,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 39,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2125 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10349 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 485 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1009 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 972 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1009 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3015 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 912 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1185 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2092 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 897 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1566 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 5,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 104 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 28,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1736 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 29449 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 745 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1231 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1231 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1231 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 916 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1038 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 759 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 350 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 558 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 30,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1986 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 29465 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 646 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1112 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1112 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1112 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
