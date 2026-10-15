# Vermessung phase-2-ab-passivierung-an-1 (e2e)

Commit `639d96f` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T05:51:54.4788590+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 4.44 11.64 18.49 · CPU-stärkste: 145.0 /Users/tobi/.dotnet/dotnet; 12.1 dotnet; 10.9 /System/Library/ExtensionKit/Extensions/Storage.appex/Contents/MacOS/Storage; 7.6 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 6.4 /usr/sbin/coreaudiod  
Fremdlast nachher: Last 15.56 14.03 18.85 · CPU-stärkste: 287.1 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine;112.7 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 70.7 /System/Library/PrivateFrameworks/iCloudDriveCore.framework/Versions/A/Support/bird; 7.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 6.4 /usr/sbin/filecoordinationd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 589 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2354 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 139 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 449 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 431 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1370 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,3 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 31203 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 31203 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 160 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 159 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 160 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 14,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 39,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 83,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1670 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10121 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 388 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1082 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 539 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1082 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 5518 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5518 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3199 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 157 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 18,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 11663 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 18145 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 12,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 114 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 64,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 924 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 9960 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 432 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1370 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 997 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1370 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19562 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19562 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 541 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 234 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 208 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1705 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3698 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 8048 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 59,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1459 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 12204 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 381 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 566 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 566 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1082 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 11518 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 11518 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
