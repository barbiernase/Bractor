# Vermessung server-netz-proxy-2 (e2e)

Commit `ab58b0e` (sauber), Branch `HEAD`, 2026-10-15T14:41:10.0785797+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.33 6.54 7.25 1/692 3243954
 · CPU-stärkste: 11.1 dotnet; 1.6 consul; 1.0 redis-server; 0.9 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 11.39 7.97 7.61 3/690 3247533
 · CPU-stärkste: 40.3 postgres;39.1 docker-proxy; 9.5 dotnet; 6.3 postgres; 1.7 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 473 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 33,8 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2896 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 2706 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2896 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 345 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1974 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 150 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1251 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1241 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1507 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27339 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27339 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5769 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 51,5 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 11,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5781 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 11436 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 223 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1458 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 11371 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 750 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1744 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1734 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1744 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 8666 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 47,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 4,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2163 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 4232 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 192 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1322 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 8845 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 424 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 283 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1159 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1159 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1159 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2335 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 94,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 218 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1919 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 8,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3956 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 8217 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 34,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1207 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7244 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 1033 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2838 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2838 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2838 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 441 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 155 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 229 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1300 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3368 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4177 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 33,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1385 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7608 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 930 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2443 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2158 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2443 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1709 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 39,7 |
| S4 Datensatz einfrieren | Queries (/s) | 3880 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 266 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1842 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1842 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1842 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 210 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 208 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 210 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 26,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 35,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 99,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2079 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13157 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 684 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1042 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1042 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1042 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6584 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6584 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 5532 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 196 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 14,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 10198 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 14674 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 31,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 66,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1264 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 12870 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 753 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1425 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1507 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19472 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19472 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 866 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 302 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 313 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1954 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 6,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2844 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 6073 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 55,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2266 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 16900 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 784 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1158 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1147 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1158 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12584 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12584 |

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
