# Vermessung phase-2-kontrolle-standardpassivierung (e2e)

Commit `e47fdeb` (3 geänderte Dateien (nicht committet)), Branch `claude/werkbank-zielbild`, 2026-10-15T04:50:42.0033530+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 10.13 13.54 9.03 · CPU-stärkste: 67.4 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 38.5 dotnet; 16.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 9.0 /System/Library/ExtensionKit/Extensions/Storage.appex/Contents/MacOS/Storage; 7.6 /usr/sbin/coreaudiod  
Fremdlast nachher: Last 27.31 23.65 14.54 · CPU-stärkste: 228.9 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 73.6 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 65.9 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 28.0 /System/Library/PrivateFrameworks/CacheDelete.framework/deleted_helper; 18.5 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1466 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5174 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 240 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1942 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1083 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2453 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,4 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 63573 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 63573 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 222 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4177 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 253 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1059 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1059 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1473 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,7 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 24373 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 24373 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1926 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 3,2 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 29,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 20,4 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 11324 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 20303 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 30,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 231 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 883 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 6684 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 340 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1193 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 808 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1193 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,7 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 4905 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 4905 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 1963 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 20,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 9,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 4981 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 9453 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 40,2 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 317 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 828 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6528 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 283 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 75,4 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1577 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1360 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1577 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 12347 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 12347 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1024 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 49,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 95,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1904 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 19,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 10511 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 19672 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 11,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 55,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1107 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 5277 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 353 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2453 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2066 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2453 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 31550 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 31550 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 170 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 65,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 84,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1226 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 12,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 5272 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 17567 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 9,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 55,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 792 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 4635 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 322 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2050 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1176 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2050 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 15451 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 15451 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1813 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 40,8 |
| S4 Datensatz einfrieren | Queries (/s) | 3971 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 224 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 820 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 820 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1193 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 0,7 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 4919 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 4919 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 107 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 106 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 107 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 17,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 56,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 102 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1119 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 6745 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 304 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1473 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 949 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1473 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3910 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3910 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2089 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 121 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 23,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 14661 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 23240 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 26,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 151 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 59,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 709 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7194 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 338 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1039 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 801 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1473 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18791 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18791 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 351 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 161 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 136 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1625 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 13,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5221 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 13451 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 925 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8260 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 297 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1229 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1105 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1473 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 10231 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10231 |

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
