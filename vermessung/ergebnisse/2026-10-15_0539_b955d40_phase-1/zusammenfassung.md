# Vermessung phase-1 (e2e)

Commit `b955d40` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T03:39:17.0624340+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 8.02 5.09 4.91 · CPU-stärkste: 66.8 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 55.8 /Users/tobi/.dotnet/dotnet; 27.9 dotnet; 16.1 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 6.8 /usr/sbin/coreaudiod  
Fremdlast nachher: Last 24.55 15.48 9.43 · CPU-stärkste: 245.4 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 77.5 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 66.9 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 18.6 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 6.4 /Applications/Claude.app/Contents/Frameworks/Claude Helper.app/Contents/MacOS/Claude Helper  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1600 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5472 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 324 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2120 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 305 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3588 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 158 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 775 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1583 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 13,2 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 31,2 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 595 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 18,8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 11296 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 18725 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 30,2 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 228 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 937 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 7074 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 370 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1287 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 1564 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 23,3 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 8,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 4301 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 8464 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 43,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 306 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1002 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 7046 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 289 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 79,2 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1776 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1137 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 40,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 98,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1929 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 18,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 11269 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 18819 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 11,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 59,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1144 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 5740 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 416 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2549 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 195 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 65,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 105 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1326 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 8,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 5148 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 14858 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 8,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 50,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 854 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 5227 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 441 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2636 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1826 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 41,1 |
| S4 Datensatz einfrieren | Queries (/s) | 4002 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 216 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1307 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 149 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 147 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 149 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 27,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 42,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 74,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1556 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 9449 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 368 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1329 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2956 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 144 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 19,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 12782 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 19822 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 17,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 127 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 62,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 860 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 8854 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 343 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1112 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 423 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 183 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 171 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1699 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 10,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4700 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 10458 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1181 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 10307 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 304 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1116 |

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
