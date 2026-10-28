# Vermessung server-pubsub-phase6-stoerung (stoerung)

Commit `4f43172` (sauber), Branch `HEAD`, 2026-10-28T04:46:16.7080413+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 1.95 1.21 1.30 1/674 4025793
 · CPU-stärkste: 12.4 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 0.75 4.28 4.45 3/640 4042824
 · CPU-stärkste: 26.6 postgres;12.6 dotnet; 2.1 consul; 1.3 redis-server; 1.3 redis-server  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | 4 offen | ✗ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 364 | 364 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 182 | 182 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 364 | 364 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 182 | 182 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 182 | 182 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 182 | 182 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 90503 | 90503 | ✓ |
| je abgelehntem Auftrag genau ein AuftragGescheitert | 11313 | 11313 | ✓ |
| kein Auftrag mit zwei Ausgängen | 0 | 0 | ✓ |
| kein gelingender Auftrag gescheitert | 0 | 0 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 11313 | 11313 | ✓ |
| keine Freigabe zweimal (je Auftrag höchstens ein PostenFreigegeben) | 0 | 0 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 45252 | 45244 | ✗ |
| kein Ziel einer Verteilung zweimal zugebucht | 0 | 0 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 11313 | 11309 | ✗ |
| Funktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 108272 | 108216 | ✗ |
| Funktion: je Eingang genau eine Wirkung (Zubuchungen je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: kein abgelehnter Command hat geschrieben | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

