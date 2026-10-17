# Vermessung server-fold-hinweis-stoerung-3 (stoerung)

Commit `76d3692` (sauber), Branch `HEAD`, 2026-10-17T19:38:41.7867145+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.17 4.87 3.58 1/729 2342239
 · CPU-stärkste: 405 dotnet; 2.0 consul; 1.7 redis-server; 1.2 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 10.33 7.59 4.82 10/703 2346561
 · CPU-stärkste: 42.1 dotnet;17.4 dotnet;12.9 postgres; 5.9 postgres; 2.6 redis-server  
Gültig: **ja**
Exactly-once: **grün**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | ja | ✓ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 362 | 362 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 181 | 181 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 362 | 362 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 181 | 181 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 181 | 181 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 181 | 181 | ✓ |
| je Vorgang genau ein ProzessBeendet | 55265 | 55265 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 49739 | 49739 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5526 | 5526 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5526 | 5526 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 44213 | 44213 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 22104 | 22104 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5526 | 5526 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 10048 | 10048 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

