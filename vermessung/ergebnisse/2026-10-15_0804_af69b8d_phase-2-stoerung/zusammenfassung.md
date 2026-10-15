# Vermessung phase-2-stoerung (stoerung)

Commit `af69b8d` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T06:04:39.9872940+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 4.56 9.70 14.20 · CPU-stärkste: 129.6 /Users/tobi/.dotnet/dotnet; 25.3 dotnet; 9.2 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 6.9 /Applications/Google Chrome.app/Contents/Frameworks/Google Chrome Framework.framework/Versions/153.0.8010.50/Helpers/Google Chrome Helper.app/Contents/MacOS/Google Chrome Helper; 6.1 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine  
Fremdlast nachher: Last 98.95 50.57 30.51 · CPU-stärkste: 316.5 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 95.6 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 76.0 /System/Library/PrivateFrameworks/iCloudDriveCore.framework/Versions/A/Support/bird; 38.4 /System/Library/ExtensionKit/Extensions/Storage.appex/Contents/MacOS/Storage; 34.4 /usr/libexec/fairplaydeviceidentityd  
Gültig: **nein** — Exactly-once-Prüfung fehlgeschlagen
Exactly-once: **ROT**

| Prüfung | Soll | Ist | ok |
|---|---:|---:|:-:|
| S1: alle Paare nach den Störungen sichtbar | ja | ja | ✓ |
| S5: alle Vorgänge nach den Störungen beendet | ja | 2092 offen | ✗ |
| S5: kein Eingang endgültig gescheitert | 0 | 0 | ✓ |
| je Datei genau eine Quell-Nachricht im Log | 362 | 362 | ✓ |
| keine Datei zweimal gemeldet | 0 | 0 | ✓ |
| je Paar genau ein ImagePairErstellt | 181 | 181 | ✓ |
| BildVerfuegbar genau einmal je Bild (2 je Paar) | 362 | 362 | ✓ |
| kein Paar mit mehr als 2 BildVerfuegbar | 0 | 0 | ✓ |
| je Paar genau ein ImagePairKomplett | 181 | 181 | ✓ |
| Read-Model: komplette Paare = ImagePairKomplett im Log | 181 | 181 | ✓ |
| Read-Model: Paare = ImagePairErstellt im Log | 181 | 181 | ✓ |
| je Vorgang genau ein ProzessBeendet | 27097 | 25008 | ✗ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 24388 | 22520 | ✗ |
| abgelehnte Aufträge enden ohne Erfolg | 2709 | 2490 | ✗ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 2709 | 2496 | ✗ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 21679 | 20028 | ✗ |
| je Verteilung jedes Ziel genau einmal zugebucht | 10836 | 9990 | ✗ |
| je Verteilung genau ein VerteilungAbgeschlossen | 2709 | 2496 | ✗ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 22136 | 22135 | ✗ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

