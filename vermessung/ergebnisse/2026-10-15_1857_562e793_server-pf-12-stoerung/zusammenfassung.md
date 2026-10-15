# Vermessung server-pf-12-stoerung (stoerung)

Commit `562e793` (sauber), Branch `HEAD`, 2026-10-15T16:57:32.9615288+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.28 3.30 5.02 1/678 3446557
 · CPU-stärkste: 23.4 dotnet; 1.6 consul; 1.3 redis-server; 0.9 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 29.78 21.68 12.66 8/646 3455501
 · CPU-stärkste: 11.7 dotnet; 6.1 postgres; 6.0 postgres; 1.7 redis-server; 1.6 consul  
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
| je Vorgang genau ein ProzessBeendet | 45843 | 45843 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 41259 | 41259 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 4584 | 4584 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 4584 | 4584 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 36675 | 36675 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 18336 | 18336 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 4584 | 4584 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 40515 | 40515 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

