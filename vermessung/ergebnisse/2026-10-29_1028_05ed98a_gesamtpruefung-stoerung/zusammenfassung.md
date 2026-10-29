# Vermessung gesamtpruefung-stoerung (stoerung)

Commit `05ed98a` (sauber), Branch `HEAD`, 2026-10-29T09:28:12.4802670+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 3.45 3.41 5.10 1/1060 3992843
 · CPU jetzt: 3 consul; 2 redis-server; 2 dotnet; 2 redis-server; 1 kworker/5:0-events  
Fremdlast nachher: Last 4.16 8.86 7.77 2/637 4017124
 · CPU jetzt: 4 dotnet; 2 consul; 2 redis-server; 1 redis-server  
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
| je gelingendem Auftrag genau ein AuftragBestaetigt | 89023 | 89023 | ✓ |
| je abgelehntem Auftrag genau ein AuftragGescheitert | 11128 | 11128 | ✓ |
| kein Auftrag mit zwei Ausgängen | 0 | 0 | ✓ |
| kein gelingender Auftrag gescheitert | 0 | 0 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 11128 | 11128 | ✓ |
| keine Freigabe zweimal (je Auftrag höchstens ein PostenFreigegeben) | 0 | 0 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 44508 | 44508 | ✓ |
| kein Ziel einer Verteilung zweimal zugebucht | 0 | 0 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 11127 | 11127 | ✓ |
| Funktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 106014 | 106014 | ✓ |
| Funktion: je Eingang genau eine Wirkung (Zubuchungen je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

