# Vermessung server-stoerung-4 (stoerung)

Commit `d394352` (sauber), 2026-10-16T10:49:36.0858993+00:00  
Rechner: Ubuntu-noble-latest-amd64-base, Ubuntu 24.04.4 LTS, 13th Gen Intel(R) Core(TM) i5-13500, 20 Kerne, 62 GiB  
Fremdlast vorher: Last 4.20 1.15 0.44 2/716 445555
 · CPU-stärkste: 370 dotnet; 1.9 consul; 1.3 redis-server; 1.1 redis-server; 0.2 dotnet  
Fremdlast nachher: Last 20.08 7.60 2.87 2/690 449730
 · CPU-stärkste: 37.4 dotnet;17.7 dotnet;13.8 postgres;11.6 postgres; 2.9 redis-server  
Gültig: **ja**
Exactly-once: **grün**

bildpaare 181 · vorgaenge 56 673 · abbau_s 10,0 · bildeingang_p99_ms 8 247 · verteilt_p99_ms 2 459

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
| je Vorgang genau ein ProzessBeendet | 56673 | 56673 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 51006 | 51006 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 5667 | 5667 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 5667 | 5667 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 45339 | 45339 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 22668 | 22668 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 5667 | 5667 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 10060 | 10060 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (Events je CommandId in genau einer Transaktion) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal noop-markiert | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
