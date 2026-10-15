# Vermessung phase-2 (e2e)

Commit `4caa176` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T05:45:04.0071810+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 3.93 9.39 20.39 · CPU-stärkste: 119.0 /Users/tobi/.dotnet/dotnet; 11.0 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 8.6 dotnet; 7.2 /System/Library/Frameworks/CoreServices.framework/Frameworks/Metadata.framework/Versions/A/Support/mds_stores; 5.9 /usr/sbin/coreaudiod  
Fremdlast nachher: Last 32.23 21.09 23.09 · CPU-stärkste: 273.5 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 87.9 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 25.7 /usr/libexec/PerfPowerServices; 9.5 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 5.1 /usr/libexec/symptomsd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 1536 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5182 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 337 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1875 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1175 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2407 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,5 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 63827 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 63827 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 226 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 4215 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 244 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 991 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 980 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1048 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 25622 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 25622 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1819 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 3,0 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 33,3 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 599 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 17,7 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 10354 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 17565 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 30,6 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 227 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1016 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 7532 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 393 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1184 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1068 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1184 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 4840 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 4840 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 1517 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 22,8 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 8,7 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 4777 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 8549 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 48,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 305 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1081 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6860 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 284 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 77,7 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1220 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1044 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1220 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 0,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 10661 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 10661 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 1010 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 39,4 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 90,3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1922 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 20,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 11941 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 20330 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 11,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 60,7 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1057 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 5428 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 428 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2388 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2027 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2407 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 30235 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 30235 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 265 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 88,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 100 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1335 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 12,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 5222 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 15096 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 8,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 50,4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 844 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 4998 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 443 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2407 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1667 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2407 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 13490 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 13490 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1828 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,2 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,2 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 42,4 |
| S4 Datensatz einfrieren | Queries (/s) | 4134 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 4854 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 4854 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 131 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 130 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 131 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 17,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 47,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 92,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1355 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 8217 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 377 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 997 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 915 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1013 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 4527 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4527 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2356 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 128 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 22,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 13996 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 21980 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 21,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 145 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 61,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 799 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7752 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 376 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1024 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 930 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1048 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19972 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19972 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 399 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 174 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 138 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1692 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 14,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 4702 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 14210 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 7,2 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 62,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 975 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8426 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 292 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1048 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1024 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1048 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 10993 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10993 |

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
