# Vermessung baseline (e2e)

Commit `9d93552` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T02:22:35.9757700+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 8.64 15.00 20.13 · CPU-stärkste: 97.3 /Users/tobi/Library/Application Support/Steam/Steam.AppBundle/Steam/Contents/MacOS/steam_osx; 68.3 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 47.8 /Users/tobi/.dotnet/dotnet; 41.5 dotnet; 19.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Fremdlast nachher: Last 22.19 20.00 21.03 · CPU-stärkste: 234.1 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 66.1 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 51.3 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 25.7 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 19.4 /Applications/Google Chrome.app/Contents/MacOS/Google Chrome  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 6766 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 249 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1819 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 241 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3969 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 253 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 991 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1478 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 2,5 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 28,8 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 20,5 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 11360 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 20356 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 30,2 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 236 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 865 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 6774 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 417 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1163 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 1660 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 22,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 8,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 4771 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 8624 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 327 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 632 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 7276 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 317 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 71,4 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1401 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 474 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 20,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 24,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1915 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 79,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 35733 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 79217 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 27,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 132 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 445 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 2115 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 171 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2641 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 187 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 68,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 80,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1272 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 14,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 10031 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 16684 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 8,8 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 53,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 705 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 4275 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 372 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1947 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1843 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 49,8 |
| S4 Datensatz einfrieren | Queries (/s) | 4849 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 138 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 137 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 137 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 14,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 49,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 79,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 66,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1438 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 9024 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 360 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1297 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2308 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 118 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 24,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 15064 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 23979 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 22,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 170 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 61,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 708 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7160 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 357 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1123 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 448 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 190 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 159 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1730 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4536 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12069 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 60,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1046 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 9552 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 336 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1121 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S2: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once ROT: S3 ×8: je Teil ein erfolgreich beendeter Vorgang (soll 2000, ist 1669)
- S4: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
