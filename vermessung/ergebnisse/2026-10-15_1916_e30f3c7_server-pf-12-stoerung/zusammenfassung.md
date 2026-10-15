# Vermessung server-pf-12-stoerung (stoerung)

Commit `e30f3c7` (sauber), Branch `HEAD`, 2026-10-15T17:16:01.2322467+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 8.74 6.69 7.96 1/673 3473756
 · CPU-stärkste: 32.6 dotnet; 1.6 consul; 1.3 redis-server; 0.9 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 19.83 11.02 9.25 2/694 3477508
 · CPU-stärkste: 24.1 dotnet;11.3 dotnet; 8.2 postgres; 3.8 postgres; 1.6 redis-server  
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
| je Vorgang genau ein ProzessBeendet | 18930 | 18930 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 17037 | 17037 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 1893 | 1893 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 1893 | 1893 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 15144 | 15144 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 7572 | 7572 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 1893 | 1893 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 4947 | 4947 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

