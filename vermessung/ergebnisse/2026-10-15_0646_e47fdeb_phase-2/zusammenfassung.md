# Vermessung phase-2 (e2e)

Commit `e47fdeb` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T04:46:20.6857460+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 6.68 5.53 4.73 · CPU-stärkste: 66.6 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 55.7 /Users/tobi/.dotnet/dotnet; 25.4 dotnet; 24.8 /usr/libexec/PerfPowerServices; 17.8 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Fremdlast nachher: Last 23.95 16.09 9.48 · CPU-stärkste: 70.9 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 33.3 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 21.4 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 9.2 /Applications/Claude.app/Contents/Frameworks/Claude Helper.app/Contents/MacOS/Claude Helper; 7.3 /Applications/Claude.app/Contents/Frameworks/Claude Helper (Renderer).app/Contents/MacOS/Claude Helper (Renderer)  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1542 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5308 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 338 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1928 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1282 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2338 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,5 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 16494 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 16494 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 286 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4248 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 282 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1015 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1015 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1203 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9411 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9411 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1647 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 11,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 29,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 596 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 19,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 11110 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 19538 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 30,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 232 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 910 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 6890 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 402 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1295 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1295 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1295 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2045 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2045 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 1244 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 21,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 9,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 5018 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 9087 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 42,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 309 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 910 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6606 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 265 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 73,5 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1193 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1163 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1245 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 11482 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 11482 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1009 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 37,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 90,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1925 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 20,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 12520 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 20196 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 12,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 59,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1149 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 5351 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 389 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2338 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2164 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2338 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 9399 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9740 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 197 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 65,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 102 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1332 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 9,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 5078 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 16173 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 10,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 52,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1013 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 5346 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 416 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2333 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2325 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2333 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 9740 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9740 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1838 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,1 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 82,4 |
| S4 Datensatz einfrieren | Queries (/s) | 4894 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 1980 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2045 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 116 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 115 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 116 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 12,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 49,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 103 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1252 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7361 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 316 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 999 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 891 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 999 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 2483 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2483 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2823 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 128 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 22,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 13942 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 22367 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 21,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 135 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 7,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 63,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 967 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 8139 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 326 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1178 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 781 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1203 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 3867 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6676 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 408 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 179 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 154 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1686 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4957 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12050 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 8,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 62,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1248 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 9582 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 282 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1203 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1199 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1203 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6676 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6676 |

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
