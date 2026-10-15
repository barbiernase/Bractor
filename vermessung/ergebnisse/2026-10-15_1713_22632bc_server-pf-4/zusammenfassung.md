# Vermessung server-pf-4 (e2e)

Commit `22632bc` (sauber), Branch `HEAD`, 2026-10-15T15:13:23.8847489+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.11 1.39 2.36 2/715 3283826
 · CPU-stärkste: 370 dotnet; 1.7 consul; 1.2 redis-server; 1.1 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 0.34 1.04 2.11 3/776 3286206
 · CPU-stärkste: 100 ps;54.2 dotnet;15.1 dotnet; 2.8 sshd; 1.7 consul  
Gültig: **nein** — TimeoutException: Host vermessung nicht bereit nach 120 s — siehe /tmp/cqrs-vermessung-logs/2026-10-15_1713_22632bc_server-pf-4/beispiel-w1.log
Exactly-once: **ROT**

