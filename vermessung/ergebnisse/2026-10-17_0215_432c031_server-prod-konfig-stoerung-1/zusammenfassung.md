# Vermessung server-prod-konfig-stoerung-1 (stoerung)

Commit `432c031` (sauber), Branch `HEAD`, 2026-10-17T00:15:26.3186147+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.00 0.35 0.28 1/632 1224971
 · CPU-stärkste: 2.0 consul; 1.3 redis-server; 1.2 redis-server; 0.9 systemd; 0.5 dotnet  
Fremdlast nachher: Last 9.20 2.48 1.00 15/864 1226998
 · CPU-stärkste: 394 dotnet;15.8 postgres;13.9 dotnet; 5.9 postgres; 5.9 postgres  
Gültig: **nein** — InvalidOperationException: Host vermessung beendet beim Start (Code 134) — siehe /tmp/cqrs-vermessung-logs/2026-10-17_0215_432c031_server-prod-konfig-stoerung-1/stoerung-beispiel.log
Exactly-once: **ROT**

