# Vermessung server-leistung-5 (leistung)

Commit `d394352` (sauber), 2026-10-16T10:55:44.9474278+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 11.49 8.81 4.14 1/674 456036
 · CPU-stärkste: 18.7 dotnet; 2.7 redis-server; 1.9 consul; 1.1 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 4.27 4.90 3.80 2/644 462685
 · CPU-stärkste: 5.5 dotnet; 3.6 postgres; 1.9 consul; 1.9 redis-server; 1.1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion (Paare) | 83,0 (79,9–85,7) | 3 622 (3 092–4 718) | 3 579 (3 481–3 775) | 6 962 (6 808–7 281) | – | 20,9 | 146 | 116 (113–119) |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile) | 908 (846–1 075) | 2 928 (1 740–3 084) | 857 (848–1 165) | 1 279 (1 224–1 529) | – | 1,0 (1,0–1,1) | 16,7 | 5,2 (5,0–6,1) |
| Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge) | 292 (281–306) | 294 (282–307) | 16,4 (15,8–22,8) | 43,7 (43,1–831) | – | 6,9 (5,6–7,4) | 35,4 (31,9–38,2) | 17,9 (13,4–19,3) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge) | 1 126 | 3 465 (3 318–3 884) | 1 016 (933–1 182) | 1 797 (1 770–1 906) | 458 (108–808) | 1,5 (0,9–1,7) | 27,2 | 6,8 (6,5–8,3) |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Leerlauf Beispieldomäne | 3,8 | 3,3 | 61,3 | 2 183 | – | – | – |
| Leerlauf Prüfdomäne | 2,5 | 1,6 | 11,0 | 1 249 | – | – | – |
| Neuaufbau aller Projektionen | – | 160 (147–166) | – | – | 54 061 (42 197–57 427) | 0,3 (0,3–0,3) | 14 480 |

- Bildeingang: Quelle, Fluss, Projektion (Paare): Exactly-once grün
- Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile): Exactly-once grün
- Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge): Exactly-once grün
- Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge): Exactly-once grün
- Leerlauf Beispieldomäne: Exactly-once grün
- Leerlauf Prüfdomäne: Exactly-once grün
- Neuaufbau aller Projektionen: Exactly-once grün
