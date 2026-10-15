# Vermessung phase-3-ab-s1s5-A2 (e2e)

Commit `ee401da` (sauber), Branch `HEAD`, 2026-10-15T11:28:56.4880650+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 18.36 21.23 17.38 · CPU-stärkste: 27.1 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 25.5 dotnet; 9.1 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper (Renderer).app/Contents/MacOS/Google Chrome Helper (Renderer); 8.4 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 8.1 /usr/sbin/coreaudiod  
Fremdlast nachher: Last 31.70 27.63 20.85 · CPU-stärkste: 211.4 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 79.9 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 72.5 /System/Library/PrivateFrameworks/iCloudDriveCore.framework/Versions/A/Support/bird; 28.4 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 6.0 /usr/sbin/coreaudiod  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 368 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 1577 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 103 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 751 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 472 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 928 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,5 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 23765 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 23765 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 61,3 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3317 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 229 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 789 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 580 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 883 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,7 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 16939 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 16939 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1084 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 7,2 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 11,3 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 596 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 52,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 26376 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 52354 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 42,2 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 341 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 475 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 3840 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 279 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 928 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 720 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 928 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,8 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 11906 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 11906 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 68,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 67,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 68,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 13,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 79,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 282 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 64,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 730 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 4369 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 285 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 883 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 546 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 883 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 2734 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2734 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 1792 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 130 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 21,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 12302 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 21374 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 48,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 204 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 5,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 54,5 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 718 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7031 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 297 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 751 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 672 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 883 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 14267 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 14267 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 117 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 53,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 75,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 956 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 8,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 8576 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 10331 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 63,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 520 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 4749 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 198 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 826 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 758 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 883 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6675 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6675 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
