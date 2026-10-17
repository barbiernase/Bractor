# Vermessung server-prod-konfig-stoerung-2 (stoerung)

Commit `432c031` (sauber), Branch `HEAD`, 2026-10-17T00:17:20.2652829+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.69 2.57 1.06 1/628 1228291
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 10.96 4.02 1.66 13/864 1230364
 · CPU-stärkste: 423 dotnet;14.1 postgres;12.7 dotnet; 5.2 postgres; 5.2 postgres  
Gültig: **nein** — InvalidOperationException: Host vermessung beendet beim Start (Code 134) — siehe /tmp/cqrs-vermessung-logs/2026-10-17_0217_432c031_server-prod-konfig-stoerung-2/stoerung-beispiel.log
Exactly-once: **ROT**

