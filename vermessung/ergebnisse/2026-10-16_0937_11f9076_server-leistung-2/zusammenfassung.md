# Vermessung server-leistung-2 (leistung)

Commit `11f9076` (sauber), 2026-10-16T07:37:24.7308622+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.94 3.03 1.88 2/724 243448
 · CPU-stärkste: 403 dotnet; 1.9 consul; 1.3 redis-server; 1.1 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 2.08 2.65 2.08 2/700 250516
 · CPU-stärkste: 23.4 dotnet; 5.2 dotnet; 3.3 postgres; 1.9 consul; 1.5 postgres  
Gültig: **ja**
Exactly-once: **grün**

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion (Paare) | 82,9 | 4 265 (2 506–4 439) | 3 449 (3 392–3 738) | 7 000 | – | 20,8 | 145 | 117 |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile) | 1 063 (1 017–1 390) | 3 042 (2 241–3 178) | 730 (618–743) | 1 098 (824–1 235) | – | 1,1 (1,0–1,2) | 16,7 | 5,5 (3,7–6,1) |
| Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge) | 285 (272–294) | 286 (273–296) | 17,8 (16,7–22,4) | 44,5 (43,1–708) | – | 7,1 (5,5–7,4) | 35,4 (33,4–36,5) | 16,1 (15,8–18,3) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge) | 1 165 (1 164–1 254) | 3 348 (2 993–4 590) | 999 (795–1 067) | 1 680 (1 371–1 828) | 114 (80,4–474) | 1,4 (1,0–1,6) | 27,2 (26,8–28,8) | 7,5 (6,2–7,9) |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Leerlauf Beispieldomäne | 3,8 | 3,4 | 62,4 | 1 987 | – | – | – |
| Leerlauf Prüfdomäne | 2,6 | 2,0 | 11,0 | 1 323 | – | – | – |
| Neuaufbau aller Projektionen | – | 185 (152–195) | – | – | 46 616 (45 510–58 327) | 0,3 (0,2–0,3) | 14 480 |

- Bildeingang: Quelle, Fluss, Projektion (Paare): Exactly-once grün
- Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile): Exactly-once grün
- Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge): Exactly-once grün
- Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge): Exactly-once grün
- Leerlauf Beispieldomäne: Exactly-once grün
- Leerlauf Prüfdomäne: Exactly-once grün
- Neuaufbau aller Projektionen: Exactly-once grün
