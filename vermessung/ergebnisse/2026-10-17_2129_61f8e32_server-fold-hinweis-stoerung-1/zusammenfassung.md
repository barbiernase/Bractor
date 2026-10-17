# Vermessung server-fold-hinweis-stoerung-1 (stoerung)

Commit `61f8e32` (sauber), Branch `HEAD`, 2026-10-17T19:29:31.8610456+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.79 1.58 1.42 1/680 2326386
 · CPU-stärkste: 22.8 dotnet; 2.0 consul; 1.4 redis-server; 1.2 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 12.24 5.68 2.94 3/1056 2330583
 · CPU-stärkste: 554 dotnet;51.9 dotnet;19.4 dotnet;15.5 dotnet; 2.7 redis-server  
Gültig: **nein** — PostgresException: 53100: could not resize shared memory segment "/PostgreSQL.2651519776" to 4312640 bytes: No space left on device
Exactly-once: **ROT**

