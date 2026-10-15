# Vermessung server-pf-12-stoerung-1 (stoerung)

Commit `5bda1bb` (sauber), Branch `HEAD`, 2026-10-15T17:44:11.0538437+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.32 4.04 4.83 1/675 3517181
 · CPU-stärkste: 25.8 dotnet; 1.6 consul; 1.2 redis-server; 0.9 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 18.56 9.06 6.48 2/695 3520877
 · CPU-stärkste: 18.9 dotnet;11.4 dotnet; 8.4 postgres; 6.2 postgres; 1.8 sshd  
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
| je Vorgang genau ein ProzessBeendet | 19871 | 19871 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 17884 | 17884 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 1987 | 1987 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 1987 | 1987 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 15897 | 15897 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 7948 | 7948 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 1987 | 1987 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 5017 | 5017 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

