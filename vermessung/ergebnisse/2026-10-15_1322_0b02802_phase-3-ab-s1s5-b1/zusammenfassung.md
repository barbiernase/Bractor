# Vermessung phase-3-ab-s1s5-B1 (e2e)

Commit `0b02802` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T11:22:48.8807230+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 19.77 19.79 14.94 · CPU-stärkste: 41.4 dotnet; 28.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 22.6 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 8.8 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 8.5 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper (Renderer).app/Contents/MacOS/Google Chrome Helper (Renderer)  
Fremdlast nachher: Last 22.41 20.81 16.18 · CPU-stärkste: 266.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 53.3 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 21.5 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 16.1 dotnet; 10.6 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,1 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 48,6 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 18,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 652 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 570 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 885 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,6 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2254 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 220 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4182 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 249 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 984 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 932 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1176 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 24700 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 24700 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1954 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 3,3 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 23,0 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 25,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 14993 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 25616 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,2 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 252 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 646 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5783 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 348 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 885 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 702 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 885 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,8 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 113 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 112 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 112 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 13,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 56,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 109 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1176 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7106 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 386 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1176 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 837 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1176 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 4012 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4012 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2461 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 120 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 23,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 15380 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 23679 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 29,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 177 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 60,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 710 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7267 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 352 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 970 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 897 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1176 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19021 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19021 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 392 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 162 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 146 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1757 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5654 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12788 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 60,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 953 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8780 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 335 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1075 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1031 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1176 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 10224 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10224 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
