# Vermessung server-pf-12-stoerung (stoerung)

Commit `0ce0c54` (sauber), Branch `HEAD`, 2026-10-15T17:29:06.3803649+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 5.93 5.03 6.41 1/675 3494313
 · CPU-stärkste: 25.7 dotnet; 1.6 consul; 1.3 redis-server; 0.9 redis-server; 0.6 dotnet  
Fremdlast nachher: Last 6.84 7.68 7.21 2/690 3499226
 · CPU-stärkste: 17.1 dotnet; 9.6 dotnet; 5.4 postgres; 3.0 postgres; 1.6 consul  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | 51 offen | ✗ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 362 | 362 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 181 | 181 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 362 | 362 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 181 | 181 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 181 | 181 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 181 | 181 | ✓ |
| je Vorgang genau ein ProzessBeendet | 20082 | 20031 | ✗ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 18074 | 18027 | ✗ |
| abgelehnte Aufträge enden ohne Erfolg | 2008 | 2004 | ✗ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 2008 | 2004 | ✗ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 16066 | 16024 | ✗ |
| je Verteilung jedes Ziel genau einmal zugebucht | 8032 | 8012 | ✗ |
| je Verteilung genau ein VerteilungAbgeschlossen | 2008 | 2003 | ✗ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 5174 | 5174 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

