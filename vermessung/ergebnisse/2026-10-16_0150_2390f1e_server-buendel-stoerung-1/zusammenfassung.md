# Vermessung server-buendel-stoerung-1 (stoerung)

Commit `2390f1e` (sauber), Branch `HEAD`, 2026-10-15T23:50:25.8266976+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.20 2.54 1.57 1/630 3952679
 · CPU-stärkste: 1.8 consul; 1.3 redis-server; 1.0 redis-server; 0.5 dotnet; 0.1 containerd  
Fremdlast nachher: Last 16.95 7.59 3.54 3/653 3956784
 · CPU-stärkste: 18.1 dotnet;12.7 postgres; 6.8 postgres; 2.9 redis-server; 1.8 consul  
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
| je Vorgang genau ein ProzessBeendet | 55307 | 55307 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 49777 | 49777 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5530 | 5530 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5530 | 5530 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 44247 | 44247 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 22120 | 22120 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5530 | 5530 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 8611 | 8611 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

