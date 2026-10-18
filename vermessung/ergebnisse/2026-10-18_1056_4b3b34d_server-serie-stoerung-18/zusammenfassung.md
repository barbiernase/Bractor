# Vermessung server-serie-stoerung-18 (stoerung)

Commit `4b3b34d` (sauber), Branch `HEAD`, 2026-10-18T08:56:01.0326033+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.38 9.10 8.91 2/634 3270363
 · CPU-stärkste: 4.3 redis-server; 2.0 consul; 1.2 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 9.48 9.52 9.07 2/649 3275923
 · CPU-stärkste: 26.9 dotnet;18.7 postgres;11.5 postgres; 4.8 redis-server; 2.0 consul  
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
| je Vorgang genau ein ProzessBeendet | 97800 | 97800 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 88020 | 88020 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 9780 | 9780 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 9780 | 9780 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 78240 | 78240 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 39120 | 39120 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 9780 | 9780 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 87761 | 87761 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

