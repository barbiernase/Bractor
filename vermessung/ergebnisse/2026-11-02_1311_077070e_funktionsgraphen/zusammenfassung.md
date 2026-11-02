# Vermessung funktionsgraphen (e2e)

Commit `077070e` (sauber), Branch `HEAD`, 2026-11-02T12:11:40.7776892+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.66 0.47 0.22 1/721 1621312
 · CPU jetzt: 101 dotnet; 1 consul  
Fremdlast nachher: Last 0.99 1.23 0.71 2/686 1626807
 · CPU jetzt: 3 consul; 1 dockerd; 1 dotnet; 1 postgres; 1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 34,2 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 357 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 6,2 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,5 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1695 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1695 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1696 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 5197 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 5261 |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 47,2 |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 214 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0,5 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3,6 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1,3 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 739 |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 739 |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 783 |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0,9 |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 9820 |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 9820 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 5862 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 142 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,1 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 2109 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3186 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 4130 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 4,1 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 71,9 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 575 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10129 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 843 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1003 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1003 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1003 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 800 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 800 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 4373 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 719 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 2026 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1671 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Erster Vorgang sichtbar nach (ms) | 19,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 422 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 543 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 0,5 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 8,2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 721 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 12844 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 287 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1677 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1677 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1677 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 5062 |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 5062 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1332 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 1100 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1331 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 348 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Erster Vorgang sichtbar nach (ms) | 157 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 6,7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 40,6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1,5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 10,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1810 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 12386 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 248 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1665 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1665 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1665 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 2824 |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 2824 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 2578 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0,1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0,1 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0,0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3,1 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 32,6 |
| S4 Datensatz einfrieren | Queries (/s) | 7675 |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 804 |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 804 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 481 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 472 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 480 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 55,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Erster Vorgang sichtbar nach (ms) | 12,2 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 6,4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 13,5 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 5,7 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 25,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2639 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 11614 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 228 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 721 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 721 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 721 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 3284 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 3284 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 4739 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1046 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 3311 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 2338 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Erster Vorgang sichtbar nach (ms) | 34,9 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 279 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 537 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | 3,4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | 71,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0,2 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 14,3 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 582 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 34309 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 144 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 759 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 759 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 759 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 9820 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 9820 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1650 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1397 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1637 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 460 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Erster Vorgang sichtbar nach (ms) | 19,1 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 17,4 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 36,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 17,8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 1456 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 25011 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 228 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 742 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 742 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 742 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1,0 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 6323 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6323 |
| S6-64 Clients über gRPC, 64 Sitzungen | angenommen (Vorgänge/s) | 2868 |
| S6-64 Clients über gRPC, 64 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 2868 |
| S6-64 Clients über gRPC, 64 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 2868 |
| S6-64 Clients über gRPC, 64 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Erster Vorgang sichtbar nach (ms) | 30,2 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p50 (ms) | 22,3 |
| S6-64 Clients über gRPC, 64 Sitzungen | Latenz p99 (ms) | 35,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p50 (ms) | 4,6 |
| S6-64 Clients über gRPC, 64 Sitzungen | Quittung p99 (ms) | 18,3 |
| S6-64 Clients über gRPC, 64 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen je Vorgang (1) | 0,9 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries je Vorgang (1) | 12,7 |
| S6-64 Clients über gRPC, 64 Sitzungen | Transaktionen (/s) | 2563 |
| S6-64 Clients über gRPC, 64 Sitzungen | Queries (/s) | 35975 |
| S6-64 Clients über gRPC, 64 Sitzungen | CPU Host (% eines Kerns) | 271 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host (max.) (MB) | 1696 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host am Fensterende (MB) | 1696 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1696 |
| S6-64 Clients über gRPC, 64 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors am Fensterende (1) | 5261 |
| S6-64 Clients über gRPC, 64 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 5261 |
| S6-8 Clients über gRPC, 8 Sitzungen | angenommen (Vorgänge/s) | 357 |
| S6-8 Clients über gRPC, 8 Sitzungen | sichtbar im Fenster (Vorgänge/s) | 357 |
| S6-8 Clients über gRPC, 8 Sitzungen | Durchsatz bis alles sichtbar (Vorgänge/s) | 357 |
| S6-8 Clients über gRPC, 8 Sitzungen | Rückstand am Fensterende (Vorgänge) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Abbauzeit (s) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Erster Vorgang sichtbar nach (ms) | 24,5 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p50 (ms) | 22,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Latenz p99 (ms) | 32,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p50 (ms) | 2,6 |
| S6-8 Clients über gRPC, 8 Sitzungen | Quittung p99 (ms) | 10,9 |
| S6-8 Clients über gRPC, 8 Sitzungen | Wiederverbunden (1) | 0,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen je Vorgang (1) | 2,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries je Vorgang (1) | 15,8 |
| S6-8 Clients über gRPC, 8 Sitzungen | Transaktionen (/s) | 709 |
| S6-8 Clients über gRPC, 8 Sitzungen | Queries (/s) | 5621 |
| S6-8 Clients über gRPC, 8 Sitzungen | CPU Host (% eines Kerns) | 210 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host (max.) (MB) | 1693 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host am Fensterende (MB) | 1693 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS Host Höchststand des Laufs (MB) | 1693 |
| S6-8 Clients über gRPC, 8 Sitzungen | RSS am Ende / Höchststand (1) | 1,0 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors am Fensterende (1) | 5134 |
| S6-8 Clients über gRPC, 8 Sitzungen | Aktive Actors, Höchststand der Fensterenden (1) | 5134 |

- L1: Exactly-once grün
- L5: Exactly-once grün
- S1: Exactly-once grün
- S3-64: Exactly-once grün
- S3-8: Exactly-once grün
- S4: Exactly-once grün
- S5-1: Exactly-once grün
- S5-64: Exactly-once grün
- S5-8: Exactly-once grün
- S6-64: Exactly-once grün
- S6-8: Exactly-once grün
