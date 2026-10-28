# Vermessung server-pubsub-phase6 (e2e)

Commit `4f43172` (sauber), Branch `HEAD`, 2026-10-28T04:36:48.9644621+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.67 1.46 1.60 2/719 4014649
 · CPU-stärkste: 360 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.3 dotnet  
Fremdlast nachher: Last 0.77 0.96 1.22 1/682 4024640
 · CPU-stärkste: 12.6 dotnet; 3.0 postgres; 2.8 dotnet; 2.1 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1469 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 2,9 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1242 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1242 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1242 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4992 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4992 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 26,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 320 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 5,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,1 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 797 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 797 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 987 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9458 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9458 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8192 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 158 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2745 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3726 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 90,5 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1494 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 13886 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 855 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1103 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1006 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1103 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 990 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 990 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4593 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 740 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1668 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1678 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 513 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 752 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 844 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 15871 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 246 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1242 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1242 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1242 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4992 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4992 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1361 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1188 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1342 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 254 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 54,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 12,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 24,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 200 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 9,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1241 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1240 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1241 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2991 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2991 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 448 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 429 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 448 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 131 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 16,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2305 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19719 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 362 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 853 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 851 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 853 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3458 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8015 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 80,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 224 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2970 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 13,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5614 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13031 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 41,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 37,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 836 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 8310 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 272 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 858 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9458 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2014 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 351 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 243 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2477 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 10,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4066 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 10860 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 915 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 9464 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 237 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 903 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 849 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 903 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6458 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
