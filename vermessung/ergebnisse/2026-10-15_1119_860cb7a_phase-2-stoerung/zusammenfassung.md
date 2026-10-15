# Vermessung phase-2-stoerung (stoerung)

Commit `860cb7a` (sauber), Branch `claude/werkbank-zielbild`, 2026-10-15T09:19:08.3601820+00:00  
Rechner: MacBook-Air-von-Tobias-2, Darwin 24.5.0 Darwin Kernel Version 24.5.0: Tue Apr 22 19:54:43 PDT 2025; root:xnu-11417.121.6~2/RELEASE_ARM64_T8132, Apple M4, 10 Kerne, 16 GiB  
Fremdlast vorher: Last 8.47 21.05 22.63 · CPU-stärkste: 130.3 /Users/tobi/.dotnet/dotnet; 27.4 dotnet; 9.3 /Users/tobi/.dotnet/dotnet; 9.3 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 9.0 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine  
Fremdlast nachher: Last 68.97 48.74 34.95 · CPU-stärkste: 311.4 /System/Library/Frameworks/Virtualization.framework/Versions/A/XPCServices/com.apple.Virtualization.VirtualMachine.xpc/Contents/MacOS/com.apple.Virtualization.VirtualMachine; 98.6 /Applications/Docker.app/Contents/MacOS/com.docker.backend; 22.3 /System/Library/PrivateFrameworks/SkyLight.framework/Resources/WindowServer; 21.6 /System/Library/ExtensionKit/Extensions/Storage.appex/Contents/MacOS/Storage; 20.6 /Applications/JetBrains Toolbox.app/Contents/MacOS/jetbrains-toolbox  
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
| je Vorgang genau ein ProzessBeendet | 29609 | 29609 | ✓ |
| kein Vorgang mit mehr als einem ProzessBeendet | 0 | 0 | ✓ |
| gelingende Vorgänge enden mit Erfolg | 26648 | 26648 | ✓ |
| abgelehnte Aufträge enden ohne Erfolg | 2961 | 2961 | ✓ |
| jede Kompensation genau einmal (PostenFreigegeben je abgelehntem Auftrag) | 2961 | 2961 | ✓ |
| je gelingendem Auftrag genau ein AuftragBestaetigt | 23687 | 23687 | ✓ |
| je Verteilung jedes Ziel genau einmal zugebucht | 11844 | 11844 | ✓ |
| je Verteilung genau ein VerteilungAbgeschlossen | 2961 | 2961 | ✓ |
| Reaktion: je Reservierung unter dem Mindestbestand genau ein Nachschub | 26257 | 26257 | ✓ |
| Reaktion: je Reaktions-Command genau eine Wirkung (Ziel-Events je Ursache = 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Beispieldomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Beispieldomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal gewirkt (KommandoVerarbeitet je CommandId ≤ 1) | 0 | 0 | ✓ |
| Prüfdomäne: keine CommandId zweimal abgelehnt-markiert | 0 | 0 | ✓ |
| Prüfdomäne: kein Stream mit doppelter Version | 0 | 0 | ✓ |

