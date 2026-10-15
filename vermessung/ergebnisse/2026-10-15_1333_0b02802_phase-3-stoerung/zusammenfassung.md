# Vermessung phase-3-stoerung (stoerung)

Commit `0b02802` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T11:33:19.4774050+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 11.30 21.56 19.17 · CPU-stärkste: 33.2 dotnet; 26.3 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 7.4 /usr/sbin/coreaudiod; 6.3 /Applications/Firefox.app/Contents/MacOS/plugin-container.app/Contents/MacOS/plugin-container; 6.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine  
Fremdlast nachher: Last 82.63 70.46 47.78 · CPU-stärkste: 317.2 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 89.3 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 20.6 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 5.2 /usr/sbin/coreaudiod; 2.9 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | 397 offen | ✗ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 362 | 362 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 181 | 181 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 362 | 362 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 181 | 181 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 181 | 181 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 181 | 181 | ✓ |
| je Vorgang genau ein ProzessBeendet | 24642 | 24277 | ✗ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 22178 | 21868 | ✗ |
| abgelehnte Aufträge enden ohne Erfolg | 2464 | 2423 | ✗ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 2464 | 2429 | ✗ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 19714 | 19458 | ✗ |
| je Verteilung jedes Ziel genau einmal zugebucht | 9856 | 9713 | ✗ |
| je Verteilung genau ein VerteilungAbgeschlossen | 2464 | 2426 | ✗ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 21646 | 21648 | ✗ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

