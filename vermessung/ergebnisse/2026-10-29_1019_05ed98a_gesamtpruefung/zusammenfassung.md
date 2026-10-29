# Vermessung gesamtpruefung (e2e)

Commit `05ed98a` (sauber), Branch `HEAD`, 2026-10-29T09:19:05.7898234+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 12.30 12.28 7.96 2/1098 3981864
 · CPU jetzt: 47 dotnet; 2 dotnet; 1 dockerd; 1 consul; 1 containerd-shim  
Fremdlast nachher: Last 2.93 3.31 5.08 2/1066 3991186
 · CPU jetzt: 2 containerd; 2 consul; 2 redis-server; 1 dockerd; 1 containerd-shim  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1488 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 1,9 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1160 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1156 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1361 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4856 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4920 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 58,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 594 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,2 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 793 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 793 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 963 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,8 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7141 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 156 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,8 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1387 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2592 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3755 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,7 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 101 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1499 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 15570 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 817 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 847 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 847 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 847 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 7801 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 55,1 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,6 |
| S2 Bildeingang mit Python-Worker, Stoß | Erster Vorgang sichtbar nach (ms) | 1488 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2662 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3511 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 11,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 120 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 623 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 6579 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 187 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 235 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1012 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1012 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1012 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 321 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 321 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 5824 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 888 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1644 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1695 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Erster Vorgang sichtbar nach (ms) | 19,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 527 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 872 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 819 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 14813 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 429 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1192 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1192 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1361 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4723 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4723 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1417 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1278 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1408 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 196 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Erster Vorgang sichtbar nach (ms) | 170 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 12,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 56,1 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1796 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 14501 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 303 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1361 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1179 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1361 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2722 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2722 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2600 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 49,0 |
| S4 Datensatz einfrieren | Queries (/s) | 7781 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 721 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 721 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 437 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 424 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 436 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 87,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 11,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 16,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2331 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19980 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 353 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 842 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 842 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 855 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8845 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 23,6 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 269 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2992 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 10,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 87,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5737 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 10832 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 23,1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 37,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1004 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 9930 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 306 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 963 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 849 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 963 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2077 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 405 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 325 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2415 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 7,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 65,5 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3902 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 7766 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,3 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1191 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 12433 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 298 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 900 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 853 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 900 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2836 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2836 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2836 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 20,8 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 22,5 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 32,4 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 4,7 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 15,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 2499 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 35471 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 152 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1209 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1209 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1361 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 4920 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 4920 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 399 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 399 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 399 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 21,4 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 19,4 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 28,3 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 1,7 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 7,4 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 797 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 6338 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 216 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1195 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1195 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1361 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 0,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 4795 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 4795 |

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
- S6-64: Exactly-once grün
- S6-8: Exactly-once grün
