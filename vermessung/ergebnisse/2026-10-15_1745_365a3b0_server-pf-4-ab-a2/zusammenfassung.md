# Vermessung server-pf-4-ab-a2 (e2e)

Commit `365a3b0` (sauber), Branch `HEAD`, 2026-10-15T15:45:46.4050912+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.41 7.14 7.01 1/676 3334250
 · CPU-stärkste: 23.9 dotnet; 1.6 consul; 1.5 redis-server; 1.0 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 9.21 7.77 7.23 3/708 3338125
 · CPU-stärkste: 19.6 dotnet; 9.0 dotnet; 7.7 postgres; 3.4 postgres; 1.7 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 375 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 17,7 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2325 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2324 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2776 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 408 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2192 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 148 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1346 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1346 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1606 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 28544 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 28544 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 3907 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 13,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,6 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 598 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5330 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10536 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 219 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1568 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12077 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 803 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1809 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1783 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1809 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4824 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 51,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2010 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3880 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 191 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1446 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9651 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 436 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 316 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1072 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1072 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1072 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2382 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 86,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 226 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1927 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3692 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8021 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1269 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7381 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 946 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2502 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2492 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2776 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 426 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 149 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 232 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1299 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 3,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3489 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 3915 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 34,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1395 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7720 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 948 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2776 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2406 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2776 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1737 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 41,8 |
| S4 Datensatz einfrieren | Queries (/s) | 4087 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 331 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1891 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1891 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1891 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 222 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 219 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 221 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 32,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 38,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 78,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2198 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13874 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 798 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1142 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1142 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1142 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6574 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6574 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5998 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 201 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 14,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9871 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 14306 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 6,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 36,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 65,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1297 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13055 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 749 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1606 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1606 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19454 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19454 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 864 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 275 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 318 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2046 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2981 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 5883 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2332 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 17181 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 880 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1252 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1243 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1252 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12574 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12574 |

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
