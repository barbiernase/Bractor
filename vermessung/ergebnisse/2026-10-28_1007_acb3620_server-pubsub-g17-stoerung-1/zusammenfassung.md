# Vermessung server-pubsub-g17-stoerung-1 (stoerung)

Commit `acb3620` (sauber), Branch `HEAD`, 2026-10-28T09:07:00.4159629+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 6.52 11.19 9.63 1/711 870565
 · CPU-stärkste: 367 dotnet; 2.1 consul; 1.3 redis-server; 1.1 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 7.46 11.56 10.82 27/699 896501
 · CPU-stärkste: 33.8 postgres;16.2 dotnet;13.0 dotnet; 4.5 postgres; 2.1 consul  
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
| je gelingendem Auftrag genau ein AuftragBestaetigt | 90556 | 90556 | ✓ |
| je abgelehntem Auftrag genau ein AuftragGescheitert | 11319 | 11319 | ✓ |
| kein Auftrag mit zwei Ausgängen | 0 | 0 | ✓ |
| kein gelingender Auftrag gescheitert | 0 | 0 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 11319 | 11319 | ✓ |
| keine Freigabe zweimal (je Auftrag höchstens ein PostenFreigegeben) | 0 | 0 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 45276 | 45276 | ✓ |
| kein Ziel einer Verteilung zweimal zugebucht | 0 | 0 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 11319 | 11319 | ✓ |
| Funktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 105997 | 105997 | ✓ |
| Funktion: je Eingang genau eine Wirkung (Zubuchungen je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

