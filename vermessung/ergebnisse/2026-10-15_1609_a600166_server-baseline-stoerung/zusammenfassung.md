# Vermessung server-baseline-stoerung (stoerung)

Commit `a600166` (sauber), Branch `HEAD`, 2026-10-15T14:09:12.3017393+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 9.74 5.82 2.99 1/944 3200996
 · CPU-stärkste: 19.0 dotnet; 9.2 docker-proxy; 1.7 consul; 1.3 redis-server; 1.1 dotnet  
Fremdlast nachher: Last 34.67 30.70 16.51 7/919 3210705
 · CPU-stärkste: 31.4 docker-proxy;11.9 dotnet; 8.0 postgres; 4.3 postgres; 1.6 redis-server  
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
| je Vorgang genau ein ProzessBeendet | 56847 | 56847 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 51163 | 51163 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5684 | 5684 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5684 | 5684 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 45479 | 45479 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 22736 | 22736 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5684 | 5684 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 50782 | 50782 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

