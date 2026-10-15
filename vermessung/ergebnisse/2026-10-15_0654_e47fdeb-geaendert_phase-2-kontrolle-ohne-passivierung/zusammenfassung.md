# Vermessung phase-2-kontrolle-ohne-passivierung (e2e)

Commit `e47fdeb` (3 geänderte Dateien (nicht committet)), Branch `claude/werkbank-zielbild`, 2026-10-15T04:54:49.1606950+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 19.88 22.21 14.49 · CPU-stärkste: 64.4 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 27.6 dotnet; 26.1 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 10.0 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 7.1 /Applications/Claude.app/Contents/Frameworks/Claude Helper (Renderer).app/Contents/MacOS/Claude Helper (Renderer)  
Fremdlast nachher: Last 42.85 32.93 23.06 · CPU-stärkste: 448.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 68.6 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 36.6 dotnet; 21.9 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 9.0 /usr/sbin/coreaudiod  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 566 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 1698 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 156 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1322 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 741 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2224 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,3 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 52942 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 52942 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 90,6 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 1018 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 177 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 709 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 533 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1175 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,5 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 15416 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 15416 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1742 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 2,9 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 24,1 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 24,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 14316 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 24332 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 30,5 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 242 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 733 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5815 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 367 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1156 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1037 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1156 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 4823 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 4823 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 660 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 4,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 40,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 11940 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 40472 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 342 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 137 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 1662 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 168 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 35,8 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1274 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 424 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1274 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 13021 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 13021 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1003 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 41,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 14,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1917 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 119 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 32,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 70428 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 9,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 66,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 162 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 1095 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 156 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2092 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1174 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2224 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 32340 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 32340 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 177 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 56,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 73,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1356 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 15,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11145 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 23720 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 11,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 62,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 807 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 4540 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 350 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2224 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1665 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2224 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 18582 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18582 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1834 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 47,9 |
| S4 Datensatz einfrieren | Queries (/s) | 4664 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 129 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1110 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1110 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1156 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1,0 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 4837 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 4837 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 24,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 23,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 24,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 9,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 299 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 952 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 59,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 235 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 1396 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 167 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 636 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 636 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1175 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 1168 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 1168 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 3344 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 29,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 101 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 55648 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 100620 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 86,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 443 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 7,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 61,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 222 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 1816 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 156 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 875 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 704 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1175 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 13574 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 13574 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 118 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 32,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 56,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1293 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 16,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 17057 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 20184 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 8,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 467 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 3396 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 226 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 787 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 748 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1175 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 5794 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5794 |

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
