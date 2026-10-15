# Vermessung phase-2 (e2e)

Commit `d7941e5` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T05:23:17.6863440+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 19.45 13.32 18.78 · CPU-stärkste: 99.4 /Users/tobi/Library/Application Support/Steam/Steam.AppBundle/Steam/Contents/MacOS/steam_osx; 58.0 /Users/tobi/.dotnet/dotnet; 54.8 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 34.7 dotnet; 18.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Fremdlast nachher: Last 43.27 45.42 37.53 · CPU-stärkste: 306.6 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 55.4 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 51.2 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 23.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 9.9 dotnet  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 396 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 1251 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 159 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 700 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 700 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1194 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,6 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 21551 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 21551 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 102 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 722 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 183 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 451 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 292 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1109 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,3 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 12943 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 12943 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 242 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 3,2 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 2,7 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 592 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 120 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 115204 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22,3 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 254 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 109 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 1244 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 159 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 832 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 798 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 832 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 6232 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 6232 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 116 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 1,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 6,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 197 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 31,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 15853 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 31148 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 27,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 338 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 166 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 2021 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 173 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 42,1 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 919 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 715 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1191 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 13105 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 13105 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 117 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 6,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 11,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1711 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 120 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 31,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 91188 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 7,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 50,1 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 99,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 667 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 203 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1054 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 206 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1054 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 9829 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 11487 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 14,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 5,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 10,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 136 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 5,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 38,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 5357 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 10080 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 24,3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 110 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 247 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 1117 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 195 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 907 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 552 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 925 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 11487 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 11487 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 39,9 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 2,6 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 6,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 10,2 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 50,4 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 239 |
| S4 Datensatz einfrieren | Queries (/s) | 1183 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 202 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 925 |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 762 |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 925 |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 0,8 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 8648 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 8648 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 28,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 27,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 27,8 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 17,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 245 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 778 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 60,3 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 277 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 1655 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 144 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 725 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 617 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1109 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 1292 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 1292 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 392 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 25,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 111 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 67055 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 110511 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 105 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 462 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 7,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 60,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 190 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 1524 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 147 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 562 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 452 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1109 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 11766 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 11766 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 85,7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 25,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 27,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 907 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 32,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 16754 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 32575 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 9,6 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 64,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 258 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 1723 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 166 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 700 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 449 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1109 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 4873 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4873 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once ROT: Read-Model: komplette Paare = ImagePairKomplett im Log (soll 346, ist 343); Read-Model: Paare = ImagePairErstellt im Log (soll 348, ist 347)
- S2: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once ROT: aufgenommene Paare = Paare im Log (soll 354, ist 381)
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
