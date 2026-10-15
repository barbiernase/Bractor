# Vermessung phase-3 (e2e)

Commit `0b02802` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T11:12:52.2649600+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 10.37 8.27 7.44 · CPU-stärkste: 61.5 /Users/tobi/.dotnet/dotnet; 59.3 /usr/libexec/triald; 39.1 /usr/libexec/trustd; 17.2 /usr/libexec/mobileassetd; 16.9 dotnet  
Fremdlast nachher: Last 26.55 20.88 13.70 · CPU-stärkste: 330.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 52.9 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 33.3 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 23.4 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 20.3 dotnet  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 602 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 33,3 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 617 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 152 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2363 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,1 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 93,1 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 955 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 160 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 591 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 434 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 959 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,5 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 19745 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 19745 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1360 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 13,6 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 26,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 594 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 22,4 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 11223 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 22243 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 27,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 239 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 728 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 6244 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 278 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1061 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 585 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1061 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,6 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 1749 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 20,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 9,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 5245 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 9498 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 209 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 583 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 4286 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 259 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 105 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1459 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1113 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1459 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 951 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 39,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 64,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1916 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 29,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 14648 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 28669 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 39,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 363 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 2509 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 317 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2363 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1467 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2363 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 164 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 63,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 83,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1225 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 11,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 10401 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 18085 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 43,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 517 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 3632 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 332 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1870 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1781 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1870 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1397 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,3 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 37,1 |
| S4 Datensatz einfrieren | Queries (/s) | 3629 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 195 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 823 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 823 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1061 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 0,8 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 85,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 84,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 85,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 20,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 70,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 183 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 887 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 5355 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 320 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 959 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 765 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 959 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3218 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3218 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2125 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 30,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 97,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 46274 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 96761 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 42,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 451 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 66,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 203 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 2015 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 162 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 919 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 512 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 959 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18782 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18782 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 248 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 103 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 111 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1751 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 15,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 9227 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 14924 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 731 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 6735 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 283 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 934 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 363 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 959 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 9885 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9885 |

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
