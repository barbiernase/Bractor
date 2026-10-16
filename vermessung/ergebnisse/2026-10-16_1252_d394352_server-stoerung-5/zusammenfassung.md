# Vermessung server-stoerung-5 (stoerung)

Commit `d394352` (sauber), 2026-10-16T10:52:40.3708654+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 19.11 7.60 2.90 1/675 450911
 · CPU-stärkste: 35.6 dotnet; 2.6 redis-server; 1.9 consul; 1.1 redis-server; 0.5 dotnet  
Fremdlast nachher: Last 13.11 9.03 4.16 1/691 454867
 · CPU-stärkste: 19.2 dotnet;18.5 dotnet;14.4 postgres; 7.3 postgres; 3.1 redis-server  
Gültig: **ja**
Exactly-once: **grün**

bildpaare 181 · vorgaenge 56 069 · abbau_s 10,3 · bildeingang_p99_ms 8 262 · verteilt_p99_ms 3 087

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| Bildeingang: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| Verteilt: alle Vorgänge nach den Störungen beendet | ja | ja | ✓ |
| Verteilt: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 362 | 362 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 181 | 181 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 362 | 362 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 181 | 181 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 181 | 181 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 181 | 181 | ✓ |
| je Vorgang genau ein ProzessBeendet | 56069 | 56069 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 50462 | 50462 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5607 | 5607 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5607 | 5607 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 44855 | 44855 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 22428 | 22428 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5607 | 5607 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 9399 | 9399 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
