# Übergabe: Funktionskatalog, Übersetzung und die Modale für Funktion und Graph

Du arbeitest im Repo `/Users/tobi/Dev/CqrsSolution`, Zweig `pubsub_neustrukturierung`, an einem allgemeinen CQRS-/Event-Sourcing-Framework mit
Werkbank (Editor). Lies zuerst `CLAUDE.md`, dein Gedächtnis (`MEMORY.md`) und dann **`docs/plan-katalog-und-graphen.md`** — das ist der Maßstab
dieser Arbeit. Antworte auf Deutsch, in Prosa, ohne Kürzel-Salat. Umgesetzt wird **Schritt für Schritt, je Schritt auf Tobis Wort** (E5).

## Stand

Schritte 1–4 des Plans sind umgesetzt, auf dem Server geprüft (schnelle Stufe, `--check`, `--sonde` grün) und im Editor abgenommen:

1. **Drift aufgeräumt** — ein `Auf` nimmt genau eine Nachricht (GR-EINGANG); `BausteinEingang.Ports` und Join-Reste sind weg.
2. **Kataloge** — Rolle `Katalog` (`<Name>.Katalog`, referenziert nur `Abstractions`/`Core`/andere Kataloge) in `Abstractions/Projektstruktur.cs`,
   Primitive `Hinweis`; CQRS098 weist Fakten, Aggregat-Commands und Aggregate aus einem Katalog; `--check` prüft die Referenzrichtung (die Domäne
   kennt keinen Katalog); Codegen und Werkbank-Stand nehmen die Kataloge der Abläufe mit; Geste `/api/werkbank/katalog` legt einen Katalog an
   (Projektdatei, Solution, Referenz der Abläufe — `GraphExtractor.Kern/Schreiben/Kataloge.cs`); die Sonde hat einen fremden Katalog
   `Sondenkatalog.Katalog` als eigenes Projekt.
3. **Funktions-Modal** (`funZeige` in `Werkbank/wwwroot/editor/editor.js`, auf `innenZeige`): Eingänge · Auf · Ausgänge · Rahmen; die Form der
   Ausgabe folgt aus Strom und Zahl der Ausgänge, dazu `BausteinEingang.Asynchron`; Scaffolder und Abgleich schreiben die Rückgabe daraus.
4. **Graph-Modal** (Komposition): Ports je Auf mit Vielfachheit, ＋ an Ein- und Ausgängen mit gestrichelter „neue Funktion davor/dahinter“,
   „＋ Quelle“, Empfänger am rechten Rand, Baukasten mit „＋ einsetzen“ statt Ziehen, ✕ samt Funktion.

## Was entschieden ist (Kurzfassung — Einzelheiten im Plan §1)

- **Wer Funktionen anstößt:** nur **Clients** (Akteur mit `IDarf<T>`) und **Quellen**. Ein `Decide` übersetzt einen Command in Events (dazu
  Hinweise, Fristen) — sonst nichts. Keine Arbeit für Akteure aus einem `Decide`, keine Ereignisquelle, kein neuer Knotentyp dafür. **Eventual
  Consistency:** der Client sieht den Stand über die Projektionen und stößt an, was danach kommt. Weg:
  `Client / Quelle → Funktion / Übersetzer → Command → Decide → Events → Projektionen → Client`.
- **Isolation:** Funktionen (im Katalog) und Aggregate kennen einander nicht; nur der Kompositionsgraph der Anwendung kennt beide. Dazwischen steht
  eine **Übersetzung** ohne Logik: ein Framework-Knoten `Uebersetze<TEin, TAus>` im Graph-Record, Felder automatisch nach Namen und implizit
  zuweisbarem Typ, Abweichungen als `[Feld(nameof(Ziel), nameof(Quelle))]`; die Id des Ziel-Aggregats kommt aus der Nachricht. Sie läuft inline
  auf dem Actor, auf dem die Nachricht gerade ist — kein eigener Schritt, kein Zustand.
- **Ein `Auf` = eine Nachricht**, kein Bündel für Funktionen; `OneOf`-Fälle sind eigene Ports (gleiche Bedeutung = gleicher Typ).
- **Funktionen sind keine virtuellen Actors** (Ort per Konfiguration, `Rechner` in der Instanz; Kette bleibt auf dem Rechner ihres Kopfs).
- **Alles im Editor**, bei kleiner Code-API: neu im Code sind nur `Uebersetze<TEin, TAus>` und `[Feld]`.

## Nächster Schritt: Schritt 5 (Übersetzung und Eingänge)

