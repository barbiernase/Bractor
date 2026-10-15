# Vermessung baseline (e2e)

Commit `3cfec32` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T02:29:07.5477620+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 11.20 16.68 19.64 · CPU-stärkste: 120.3 /Users/tobi/.dotnet/dotnet; 63.3 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 26.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 25.4 dotnet; 22.3 /Applications/Google Chrome.app/Contents/MacOS/Google Chrome  
Fremdlast nachher: Last 22.95 22.26 21.59 · CPU-stärkste: 240.3 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 83.0 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 67.5 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 32.7 /System/Library/ExtensionKit/Extensions/Storage.appex/Contents/MacOS/Storage; 5.5 /usr/sbin/coreaudiod  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 258 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 777 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 87,2 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1743 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 162 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3739 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 203 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 976 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1474 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 4,9 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 19,9 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 598 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 29,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 17286 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 29476 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 29,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 258 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 591 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5104 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 360 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1061 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 2424 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 16,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 12,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 6154 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 12279 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 55,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 314 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 879 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 5007 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 281 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 69,8 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1341 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 505 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 21,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 50,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1916 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 35,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 22828 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 35756 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 67,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 544 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 3365 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 369 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2725 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 119 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 39,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 54,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1207 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 17,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 2,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11111 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 21101 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 10,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 57,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 563 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 3097 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 307 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1843 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1091 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,4 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 6,4 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 28,5 |
| S4 Datensatz einfrieren | Queries (/s) | 5564 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 258 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 774 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 108 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 107 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 108 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 21,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 57,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 105 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 66,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1136 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7099 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 365 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2181 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 126 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 22,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 14771 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 22324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 27,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 190 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 59,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 766 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7380 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 350 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1063 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 333 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 163 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 138 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1535 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5179 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12607 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 931 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8440 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 325 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1064 |

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
