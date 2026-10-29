# Vermessung gesamtpruefung-ab-b (e2e)

Commit `05ed98a` (sauber), Branch `HEAD`, 2026-10-29T09:53:45.6869925+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.40 1.79 3.69 1/672 4037786
 · CPU jetzt: 2 consul; 2 redis-server; 2 dotnet; 1 redis-server  
Fremdlast nachher: Last 0.52 1.16 2.60 3/639 4045984
 · CPU jetzt: 3 consul; 2 containerd; 2 containerd-shim; 2 dotnet; 1 kworker/5:1H-kblockd  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 1491 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 7,1 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,0 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1210 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1210 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1210 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 4721 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 4721 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 55,3 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 591 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,0 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 6,9 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,1 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 890 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 890 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 992 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9271 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7212 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 150 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 3,9 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1585 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 2896 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 3930 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 9,8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 105 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1432 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 15384 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 766 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 971 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 971 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 971 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 719 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 719 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4656 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 838 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1621 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1640 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Erster Vorgang sichtbar nach (ms) | 14,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 518 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 793 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 10,9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 884 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 16035 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 331 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1210 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1210 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1210 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 4721 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 4721 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1393 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1340 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1383 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 76,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Erster Vorgang sichtbar nach (ms) | 159 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11,9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 45,2 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 11,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1678 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 13473 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 285 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1196 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1196 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1196 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2720 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2720 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 426 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 413 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 425 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 93,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 16,9 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 7,1 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 20,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 46,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2284 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 19503 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 319 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 872 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 872 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 872 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3271 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 8525 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 73,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 247 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2974 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 11,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 31,8 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 5136 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 11808 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 2,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 58,7 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 3,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 37,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 939 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 9235 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 267 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 992 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 889 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 992 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9271 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 2138 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 341 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 262 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2522 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 10,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 19,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 3461 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 10149 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 3,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 39,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 975 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 10080 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 266 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 925 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 870 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 925 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 0,9 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6271 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6271 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
