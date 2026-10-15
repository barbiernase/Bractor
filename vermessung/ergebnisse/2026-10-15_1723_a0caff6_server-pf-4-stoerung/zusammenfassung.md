# Vermessung server-pf-4-stoerung (stoerung)

Commit `a0caff6` (sauber), Branch `HEAD`, 2026-10-15T15:23:43.0726915+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 8.24 4.60 3.02 1/670 3299141
 · CPU-stärkste: 42.0 dotnet; 1.7 consul; 1.3 redis-server; 1.1 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 31.45 21.72 10.83 15/690 3306501
 · CPU-stärkste: 15.1 dotnet;14.5 dotnet;13.8 postgres; 4.8 postgres; 1.7 redis-server  
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
| je Vorgang genau ein ProzessBeendet | 43075 | 43075 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 38768 | 38768 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 4307 | 4307 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 4307 | 4307 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 34461 | 34461 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 17228 | 17228 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 4307 | 4307 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 37749 | 37749 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

