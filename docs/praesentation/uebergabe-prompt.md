# Übergabe: Präsentation „Software, die nichts vergisst“

Du arbeitest an einer HTML-Präsentation (reveal.js) über das CQRS-/Event-Sourcing-Framework in diesem Repo
(`/Users/tobi/Dev/CqrsSolution`, Zweig `pubsub_neustrukturierung`). Lies zuerst `CLAUDE.md` und dein Gedächtnis.

## Worum es geht

Die Präsentation erklärt das **Zielbild** des Frameworks (Maßstab: `docs/plan-pubsub-neustrukturierung.md`), nicht den Umbau.
Sie ist **eine durchgehende Geschichte**: Wir begleiten „Bildpaar 4711“ durch einen Tag an der Linie (Kamera → Aufbereitung →
Bildeingang → Paar entscheidet → KI → Liste der Inspekteurin → sie drückt F → Rechte → alle sehen es). Jede Station zeigt
erst den menschlichen Moment, dann die Karte im Editor-Stil, dann klein „Fachwort: …“; der Code liegt eine Ebene tiefer (↓).
Danach: Konsistenz und Garantien („Wann sieht wer was?“), der Rest des Tages am Arbeitsplatz (Ablehnung, WLAN weg, Anmeldung),
eine neue Anforderung (Werkbank, KI füllt den Code über den 🤖-Knoten, Test, Ausprobieren, Protokoll, Einreichen), „Alles auf
einen Blick“, Schluss. Am Anfang steht „Was wir lösen“ mit fünf Punkten (KI schreibt sicher mit, Fehler fallen vor dem Start
auf, Fachsprache wird zu Code, die KI liest viel weniger, Regeln, die sich selbst prüfen) samt Detailfolien.

## Dateien (alle in `docs/praesentation/`)

| Datei | Rolle |
|---|---|
| `geschichte_full.html` | **Hauptquelle**: alle Folien der vollen Fassung, lesbar. Hier Texte und Folien ändern. |
| `geschichte.html` | kurze Fassung (älter, noch nicht in Alltagssprache umgeschrieben) |
| `zielbild-seite.html` | Scroll-Seite; liefert Karten (`NODES`), Graphen (`GRAPHS`), Diagramme, CSS und das Render-Skript |
| `baue_deck.py` | setzt Vorlage + Bausteine zum Deck zusammen; Platzhalter `{{name}}` / `{{el_code:N}}` / `{{proto:x}}` |
| `zielbild_full.html`, `zielbild.html` | **erzeugte** Decks (nie von Hand ändern) |
| `reveal.min.css`, `reveal.min.js` | reveal.js 5.1.0 (MIT), werden eingebettet |
| `schriften.css` | Archivo, IBM Plex Sans/Mono (OFL) als data-URIs, eingebettet → Deck läuft vollständig ohne Netz |

Bauen und öffnen:

```bash
VORLAGE=geschichte_full.html python3 docs/praesentation/baue_deck.py docs/praesentation/zielbild_full.html
python3 docs/praesentation/baue_deck.py          # kurze Fassung
open docs/praesentation/zielbild_full.html
```

Zum Prüfen alle Fragmente auf einmal zeigen: `ALLE=1` setzen und in eine Scratch-Datei bauen. Gerendert wird mit
Headless-Chrome (`--headless=new --window-size=1440,810 --screenshot=… "file://…#/h/v"`, je Aufruf mit `perl -e 'alarm 45; exec @ARGV'`
gegen Hänger), Kontaktbögen mit PIL. Folien-Index `#/h/v` ist 0-basiert; die Struktur ergibt sich aus den `<section>`-Ebenen.

Das Deck ist außerdem als Artifact veröffentlicht: https://claude.ai/artifact/1KAU7UdmgMv5DZohRjtBCg — für ein Update die
Fassung ohne eigenes Gerüst erzeugen (zweites Argument von `baue_deck.py`) und mit `url` neu veröffentlichen (vorher lesen).

## Technik der Folien

- Karten: `<div data-node="E_a"></div>` rendert eine Karte aus `NODES` (in `zielbild-seite.html`); Graphen über
  `{{allg}}` / `{{ip}}`. Kanten werden per JS gezeichnet; rückläufige Kanten haben Bahnen (`{ly, ex, nx}` in `GRAPHS`).
- Schrittweiser Aufbau: Klasse `fragment`. Horizontal = Geschichte, vertikal = „genauer hinsehen“.
- Stile für neue Folien gehören in `deck_css` in `baue_deck.py`. Farben: Wunsch/Command blau, Fakt/Event grün, Entscheider/
  Aggregat türkis, Funktion orange, Ansicht/Projektion grün, Frage/Query violett, Quelle hellgrün, Client blau, Akteur rosa.

## Vorgaben von Tobi (wichtig)