Steht vollständig im Plan. Kern: `Uebersetze`/`FeldAttribute` in `Abstractions`, eine Lesart `Uebersetzungsform.cs` (per Link in Generator,
Analyzer, Extractor wie `Infrastructure.SourceGeneration/Graphform.cs`), `Graphform` kennt den neuen Knotentyp, Generator und Laufzeit rechnen ihn
inline (`Infrastructure/Funktionen/Kette.cs`, Kind, Graph-Actor), Analyzer ab CQRS119 mit Gegenstück in `DomainEditor/Grammatik.cs`, Sonde
(`GraphExtractor.Kern/Sonde/`, `soll.txt`) und Garantie-Test in der Prüfdomäne. Im Editor: „＋ Eingang“ (Quelle oder Client) am linken Rand des
Graph-Modals, „→ ▣ Command an …“ am Ausgang, „＋ Graph, der ihn schickt“ im Aggregat-Modal, kein „Command ohne Ziel“ mehr unter den Ergebnissen eines
`Decide`, die ↦-Karte; der Picker der Fläche legt keinen leeren Graphen mehr an.

## Achtung im Arbeitsbaum

Der Commit „Werkbank-Stand von Tobi“ enthält Tobis Experimente aus der Werkbank: einen Katalog `Bildverarbeitung.Katalog` (mit `Resize` ohne `Auf`
und `ResizeFast`), Änderungen an `Domain/ImagePair` (zusätzliche Events, erweiterte Decides) und **drei leere Graphen** `NeuerGraph`,
`NeuerGraph2`, `NeuerGraph3` in `Domain.Pipeline/Datensatz/Graphen.cs`. **Mit ihnen baut die Lösung nicht** (CQRS115: ein Graph nennt seine Knoten als
Parameter). Frag Tobi, bevor du daran etwas löschst oder änderst; Server-Läufe auf diesem Stand scheitern am Build.

## Arbeitsweise (aus dem Gedächtnis, hier zur Sicherheit)

- **Tests nur auf dem Messserver:** `scripts/server.sh lauf scripts/tests.sh schnell` (bzw. `… lauf bash -c '…'` für `--check`/`--sonde`), immer im
  Hintergrund, Ergebnis abwarten; er läuft auf dem **gepushten** `HEAD`. Lokal nur `dotnet build`.
- **Gesten nie in Tobis Werkbank (5178) prüfen** — sie schreibt in den echten Baum. Eigene Probe: `git worktree add -b probe-… <scratchpad>/wt-… HEAD`,
  dort `dotnet build Host.Grpc` und `dotnet build Werkbank`, temporärer Eintrag „Werkbank-Probe“ in `.claude/launch.json`
  (`cd <wt>/Werkbank && PORT=5191 exec dotnet bin/Debug/net9.0/Werkbank.dll`), `preview_start`; das kalte Laden dauert ≈ 50 s. Danach Preview stoppen,
  `launch.json` zurücksetzen, Worktree und Branch entfernen.
- Nach Änderungen an Werkbank-Server-Code Tobis Werkbank neu starten (`preview_stop`/`preview_start` „Werkbank“) und den Link
  http://localhost:5178/editor/ nennen; nur JS/CSS: Neuladen reicht.
- Commits **ohne** `Co-Authored-By`, nur die eigenen Dateien stagen; Tobis offene Änderungen nie mitnehmen, ohne zu fragen.
- Handeln statt fragen: sicherer Standard, Annahmen benennen, höchstens eine Rückfrage. Nicht Fähigkeiten des Bestands stillschweigend zur
  Anforderung machen — eine Primitive bleibt, weil sie allgemein gebraucht wird (W11).

## Gelernt in den Schritten 1–4

- Der `Dateipuffer` behält jetzt das UTF-8-BOM (Solution und Projektdateien tragen eins).
- `uniq()` im Editor griff auf alte Modell-Sammlungen zu; behoben.
- Neue Funktionen im Editor tragen die Server-Id `funktion:<Name>` (`WirkerId.FuerFunktion`), sonst schließt ein offenes Modal, wenn das Board
  vom Server zurückkommt; Graphen `graph:<Name>`.
- Eine Projektdatei in einer Geste lädt den Arbeitsbereich kalt neu (`Arbeitsbereich.UebernimmAsync`).
- Die Ports je Auf im Graph-Modal rechnet der Editor aus der Signatur im Modell (`komGruppen`); die Sicht (`Sicht.Komposition.cs`) blieb flach.
- Offen aus Schritt 3: neue Zustandsklasse (`IZustand<T>`) im Funktions-Modal, Schalter „lauscht“, `CancellationToken` als Schalter; Technik heißt im
  Editor-Code noch „gerufen“.
