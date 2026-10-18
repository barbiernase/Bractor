# Vermessung server-serie-stoerung-11 (stoerung)

Commit `4b3b34d` (sauber), Branch `HEAD`, 2026-10-18T08:33:23.9578656+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 7.45 9.01 8.17 1/638 3224313
 · CPU-stärkste: 4.3 redis-server; 2.0 consul; 1.2 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 13.96 10.63 8.86 2/652 3229774
 · CPU-stärkste: 28.3 dotnet;19.0 postgres;14.3 postgres; 4.7 redis-server; 2.0 consul  
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
| je Vorgang genau ein ProzessBeendet | 95959 | 95959 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 86363 | 86363 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 9596 | 9596 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 9596 | 9596 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 76767 | 76767 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 38384 | 38384 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 9596 | 9596 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 85997 | 85997 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

