# Vermessung phase-3-ab-s1s5-B2 (e2e)

Commit `0b02802` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T11:25:50.8488810+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 19.01 20.14 16.02 · CPU-stärkste: 26.6 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 18.1 dotnet; 8.4 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 7.9 /usr/sbin/coreaudiod; 6.4 /Applications/Firefox.app/Contents/MacOS/plugin-container.app/Contents/MacOS/plugin-container  
Fremdlast nachher: Last 21.33 21.91 17.55 · CPU-stärkste: 200.1 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 58.2 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 30.0 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 23.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 17.0 /System/Library/Frameworks/CoreServices.framework/Frameworks/Metadata.framework/Versions/A/Support/mdworker_shared  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 0,5 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 3,7 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 659 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 589 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 965 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,6 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 2254 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 233 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4202 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 275 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 854 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 803 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1181 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,7 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 24835 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 24835 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1574 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 10,5 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 22,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 596 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 26,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 15077 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 26506 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 248 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 618 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5491 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 328 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 965 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 734 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 965 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,8 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 114 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 112 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 113 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 19,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 55,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 98,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1181 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7154 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 312 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1075 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1034 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1075 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 4034 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4034 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2340 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 122 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 23,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 14855 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 23313 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 24,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 168 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 61,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 720 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7396 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 355 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1080 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 829 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1181 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19108 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19108 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 417 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 165 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 154 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1815 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5198 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12214 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 60,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1036 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 9203 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 293 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1181 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1098 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1181 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 10217 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10217 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
