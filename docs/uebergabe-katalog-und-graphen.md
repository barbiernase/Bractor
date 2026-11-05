# Übergabe: Funktionskatalog, Übersetzung und die Modale für Funktion und Graph

Du arbeitest im Repo `/Users/tobi/Dev/CqrsSolution`, Zweig `pubsub_neustrukturierung`, an einem allgemeinen CQRS-/Event-Sourcing-Framework mit
Werkbank (Editor). Lies zuerst `CLAUDE.md`, dein Gedächtnis (`MEMORY.md`) und dann **`docs/plan-katalog-und-graphen.md`** — das ist der Maßstab
dieser Arbeit. Antworte auf Deutsch, in Prosa, ohne Kürzel-Salat. Umgesetzt wird **Schritt für Schritt, je Schritt auf Tobis Wort** (E5).

## Stand

Schritte 1–5 des Plans sind umgesetzt, auf dem Server geprüft (schnelle Stufe, `--check`, `--sonde` grün) und im Editor abgenommen:

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
5. **Übersetzung und Eingänge** — `Uebersetze<TEin, TAus>` mit `[Feld]` (Lesart `Uebersetzungsform`, CQRS119–121, inline im Kind bzw. am Kopf im
   Graph-Actor, Wirker = Knoten); Prüfdomäne mit Katalog `Rechenwerk` und Garantie-Test; Sonde `Funkortung`; im Editor ↦-Karte, „＋ Eingang“
   (Quelle oder Client), „↦ ▣ Command an …“, im Aggregat „＋ Graph, der ihn schickt“, kein „Command ohne Ziel“ unter den Ergebnissen, Picker nur mit
   erstem Knoten. Abgenommen bis zum Bauen; durchgespielt nicht im Editor (lokal kein Postgres), „genau einmal“ belegt der Garantie-Test.

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

## Nächster Schritt: Schritt 6 (Bestand umziehen und Abbau)

Steht im Plan: jeder Auftrag aus einem `Decide` wird ein Event, auf das ein Client reagiert; danach Abbau der Arbeit für Akteure und des offenen
Eingangs aus dem `Decide`, eine Build-Regel „ein `Decide` gibt nur Events, Hinweise und Fristen aus“. Vorher mit Tobi klären: A4 (Anwendungsfunktionen)
und ob `Bildeingang` Katalogfunktion plus Übersetzung wird.

## Schritt 5 (erledigt, zur Einordnung)

Steht vollständig im Plan. Kern: `Uebersetze`/`FeldAttribute` in `Abstractions`, eine Lesart `Uebersetzungsform.cs` (per Link in Generator,
Analyzer, Extractor wie `Infrastructure.SourceGeneration/Graphform.cs`), `Graphform` kennt den neuen Knotentyp, Generator und Laufzeit rechnen ihn
inline (`Infrastructure/Funktionen/Kette.cs`, Kind, Graph-Actor), Analyzer ab CQRS119 mit Gegenstück in `DomainEditor/Grammatik.cs`, Sonde
(`GraphExtractor.Kern/Sonde/`, `soll.txt`) und Garantie-Test in der Prüfdomäne. Im Editor: „＋ Eingang“ (Quelle oder Client) am linken Rand des
Graph-Modals, „→ ▣ Command an …“ am Ausgang, „＋ Graph, der ihn schickt“ im Aggregat-Modal, kein „Command ohne Ziel“ mehr unter den Ergebnissen eines
`Decide`, die ↦-Karte; der Picker der Fläche legt keinen leeren Graphen mehr an.

## Tobis Experimente im Bestand

Der Commit „Werkbank-Stand von Tobi“ enthält Experimente aus der Werkbank: den Katalog `Bildverarbeitung.Katalog` (Funktion `ResizeFast` mit
`ResizeFastAuftrag` → `ResizeFastErgebnis`, von den Abläufen genutzt, Codegen nimmt ihre Nachrichten mit) und Änderungen an `Domain/ImagePair`
(zusätzliche Events, erweiterte Decides). Drei leere Graphen und eine leere Funktion `Resize` sind wieder entfernt — `HEAD` baut. Frag Tobi, bevor du
an seinen Experimenten etwas änderst.

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

## Gelernt in Schritt 5

- Eine Übersetzung ist ein `Graphform.Knoten` mit `Uebersetzung` gesetzt und einer Hülle als `Funktion` (Auf ohne Methode) — so gelten Kanten,
  Köpfe, Ausgänge und Kreise unverändert; Generator (`KnotenPlan`), Bausteine und Laufzeit-Parents überspringen sie.
- Im Roslyn-Fork der Sonde gibt es keine transitiven Projektreferenzen: wer über den Abläufen liegt (Laufzeit, Akteure), braucht die
  Katalog-Referenz ausdrücklich. Ein Namespace, der in zwei Verzeichnissen liegt, hat kein Verzeichnis (Fixpunkt bricht) — Sonden-Teile bei den
  Abläufen bekommen einen eigenen Namespace.
- `Id<Modell>` in `Domain.Pipeline.Modell` meint den Namespace, nicht das Aggregat: kopierte Feldtypen bekommen dort den vollen Namen.
- Ein Command ohne Ziel darf kein Feld `AggregateId` tragen (CQRS013): eine Client-Nachricht nennt die Ziel-Id `<Aggregat>Id`, die Übersetzung
  ordnet sie mit `[Feld]` zu.
- Die Instanz-Geste fragt den Namen mit `prompt()` — das Browser-Pane bedient ihn nicht; in einer Probe die Instanzdatei von Hand schreiben.
- Ein Menü, das nach einer Geste aufgehen soll, erst nach `render()` öffnen (`setTimeout`); Abfragen im Browser kurz danach sehen es sonst noch nicht.

## Gelernt in den Schritten 1–4

- Der `Dateipuffer` behält jetzt das UTF-8-BOM (Solution und Projektdateien tragen eins).
- `uniq()` im Editor griff auf alte Modell-Sammlungen zu; behoben.
- Neue Funktionen im Editor tragen die Server-Id `funktion:<Name>` (`WirkerId.FuerFunktion`), sonst schließt ein offenes Modal, wenn das Board
  vom Server zurückkommt; Graphen `graph:<Name>`.
- Eine Projektdatei in einer Geste lädt den Arbeitsbereich kalt neu (`Arbeitsbereich.UebernimmAsync`).
- Ändert sich `domain.proto` (auch durch einen genutzten Katalog — der Codegen nimmt seine Nachrichten mit), müssen die Python-DTOs neu:
  `PATH=/opt/miniconda3/envs/gpu_env/bin:$PATH Domain.Client.Worker.Python.ML/build_python.sh` und `domain_client/generated/__init__.py` samt
  `.proto_hash` committen — sonst scheitert der Python-Worker am Import (Integrationstest „Der Python-Worker hat die Arbeit … nicht angenommen“).
  Der Build erzeugt sie nicht von selbst.
- Die Ports je Auf im Graph-Modal rechnet der Editor aus der Signatur im Modell (`komGruppen`); die Sicht (`Sicht.Komposition.cs`) blieb flach.
- Offen aus Schritt 3: neue Zustandsklasse (`IZustand<T>`) im Funktions-Modal, Schalter „lauscht“, `CancellationToken` als Schalter; Technik heißt im
  Editor-Code noch „gerufen“.
