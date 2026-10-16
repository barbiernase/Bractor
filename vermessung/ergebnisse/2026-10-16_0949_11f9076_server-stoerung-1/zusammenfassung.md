# Vermessung server-stoerung-1 (stoerung)

Commit `11f9076` (sauber), 2026-10-16T07:49:09.1661732+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 2.43 2.99 2.51 1/633 260051
 · CPU-stärkste: 1.9 consul; 1.3 redis-server; 1.1 redis-server; 0.6 dotnet; 0.1 containerd  
Fremdlast nachher: Last 13.72 6.80 3.95 3/987 264036
 · CPU-stärkste: 768 dotnet;56.7 dotnet;18.8 dotnet; 4.0 postgres; 4.0 postgres  
Gültig: **nein** — PostgresException: 53100: could not resize shared memory segment "/PostgreSQL.2491488578" to 4308800 bytes: No space left on device
Exactly-once: **ROT**

