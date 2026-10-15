# Vermessung phase-2-ab-passivierung-aus-2 (e2e)

Commit `639d96f` (1 geänderte Dateien (nicht committet)), Branch `claude/werkbank-zielbild`, 2026-10-15T05:57:45.1866270+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 5.79 11.05 16.21 · CPU-stärkste: 33.1 dotnet; 26.9 /System/Library/PrivateFrameworks/iCloudDriveCore.framework/Versions/A/Support/bird; 24.1 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper (Renderer).app/Contents/MacOS/Google Chrome Helper (Renderer); 11.9 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper.app/Contents/MacOS/Google Chrome Helper; 10.3 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Fremdlast nachher: Last 21.00 15.48 17.51 · CPU-stärkste: 250.7 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 80.3 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 21.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 17.3 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper (Renderer).app/Contents/MacOS/Google Chrome Helper (Renderer); 9.1 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper.app/Contents/MacOS/Google Chrome Helper  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 275 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4012 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 163 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1096 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1096 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1096 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 26687 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 26687 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 140 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 139 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 140 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 10,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 42,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 87,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1461 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 8833 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 318 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1086 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 501 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1086 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 4913 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4913 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2951 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 156 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 18,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 11402 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 18169 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 18,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 118 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 60,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 938 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 9401 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 353 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1067 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 759 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1086 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19765 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19765 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 382 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 183 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 161 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1562 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 10,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4017 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 10802 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 62,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1117 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 9913 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 279 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1060 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1060 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1086 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 11115 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 11115 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
