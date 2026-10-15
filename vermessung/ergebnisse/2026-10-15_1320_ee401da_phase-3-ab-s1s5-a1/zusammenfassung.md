# Vermessung phase-3-ab-s1s5-A1 (e2e)

Commit `ee401da` (sauber), Branch `HEAD`, 2026-10-15T11:20:00.0765100+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 8.16 16.15 12.66 · CPU-stärkste: 30.9 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 23.7 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 22.0 /Users/tobi/.dotnet/dotnet; 10.9 /System/Library/CoreServices/Spotlight.app/Contents/MacOS/Spotlight; 10.5 dotnet  
Fremdlast nachher: Last 22.26 20.24 15.01 · CPU-stärkste: 290.0 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine;111.5 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 24.9 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 20.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 15.5 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper.app/Contents/MacOS/Google Chrome Helper  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1550 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5681 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 310 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1220 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1190 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1220 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 26942 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 26942 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 162 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3897 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 293 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 856 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 527 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1135 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,5 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 23197 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 23197 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1422 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 2,4 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 28,6 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 20,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 11412 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 20441 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,3 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 225 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 806 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 6419 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 366 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1008 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 986 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1008 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 3681 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 3681 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 111 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 110 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 111 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 57,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 113 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1163 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7008 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 361 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1078 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 735 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1078 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 4014 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4014 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2021 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 97,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 29,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 18713 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 29389 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 38,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 259 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 61,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 572 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 5864 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 352 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 864 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 847 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1135 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19066 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19066 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 381 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 147 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 125 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1839 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 16,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5945 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 15978 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 60,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 806 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 7465 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 306 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1135 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 818 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1135 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 10199 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10199 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
