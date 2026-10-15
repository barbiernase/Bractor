# Vermessung phase-2-ab-passivierung-aus-1 (e2e)

Commit `639d96f` (1 geänderte Dateien (nicht committet)), Branch `claude/werkbank-zielbild`, 2026-10-15T05:54:47.5327990+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 5.33 10.88 17.08 · CPU-stärkste: 96.1 /usr/libexec/replayd; 23.7 dotnet; 8.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 6.5 /usr/sbin/coreaudiod; 5.8 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper.app/Contents/MacOS/Google Chrome Helper  
Fremdlast nachher: Last 18.41 14.47 17.94 · CPU-stärkste: 237.3 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 92.3 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 63.1 /System/Library/PrivateFrameworks/iCloudDriveCore.framework/Versions/A/Support/bird; 9.9 /System/Library/Frameworks/CoreServices.framework/Frameworks/Metadata.framework/Versions/A/Support/mdworker_shared; 9.1 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 351 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1765 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 117 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 407 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 403 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1401 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,3 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 28211 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 28211 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 160 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 158 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 160 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 34,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 39,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 71,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1680 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 10183 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 385 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1146 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 730 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1146 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 5450 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5450 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2893 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 134 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 21,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 13987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 21320 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 18,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 133 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 64,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 828 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 8514 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 329 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1401 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 849 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1401 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 20354 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 20354 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 489 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 201 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 189 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1765 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 9,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3737 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 9618 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 60,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1275 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 11415 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 323 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1093 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1093 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1146 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 11450 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 11450 |

- L5: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
