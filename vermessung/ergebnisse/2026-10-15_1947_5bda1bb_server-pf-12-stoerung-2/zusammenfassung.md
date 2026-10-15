# Vermessung server-pf-12-stoerung-2 (stoerung)

Commit `5bda1bb` (sauber), Branch `HEAD`, 2026-10-15T17:47:12.9037510+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 16.66 8.96 6.48 1/675 3521995
 · CPU-stärkste: 18.7 dotnet; 1.6 consul; 1.5 redis-server; 0.9 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 16.09 11.16 7.63 2/695 3525795
 · CPU-stärkste: 14.8 dotnet;11.7 dotnet; 8.1 postgres; 7.0 postgres; 1.6 redis-server  
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
| je Vorgang genau ein ProzessBeendet | 19817 | 19817 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 17836 | 17836 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 1981 | 1981 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 1981 | 1981 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 15855 | 15855 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 7924 | 7924 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 1981 | 1981 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 4425 | 4425 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

