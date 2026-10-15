# Vermessung server-pf-4-ab-b2 (e2e)

Commit `61e28bd` (sauber), Branch `HEAD`, 2026-10-15T15:43:11.6760507+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.14 6.69 6.85 2/675 3329520
 · CPU-stärkste: 29.9 dotnet; 1.7 consul; 1.4 redis-server; 1.0 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 7.70 7.19 7.02 2/689 3333044
 · CPU-stärkste: 24.2 dotnet;10.4 dotnet; 7.9 postgres; 2.8 postgres; 1.6 consul  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 2,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,1 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2719 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2654 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2719 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 395 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1947 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 132 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1129 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1129 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1377 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27878 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27878 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5551 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 12,6 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 6346 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 12516 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 225 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1342 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10611 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 837 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1952 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1937 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1952 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 5297 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 46,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2245 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4210 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 193 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1289 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 8662 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 420 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 303 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1032 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1032 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1032 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2249 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 87,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 221 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1922 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3744 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8168 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 33,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1234 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7313 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 989 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2717 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2717 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2717 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 316 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 107 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 184 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1321 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 4751 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 5109 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1160 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 6099 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 1262 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2595 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2524 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2595 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1343 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 31,3 |
| S4 Datensatz einfrieren | Queries (/s) | 3059 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 221 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 219 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 220 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 20,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 37,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 92,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2167 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13692 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 794 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 983 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 949 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 983 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6569 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6569 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6309 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 219 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 13,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9178 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13214 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 60,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 56,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1272 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12189 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 730 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1377 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1309 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1377 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19083 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19083 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 855 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 258 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 313 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2094 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 6,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3325 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 6015 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 54,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2279 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 16841 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 934 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1058 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1048 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1058 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12569 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12569 |

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
