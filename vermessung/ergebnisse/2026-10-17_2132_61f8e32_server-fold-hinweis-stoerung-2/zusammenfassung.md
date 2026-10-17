# Vermessung server-fold-hinweis-stoerung-2 (stoerung)

Commit `61f8e32` (sauber), Branch `HEAD`, 2026-10-17T19:32:34.0796059+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 10.51 5.53 2.92 1/680 2331780
 · CPU-stärkste: 15.2 dotnet; 2.3 redis-server; 2.0 consul; 1.2 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 8.06 6.61 3.75 2/655 2336084
 · CPU-stärkste: 19.8 postgres;18.4 dotnet;11.3 postgres; 2.5 redis-server; 2.0 consul  
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
| je Vorgang genau ein ProzessBeendet | 54852 | 54852 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 49367 | 49367 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5485 | 5485 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5485 | 5485 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 43882 | 43882 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 21940 | 21940 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5485 | 5485 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 7634 | 7634 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

