# Vermessung graphen-schritt4-5 (e2e)

Commit `55dbcd8` (sauber), Branch `HEAD`, 2026-11-02T06:37:12.6993914+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.56 2.05 1.67 1/712 1263705
 · CPU jetzt: 98 dotnet; 1 rcu_preempt; 1 dotnet; 1 dotnet; 1 redis-server  
Fremdlast nachher: Last 0.56 1.36 1.47 1/684 1266536
 · CPU jetzt: 2 dotnet; 2 consul; 2 redis-server; 2 redis-server; 1 md2_raid1  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 53,3 |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 407 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5,8 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2,4 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 963 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 960 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 963 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 800 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 800 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 9958 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 141 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 4,2 |
| S1 Bildeingang Host, Stoß | Erster Vorgang sichtbar nach (ms) | 1156 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 3171 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 4195 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 4,0 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 69,3 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 557 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 9618 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 892 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 924 |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 924 |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 924 |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 800 |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 800 |

- L1: Exactly-once grün
- S1: Exactly-once grün
