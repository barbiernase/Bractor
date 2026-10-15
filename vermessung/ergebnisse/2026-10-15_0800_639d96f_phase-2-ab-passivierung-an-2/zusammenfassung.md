# Vermessung phase-2-ab-passivierung-an-2 (e2e)

Commit `639d96f` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T06:00:52.4672750+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 9.27 12.25 15.96 · CPU-stärkste: 14.3 dotnet; 5.7 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 2.7 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 1.2 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper (Renderer).app/Contents/MacOS/Google Chrome Helper (Renderer); 1.1 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper.app/Contents/MacOS/Google Chrome Helper  
Fremdlast nachher: Last 15.67 14.24 16.44 · CPU-stärkste: 234.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 88.4 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 19.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 10.8 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper (Renderer).app/Contents/MacOS/Google Chrome Helper (Renderer); 9.8 /Applications/Google Chrome.app/Contents/MacOS/Google Chrome  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 372 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1706 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 103 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 420 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 420 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1121 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,4 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27606 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27606 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 143 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 142 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 143 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 17,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 43,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 137 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1489 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 9024 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 411 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 948 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 406 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 948 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 4930 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4930 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2541 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 145 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 19,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 13259 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 19464 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 19,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 145 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 62,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 853 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 9033 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 393 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1121 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 868 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1121 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19313 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19313 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 524 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 217 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 198 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1755 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 9,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3782 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 9313 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 60,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1285 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11744 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 372 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 950 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 950 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 950 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 10930 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10930 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
