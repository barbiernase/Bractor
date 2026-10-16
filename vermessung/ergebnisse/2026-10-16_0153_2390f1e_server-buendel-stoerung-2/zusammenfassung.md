# Vermessung server-buendel-stoerung-2 (stoerung)

Commit `2390f1e` (sauber), Branch `HEAD`, 2026-10-15T23:53:36.7178174+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 13.51 7.28 3.50 1/632 3958214
 · CPU-stärkste: 2.4 redis-server; 1.8 consul; 1.0 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 19.44 11.47 5.63 2/655 3962612
 · CPU-stärkste: 18.8 dotnet;12.6 postgres; 6.3 postgres; 2.9 redis-server; 1.8 consul  
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
| je Vorgang genau ein ProzessBeendet | 57016 | 57016 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 51315 | 51315 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5701 | 5701 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5701 | 5701 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 45614 | 45614 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 22804 | 22804 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5701 | 5701 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 9407 | 9407 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

