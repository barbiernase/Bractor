# Vermessung server-netz-proxy-1 (e2e)

Commit `ab58b0e` (sauber), Branch `HEAD`, 2026-10-15T14:31:55.3813866+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.17 2.55 7.29 2/740 3227812
 · CPU-stärkste: 324 dotnet; 1.6 consul; 0.7 redis-server; 0.5 redis-server; 0.3 postgres  
Fremdlast nachher: Last 9.88 5.39 7.52 2/739 3231321
 · CPU-stärkste: 36.5 docker-proxy;32.7 dotnet;18.1 postgres; 8.8 dotnet; 6.3 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 587 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 27,8 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2696 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2695 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2705 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 343 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1815 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 141 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1246 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1190 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1440 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27424 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27424 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5780 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 54,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 11,0 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5638 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10856 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 221 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1541 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11892 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 741 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1799 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1794 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1799 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 4375 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2203 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4169 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 192 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1352 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9022 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 421 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 288 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1047 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1047 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1047 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2444 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 83,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 208 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1932 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3874 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8779 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 33,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1156 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 6909 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 899 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2705 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2705 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2705 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 428 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 151 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 227 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1294 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3551 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4114 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 34,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1347 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7463 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 953 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2501 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2378 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2501 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1767 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 42,8 |
| S4 Datensatz einfrieren | Queries (/s) | 4180 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 204 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1906 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1906 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1906 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 210 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 207 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 210 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 53,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 40,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 79,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2083 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13087 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 764 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1099 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1089 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1099 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6596 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6596 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5420 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 191 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 15,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 10778 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 15082 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 35,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 67,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1152 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12691 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 725 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1440 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1427 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1440 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19654 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19654 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 840 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 301 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 310 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1924 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 6,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2966 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 6042 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2268 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 16836 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 788 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1220 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1200 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1220 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12596 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12596 |

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
