# Vermessung server-korreliert-stoerung (stoerung)

Commit `6e361a6` (sauber), Branch `HEAD`, 2026-10-15T18:24:42.7532650+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.95 3.99 3.31 1/675 3573420
 · CPU-stärkste: 35.4 dotnet; 1.6 consul; 1.2 redis-server; 0.9 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 13.15 7.34 4.63 2/690 3577425
 · CPU-stärkste: 25.6 dotnet;15.4 dotnet;10.1 postgres; 9.1 postgres; 2.3 redis-server  
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
| je Vorgang genau ein ProzessBeendet | 29794 | 29794 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 26815 | 26815 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 2979 | 2979 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 2979 | 2979 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 23836 | 23836 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 11916 | 11916 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 2979 | 2979 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 4749 | 4749 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

