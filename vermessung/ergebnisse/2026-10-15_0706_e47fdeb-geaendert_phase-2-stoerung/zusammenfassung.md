# Vermessung phase-2-stoerung (stoerung)

Commit `e47fdeb` (1 geänderte Dateien (nicht committet)), Branch `claude/werkbank-zielbild`, 2026-10-15T05:06:14.6901650+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 15.20 21.91 20.24 · CPU-stärkste: 78.9 dotnet; 53.8 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 17.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 9.9 /usr/sbin/coreaudiod; 4.3 /System/Library/PrivateFrameworks/BackgroundTaskManagement.framework/Resources/backgroundtaskmanagementd  
Fremdlast nachher: Last 50.77 41.45 30.29 · CPU-stärkste: 231.0 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 56.0 /Users/tobi/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2; 38.1 dotnet; 27.4 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 21.0 /Applications/Docker.app/Contents/MacOS/com.docker.backend  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | 3502 offen | ✗ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 362 | 362 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 181 | 181 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 362 | 362 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 181 | 181 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 181 | 181 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 181 | 181 | ✓ |
| je Vorgang genau ein ProzessBeendet | 7708 | 4244 | ✗ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 6937 | 3831 | ✗ |
| abgelehnte Aufträge enden ohne Erfolg | 771 | 425 | ✗ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 771 | 426 | ✗ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 6167 | 3412 | ✗ |
| je Verteilung jedes Ziel genau einmal zugebucht | 3080 | 1704 | ✗ |
| je Verteilung genau ein VerteilungAbgeschlossen | 770 | 426 | ✗ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 3653 | 3651 | ✗ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

