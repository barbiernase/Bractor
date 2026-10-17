# Vermessung server-sonde-sync-aus (e2e)

Commit `03b25d0` (sauber), Branch `HEAD`, 2026-10-17T18:10:10.3602158+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.47 1.34 1.21 1/656 2227247
 · CPU-stärkste: 2.8 python3; 2.0 consul; 1.4 redis-server; 1.2 redis-server; 1.0 postgres  
Fremdlast nachher: Last 1.41 1.52 1.38 3/684 2241565
 · CPU-stärkste: 4.2 postgres; 3.5 python3; 3.4 dotnet; 2.6 postgres; 2.0 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 26,1 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 465 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1,4 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 938 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 938 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 938 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 8431 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 18,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1734 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1290 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1250 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1312 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2889 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 371 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1400 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1743 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 642 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 766 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1308 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 20261 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 574 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 908 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 908 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 908 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 8431 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 8431 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 962 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 803 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 962 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 331 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 192 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1785 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 14933 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 617 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 845 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 845 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 845 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 4430 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4430 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 450 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 441 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 449 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 65,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 28,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 3449 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 15642 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 638 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1083 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 995 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1083 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4158 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 807 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 931 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2418 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 1,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 1137 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 1819 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 756 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 1,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 25,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1205 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 22739 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 797 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1287 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1287 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1287 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1411 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 829 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 970 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1237 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 644 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 1185 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 29,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2152 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 26031 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 841 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1170 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1170 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1170 |
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