- **Menschlich und klar, wenig Text.** Überschriften sagen, was für Menschen passiert oder was es bringt. Kein Jargon in
  Überschriften (nicht: Signatur, Graph, Rumpf, Kontextfenster, Patch, Commit, Projektion, Analyzer). Fachwörter nur klein als
  „Fachwort: …“ und auf den Code-Detailfolien. Wörter: KI (nicht LLM), Stück, Baustein, Auftrag, Absprache, Protokoll,
  Hauptfläche, Testumgebung. Verworfen als schräg: Spur, Leitplanken, Spielwiese, Kästchen, grober Umriss, Bühne.
- Erzählen statt aufzählen: herleiten, ein Satz + ein Bild je Folie.
- Nur die ImagePair-Domäne als Beispiel. Echte Namen aus dem Code (`NimmBildAuf`, `IBildaufbereitung`, `Bildeingang`,
  `IBildpaarBeurteilung`, `ImagePairProjektion`, `ImagePairReader`, `IArbeitsplatz`, `IKlassifikationsWorker` …).
- Ehrlich bleiben: Ausgedachtes als Beispiel kennzeichnen. Ausgedacht sind: „Bildpaar 4711“, Uhrzeiten,
  `PruefeVollstaendigkeit`/`PaarUnvollstaendig`, die Protokoll-Dauern. Zielbild statt Ist: Werkbank-Oberfläche (im Umbau
  zurückgestellt), „mindestens Stand v“, Nachlesen nach Wiederverbinden, Command-Id vor dem Senden (Plan Phase 7).
- Token-Zahlen stammen aus `.llm-kontext/` und `.llm-kontext/protokoll.jsonl` (Schätzung Zeichen ÷ 3,3, wie die Werkbank):
  ≈ 11 000 je Aufruf, davon ≈ 8 400 Übersicht (für alle 282 Stücke gleich), ≈ 2 600 je Stück; Domänen-Code ≈ 70 000,
  ganzes Repo ≈ 1,27 Mio.
- KI-Logik so, wie sie im Code steht: `Werkbank/LlmKonsole.cs` (zustandslose Runden, leerer Ordner, `claude -p` über das Abo,
  probeweise bauen mit echten Generatoren/Analyzern, nur neue Fehler zählen, max. 3 Runden, `AUSSERHALB: braucht …`,
  Protokoll), 🤖-Knoten im Board (`Werkbank/wwwroot/editor/editor.js`, `GraphExtractor.Kern/LlmKontext.cs`).
- Nach jeder Änderung bauen, rendern, selbst ansehen, dann erst melden. Nicht committen und nicht pushen ohne Tobis Wort.
  Im Arbeitsbaum liegen parallel Phase-4-Änderungen einer anderen Session — nichts davon anfassen.

## Kapitel „Was aus unseren Zeilen entsteht“ (echter generierter Code)

Steht am Ende vor dem Schluss: eine Übersicht „aus … wird …“ und je Paar eine Folie (von uns geschrieben ↔ generiert).
**Keine absoluten Zahlen** (Tobis Vorgabe), nur Beispiele. Paare: Aggregat → Actor im Cluster (`AggregateActors.g.cs`,
`GeneratedAggregates.g.cs`), Entscheider → Routing + Verdrahtung (`GeneratedCommandRouting.g.cs`, `ImagePairAggregateHandler.g.cs`),
drei `Auf`-Zeilen → Weckplan und Arbeitsschritt → Bauplan (`GeneratedFunktionsBausteine.g.cs`), Ansicht → Verteilung
(`ImagePairProjektion.Subscriber.Dispatch.g.cs`), Quelle → Treiber/Fabrik (`GeneratedQuellen.g.cs`), Dienst + Konfig → Bindung
(`GeneratedDienste.g.cs`), Rolle → Rechte (`GeneratedAkteurRechte.g.cs`), Absprache → Server-Vertrag (`GeneratedClientVertraege.g.cs`)
und Client-Verdrahtung (`GeneratedWiring.g.cs` im Blazor-Modul), C#-Schnittstelle → Python-Basis und Protobuf, Modul-Ordner →
`GeneratedModuleRegistry`. Erzeugt aus dem Arbeitsstand vom 27.10.2026: Arbeitsbaum ohne `bin/obj/.git` in einen Scratch-Ordner
kopiert, dort `dotnet build <Projekt> -p:EmitCompilerGeneratedFiles=true` (Domain.Laufzeit und Domain.Client.Modules.Blazor),
Generate unter `*/obj/Debug/net9.0/generated/`. Ausschnitte sind gekürzt um Namensräume.

## Offen / naheliegende nächste Schritte

- Kurze Fassung `geschichte.html` genauso in Alltagssprache bringen oder auf die volle Fassung umstellen.
- Port-Beschriftungen der technischen Karten (z. B. „Kenntnis“, „trägt“) bewusst technisch gelassen — bei Bedarf entschärfen.
- `docs/README.md` beschreibt den Ordner; nach größeren Umbauten nachziehen.
