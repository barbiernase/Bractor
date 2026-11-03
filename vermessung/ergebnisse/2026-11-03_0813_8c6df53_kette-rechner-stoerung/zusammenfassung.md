# Vermessung kette-rechner-stoerung (stoerung)

Commit `8c6df53` (sauber), Branch `HEAD`, 2026-11-03T07:13:51.2730078+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.63 1.95 1.39 1/668 2815558
 · CPU jetzt: 2 dotnet; 2 consul; 1 kworker/19:1H-kblockd; 1 redis-server; 1 redis-server  
Fremdlast nachher: Last 6.64 3.44 2.01 3/693 2818869
 · CPU jetzt: 2 dockerd; 2 dotnet; 2 containerd-shim; 2 containerd-shim; 1 containerd  
Gültig: **ja**
Exactly-once: **grün**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | ja | ✓ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 182 | 182 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 364 | 364 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 182 | 182 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 182 | 182 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 182 | 182 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 99315 | 99315 | ✓ |
| je abgelehntem Auftrag genau ein AuftragGescheitert | 12414 | 12414 | ✓ |
| kein Auftrag mit zwei Ausgängen | 0 | 0 | ✓ |
| kein gelingender Auftrag gescheitert | 0 | 0 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 12414 | 12414 | ✓ |
| keine Freigabe zweimal (je Auftrag höchstens ein PostenFreigegeben) | 0 | 0 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 49656 | 49656 | ✓ |
| kein Ziel einer Verteilung zweimal zugebucht | 0 | 0 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 12414 | 12414 | ✓ |
| Funktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 111534 | 111534 | ✓ |
| Funktion: je Eingang genau eine Wirkung (Zubuchungen je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

