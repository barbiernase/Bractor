# Vermessung server-pf-4-stoerung (stoerung)

Commit `22632bc` (sauber), Branch `HEAD`, 2026-10-15T15:15:33.7427376+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.99 1.37 2.21 1/674 3287367
 · CPU-stärkste: 50.8 dotnet; 1.7 consul; 1.2 redis-server; 1.1 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 0.39 0.97 1.96 3/776 3289733
 · CPU-stärkste: 100 ps;28.2 dotnet;15.8 dotnet; 1.7 consul; 1.2 redis-server  
Gültig: **nein** — TimeoutException: Host vermessung nicht bereit nach 120 s — siehe /tmp/cqrs-vermessung-logs/2026-10-15_1715_22632bc_server-pf-4-stoerung/stoerung-beispiel.log
Exactly-once: **ROT**

