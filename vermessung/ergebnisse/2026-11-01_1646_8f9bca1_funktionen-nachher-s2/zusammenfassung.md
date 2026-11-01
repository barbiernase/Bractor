# Vermessung funktionen-nachher-s2 (e2e)

Commit `8f9bca1` (sauber), Branch `HEAD`, 2026-11-01T15:46:07.7643738+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.95 0.76 0.30 2/719 350080
 · CPU jetzt: 100 dotnet; 2 dotnet; 1 rcu_preempt; 1 redis-server  
Fremdlast nachher: Last 0.18 0.41 0.28 2/707 355739
 · CPU jetzt: 2 dotnet; 2 consul; 2 redis-server; 1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Kennzahl | Median |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 920 |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0,0 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 4,7 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 1,2 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 467 |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 465 |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 467 |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 61,0 |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 61,0 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 6833 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57,9 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3,4 |
| S2 Bildeingang mit Python-Worker, Stoß | Erster Vorgang sichtbar nach (ms) | 1472 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2951 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3436 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 8,5 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 101 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 478 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 5680 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 184 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 278 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 969 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 969 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 969 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1,0 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 933 |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 933 |

- L1: Exactly-once grün
- S2: Exactly-once grün
