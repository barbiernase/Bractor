# Vermessung server-leistung-3 (leistung)

Commit `11f9076` (sauber), 2026-10-16T07:43:15.7985552+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.15 2.86 2.15 1/687 251673
 · CPU-stärkste: 100 ps;22.8 dotnet; 1.9 consul; 1.3 redis-server; 1.1 redis-server  
Fremdlast nachher: Last 1.90 2.90 2.47 2/646 258825
 · CPU-stärkste: 5.4 dotnet; 3.3 postgres; 1.9 consul; 1.4 postgres; 1.3 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion (Paare) | 81,9 (76,8–85,0) | 4 012 (3 992–7 060) | 3 643 (3 504–4 221) | 7 178 (6 807–7 623) | – | 21,0 | 146 | 119 (112–124) |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile) | 1 044 (964–1 327) | 2 984 (1 993–3 195) | 824 (651–841) | 1 086 (862–1 304) | – | 1,1 (1,0–1,2) | 16,7 | 4,0 (3,7–5,8) |
| Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge) | 300 (281–314) | 301 (282–315) | 19,2 (17,1–20,1) | 333 (36,3–613) | – | 6,3 (5,8–7,4) | 35,7 | 17,1 (15,6–18,8) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge) | 1 223 (1 159–1 337) | 3 563 (2 579–4 050) | 968 (792–998) | 1 607 (1 446–1 676) | 228 (144–348) | 1,2 (1,0–1,6) | 26,3 (25,8–28,5) | 6,3 (6,0–6,6) |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Leerlauf Beispieldomäne | 3,8 | 3,3 | 62,3 | 2 101 | – | – | – |
| Leerlauf Prüfdomäne | 2,6 | 1,5 | 14,1 | 1 402 | – | – | – |
| Neuaufbau aller Projektionen | – | 193 (153–209) | – | – | 53 156 (42 862–56 032) | 0,3 (0,3–0,3) | 14 480 |

- Bildeingang: Quelle, Fluss, Projektion (Paare): Exactly-once grün
- Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile): Exactly-once grün
- Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge): Exactly-once grün
- Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge): Exactly-once grün
- Leerlauf Beispieldomäne: Exactly-once grün
- Leerlauf Prüfdomäne: Exactly-once grün
- Neuaufbau aller Projektionen: Exactly-once grün
