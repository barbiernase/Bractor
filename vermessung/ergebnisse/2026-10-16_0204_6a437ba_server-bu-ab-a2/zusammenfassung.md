# Vermessung server-bu-ab-a2 (e2e)

Commit `6a437ba` (sauber), Branch `HEAD`, 2026-10-16T00:04:14.0053364+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.54 4.43 4.37 1/684 3976457
 · CPU-stärkste: 18.9 dotnet; 1.9 redis-server; 1.8 consul; 1.0 redis-server; 0.4 dotnet  
Fremdlast nachher: Last 1.96 3.30 3.95 2/696 3979382
 · CPU-stärkste: 14.5 dotnet; 6.2 dotnet; 5.9 postgres; 2.8 postgres; 1.9 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1474 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1472 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1474 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 10,5 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 111 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 9,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1173 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1084 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1173 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2979 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 110 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 557 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1926 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 2,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 1502 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 2907 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 2,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 18,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1157 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 9606 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 229 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1470 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1470 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1470 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 817 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 283 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 457 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1307 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 1,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 1317 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 1939 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 3,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 19,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1308 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 8170 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 356 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1432 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1424 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1432 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 300 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 295 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 299 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 50,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 15,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 39,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2168 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10514 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 422 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 965 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 912 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 965 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3151 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 886 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1217 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2157 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 889 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1566 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 265 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 28,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1683 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 33004 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 662 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1171 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1171 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1171 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1122 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 909 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1014 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 570 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 187 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 365 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 30,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1980 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 27702 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 689 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1054 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1054 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1054 |
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
