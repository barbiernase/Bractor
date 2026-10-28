# Vermessung server-pubsub-g17-stoerung-2 (stoerung)

Commit `acb3620` (sauber), Branch `HEAD`, 2026-10-28T09:16:59.6916337+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.70 10.79 10.59 1/627 897968
 · CPU-stärkste: 2.1 consul; 1.3 redis-server; 1.1 redis-server; 0.4 dotnet; 0.2 containerd  
Fremdlast nachher: Last 5.12 11.64 11.70 3/637 923876
 · CPU-stärkste: 15.9 dotnet;13.1 postgres; 4.9 postgres; 2.1 consul; 1.3 redis-server  
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
| je gelingendem Auftrag genau ein AuftragBestaetigt | 86674 | 86674 | ✓ |
| je abgelehntem Auftrag genau ein AuftragGescheitert | 10834 | 10834 | ✓ |
| kein Auftrag mit zwei Ausgängen | 0 | 0 | ✓ |
| kein gelingender Auftrag gescheitert | 0 | 0 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 10834 | 10834 | ✓ |
| keine Freigabe zweimal (je Auftrag höchstens ein PostenFreigegeben) | 0 | 0 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 43336 | 43336 | ✓ |
| kein Ziel einer Verteilung zweimal zugebucht | 0 | 0 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 10834 | 10834 | ✓ |
| Funktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 102586 | 102586 | ✓ |
| Funktion: je Eingang genau eine Wirkung (Zubuchungen je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

