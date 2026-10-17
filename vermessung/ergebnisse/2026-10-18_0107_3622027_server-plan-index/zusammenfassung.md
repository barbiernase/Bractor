# Vermessung server-plan-index (e2e)

Commit `3622027` (sauber), Branch `HEAD`, 2026-10-17T23:07:51.8277965+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.13 1.74 1.08 1/722 2588894
 · CPU-stärkste: 73.9 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.4 dotnet  
Fremdlast nachher: Last 1.91 2.05 1.37 2/696 2594939
 · CPU-stärkste: 30.6 dotnet; 3.8 dotnet; 2.6 postgres; 2.0 consul; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 44,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 719 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,8 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1952 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1952 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1952 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10283 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 20,1 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1780 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,9 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1221 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1117 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1221 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 8343 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 62,1 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5810 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9496 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 172 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1400 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10610 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 641 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1318 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1318 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1318 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 5674 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 46,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 3106 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4255 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 23,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 150 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1057 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6875 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 227 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 285 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1150 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 993 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1150 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4046 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 322 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1423 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1841 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 559 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 915 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1474 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 22195 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 333 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1939 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1939 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1939 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10283 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10283 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1243 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1063 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1240 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 290 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 61,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1752 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 20337 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 251 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1875 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1875 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1875 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6282 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6282 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2700 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 59,0 |
| S4 Datensatz einfrieren | Queries (/s) | 8022 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2270 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2270 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 411 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 395 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 411 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 121 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 10,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 26,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2776 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13974 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 389 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 994 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 969 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 994 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3218 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1854 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2813 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 128 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 429 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 80,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 27,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1654 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 55628 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 579 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1213 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1213 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1213 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1408 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1166 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1397 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 516 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 26,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 63,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 32,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2593 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 39211 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 501 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1020 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1020 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1020 |
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
