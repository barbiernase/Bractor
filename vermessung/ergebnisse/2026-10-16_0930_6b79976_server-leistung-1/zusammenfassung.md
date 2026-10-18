# Vermessung server-leistung-1 (leistung)

Commit `6b79976` (sauber), 2026-10-16T07:30:41.6653862+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.46 1.49 0.93 2/718 234783
 · CPU-stärkste: 408 dotnet; 1.9 consul; 1.3 redis-server; 1.1 redis-server; 0.2 systemd  
Fremdlast nachher: Last 1.81 2.56 1.67 1/691 240994
 · CPU-stärkste: 24.3 dotnet; 5.4 dotnet; 3.2 postgres; 1.9 consul; 1.3 redis-server  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion | 82,8 | 4 101 (3 957–4 286) | 3 567 (3 475–3 925) | 7 009 | – | 20,9 | 145 | 119 (111–119) |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender | 1 030 (968–1 064) | 3 013 (2 326–3 180) | 767 (732–835) | 1 331 (1 042–1 412) | – | 1,0 (1,0–1,1) | 16,8 | 5,8 (4,9–6,6) |
| Verteilt ruhig: Prüfdomäne, 1 Sender | 297 (285–307) | 298 (286–309) | 17,8 (15,0–20,4) | 44,2 (34,7–589) | – | 7,0 (6,0–7,4) | 35,4 (32,9–37,0) | 15,9 (14,8–16,9) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender | 1 183 (1 062–1 256) | 3 856 (3 493–3 913) | 1 066 (928–1 173) | 1 758 (1 650–1 971) | 254 (57,2–669) | 1,1 (0,8–1,8) | 26,2 (26,1–27,9) | 7,2 (6,3–8,7) |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion | 11 982 | 982 (875–982) | – | 1 708 (1 597–1 788) | – | – | – |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender | 14 942 (14 545–17 017) | 521 (425–672) | – | 1 843 (1 764–1 860) | – | – | – |
| Verteilt ruhig: Prüfdomäne, 1 Sender | 10 385 (8 833–10 574) | 443 (426–475) | – | 1 025 (859–1 187) | – | – | – |
| Verteilt an der Decke: Prüfdomäne, 64 Sender | 29 170 (25 369–30 141) | 805 (684–845) | – | 1 165 (998–1 227) | – | – | – |
| Neuaufbau aller Projektionen | – | 190 | – | – | 34 520 | 0,4 | 14 480 |
| Leerlauf Beispieldomäne | 3,8 | 3,2 | 61,3 | 1 852 | – | – | – |
| Leerlauf Prüfdomäne | 2,6 | 1,7 | 12,1 | 1 221 | – | – | – |

- Bildeingang: Quelle, Fluss, Projektion: Exactly-once ROT: je Datei genau eine Quell-Nachricht im Log (soll 1200, ist 2420); je Datei genau eine Quell-Nachricht im Log (soll 1200, ist 3620)
- Heißer Schreiber: ein Sammelvorgang, 64 Sender: Exactly-once grün
- Verteilt ruhig: Prüfdomäne, 1 Sender: Exactly-once grün
- Verteilt an der Decke: Prüfdomäne, 64 Sender: Exactly-once grün
- Neuaufbau aller Projektionen: Exactly-once grün
- Leerlauf Beispieldomäne: Exactly-once grün
- Leerlauf Prüfdomäne: Exactly-once grün
