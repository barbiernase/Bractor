# Vermessung server-pf-4-stoerung (stoerung)

Commit `ffaeaea` (sauber), Branch `HEAD`, 2026-10-15T16:25:15.7997263+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.53 5.67 5.63 1/668 3402036
 · CPU-stärkste: 30.4 dotnet; 1.6 consul; 1.4 redis-server; 0.9 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 31.67 27.55 15.93 11/648 3412023
 · CPU-stärkste: 11.9 dotnet; 6.7 postgres; 4.9 postgres; 1.6 redis-server; 1.6 consul  
Gültig: **ja**
Exactly-once: **grün**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | ja | ✓ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 364 | 364 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 182 | 182 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 364 | 364 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 182 | 182 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 182 | 182 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 182 | 182 | ✓ |
| je Vorgang genau ein ProzessBeendet | 51027 | 51027 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 45925 | 45925 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5102 | 5102 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5102 | 5102 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 40823 | 40823 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 20408 | 20408 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5102 | 5102 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 44636 | 44636 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

