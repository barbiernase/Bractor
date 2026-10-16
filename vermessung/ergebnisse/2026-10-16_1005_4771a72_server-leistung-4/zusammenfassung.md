# Vermessung server-leistung-4 (leistung)

Commit `4771a72` (sauber), 2026-10-16T08:05:06.2852540+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 12.38 9.24 6.09 1/682 292587
 · CPU-stärkste: 17.3 dotnet; 2.5 redis-server; 1.9 consul; 1.1 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 1.29 4.43 4.97 2/647 299362
 · CPU-stärkste: 5.4 dotnet; 3.4 postgres; 1.9 consul; 1.8 redis-server; 1.4 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion (Paare) | 82,3 | 4 736 (3 647–5 674) | 3 589 | 7 082 | – | 20,9 | 146 | 115 (114–128) |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile) | 922 (905–1 099) | 3 035 (2 296–3 093) | 880 (785–937) | 1 539 (975–1 557) | – | 1,0 (1,0–1,2) | 16,7 | 5,0 (4,7–6,5) |
| Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge) | 298 (297–320) | 299 (298–321) | 19,5 (15,2–20,5) | 473 (38,6–589) | – | 6,1 (5,9–7,4) | 35,6 (35,4–37,2) | 16,1 (15,7–16,8) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge) | 1 166 (1 157–1 223) | 3 464 (3 376–3 577) | 1 019 (895–1 031) | 1 717 (1 616–1 757) | 254 (137–283) | 1,1 (1,0–1,5) | 26,0 (25,6–27,4) | 7,3 |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Leerlauf Beispieldomäne | 3,8 | 3,5 | 61,3 | 2 006 | – | – | – |
| Leerlauf Prüfdomäne | 2,5 | 1,6 | 13,0 | 1 301 | – | – | – |
| Neuaufbau aller Projektionen | – | 157 | – | – | 56 772 (44 016–63 083) | 0,3 (0,2–0,3) | 14 480 |

- Bildeingang: Quelle, Fluss, Projektion (Paare): Exactly-once grün
- Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile): Exactly-once grün
- Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge): Exactly-once grün
- Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge): Exactly-once grün
- Leerlauf Beispieldomäne: Exactly-once grün
- Leerlauf Prüfdomäne: Exactly-once grün
- Neuaufbau aller Projektionen: Exactly-once grün
