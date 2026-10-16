# Vermessung server-leistung-6 (leistung)

Commit `d394352` (sauber), 2026-10-16T11:01:36.1002788+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.30 4.89 3.81 1/634 463892
 · CPU-stärkste: 1.9 consul; 1.8 redis-server; 1.1 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 1.48 3.09 3.38 1/644 470595
 · CPU-stärkste: 5.3 dotnet; 3.7 postgres; 1.9 consul; 1.8 redis-server; 1.1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion (Paare) | 82,6 (80,6–85,5) | 3 550 (3 375–4 737) | 3 687 (3 549–3 843) | 7 089 (6 752–7 317) | – | 20,9 | 146 | 117 (111–122) |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile) | 1 330 (1 002–1 371) | 3 206 (2 271–3 208) | 665 (625–797) | 892 (863–1 125) | – | 1,1 (1,0–1,2) | 16,7 | 5,4 (3,4–5,5) |
| Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge) | 303 (287–309) | 304 (288–311) | 16,8 (14,7–21,2) | 46,4 (34,4–611) | – | 6,9 (6,1–7,3) | 35,4 (33,5–37,9) | 17,7 (14,3–19,0) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge) | 1 185 (1 077–1 239) | 3 505 (2 896–3 629) | 1 039 (845–1 153) | 1 697 (1 545–1 798) | 266 (102–476) | 1,4 (1,0–1,6) | 27,2 (26,5–28,3) | 6,8 (6,3–8,1) |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Leerlauf Beispieldomäne | 3,8 | 3,3 | 62,3 | 2 048 | – | – | – |
| Leerlauf Prüfdomäne | 2,6 | 1,6 | 13,1 | 1 282 | – | – | – |
| Neuaufbau aller Projektionen | – | 167 (144–191) | – | – | 56 828 | 0,3 | 14 480 |

- Bildeingang: Quelle, Fluss, Projektion (Paare): Exactly-once grün
- Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile): Exactly-once grün
- Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge): Exactly-once grün
- Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge): Exactly-once grün
- Leerlauf Beispieldomäne: Exactly-once grün
- Leerlauf Prüfdomäne: Exactly-once grün
- Neuaufbau aller Projektionen: Exactly-once grün
