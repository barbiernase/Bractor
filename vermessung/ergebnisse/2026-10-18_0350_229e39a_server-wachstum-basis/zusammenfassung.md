# Vermessung server-wachstum-basis (e2e)

Commit `229e39a` (sauber), Branch `HEAD`, 2026-10-18T01:50:41.3766295+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.53 1.21 1.10 2/725 2768816
 · CPU-stärkste: 393 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 1.35 0.72 0.89 2/687 2776335
 · CPU-stärkste: 17.6 dotnet; 3.2 dotnet; 2.0 consul; 1.3 redis-server; 1.2 redis-server  
Gültig: **nein** — TimeoutException: S4: Range nicht vollständig aufgenommen nach 300 s
Exactly-once: **ROT**

