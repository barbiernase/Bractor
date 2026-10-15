# Vermessung server-pf-4 (e2e)

Commit `a0caff6` (sauber), Branch `HEAD`, 2026-10-15T15:21:08.5433753+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.12 1.00 1.73 1/704 3294432
 · CPU-stärkste: 359 dotnet; 1.7 consul; 1.2 redis-server; 1.1 redis-server; 0.4 systemd  
Fremdlast nachher: Last 8.58 4.54 2.98 2/679 3297955
 · CPU-stärkste: 44.5 dotnet;10.2 dotnet; 7.5 postgres; 3.1 postgres; 2.1 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2418 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2417 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2491 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 409 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1973 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 134 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1309 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1309 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1539 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 28573 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 28573 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4728 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 45,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 13,0 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6704 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 12893 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 31,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 230 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1436 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10459 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 847 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1858 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1837 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1858 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 2820 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2181 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4181 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 31,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 195 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1472 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9077 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 405 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 331 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1141 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1141 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1141 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2374 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 90,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 224 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1924 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3756 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7956 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 33,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1265 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7422 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1043 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2491 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2491 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2491 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 342 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 122 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 200 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1286 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 4105 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4387 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1228 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 6489 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 1118 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2472 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2403 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2472 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1766 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 42,0 |
| S4 Datensatz einfrieren | Queries (/s) | 4113 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 661 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1901 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1901 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1901 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 221 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 217 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 220 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 51,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 37,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 81,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2197 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13738 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 782 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1102 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1102 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1102 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6585 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6585 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6208 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 221 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 13,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 8866 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13027 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 55,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 56,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1322 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12212 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 736 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1539 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1477 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1539 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19241 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19241 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 829 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 267 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 310 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2034 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 6,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3212 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5979 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2307 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 16660 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 932 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1212 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1206 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1212 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12585 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12585 |

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
