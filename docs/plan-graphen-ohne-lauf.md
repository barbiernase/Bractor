# Plan: Graphen ohne Lauf-Steuerung

Stand 2026-11-03, Vorschlag nach dem Gespräch mit Tobi. Maßstab für den Umbau auf dem Branch `funktionsgraphen` (nach „Ausdrückliche Kanten“).
Nach der Abnahme wird dieses Dokument gelöscht (Git-Verlauf); was bleibt, steht dann in docs/04 §4.4, docs/10 und CLAUDE.md.

## Warum

Ein Graph soll eine schnelle Rechenschicht sein: Nachrichten laufen von Schritt zu Schritt, jeder Schritt rechnet, sobald etwas bei ihm ankommt, und
schickt sein Ergebnis weiter. Wohin, steht fest im Graphen und wird beim Bauen abgeleitet. Im Editor legt man nur fest, welche Nachrichten von wo
nach wo laufen; was ein Schritt damit macht, steht in seinem Code.

Heute ist es anders. Neben dem Actor-Framework gibt es eine eigene Steuerung (`Lauf`, der Graph-Actor als Koordinator, rund 800 Zeilen): Sie begleitet
jedes Bild als „Lauf“ durch den Graphen, führt einen Laufzettel und entscheidet, wann welcher Schritt rechnen darf. Gebraucht wird der Laufzettel nur
für Sonderformen der Eingänge: Zusammenführen (ein Schritt wartet auf zwei Nachrichten), „höchstens eine“ (`T?`, warten, bis feststeht, dass keine
kommt), Einsammeln (`IReadOnlyList<T>`, warten, bis alle Kacheln zurück sind), Rennen und Zeitlimit. Der Preis:

1. **Ein Schritt rechnet je Lauf höchstens einmal.** Kommt ein zweiter Wert an, ist sein Platz auf dem Laufzettel belegt, und der Wert wird still
   verworfen. Daraus wurde die Regel „zwei Werte an einem Schritt sind ein Fehler“ (Commits 1a1b768, 25d5b92) — eine Schwäche der Umsetzung, die
   zur Vorschrift für den Modellierer wurde.
2. **Ein versteckter Workflow-Motor.** Genau das wollten wir nie (kein Dirigent, keine Saga). Jeder Schritt meldet sich beim Koordinator zurück,
   bevor es weitergeht — eine zentrale Stelle je Instanz und Rechner.
3. **Synchronisation steckt in Parameter-Typen.** Ob ein Schritt wartet, sammelt oder rennt, liest man nur an `T?`, `IReadOnlyList` oder `OneOf`
   im Parameter ab; was dabei geschieht, macht die Laufzeit unsichtbar.

## Das Bild danach

- Jeder Schritt (Knoten) einer Instanz ist ein Actor mit fester **Adresse**. Er nimmt eine Nachricht, rechnet, und für jedes Ergebnis schlägt er in
  einer beim Bauen erzeugten **Wegtabelle** nach, wohin es geht. Gibt er ein `OneOf` zurück, zählt der Fall, der tatsächlich kam: sein Typ ist der
  Schlüssel in der Tabelle, dort stehen die Adressen der Empfänger. Mehr Logik gibt es auf dem Weg nicht.
- Kommen zwei Nachrichten an, rechnet der Schritt zweimal. Es gibt keine Fallunterscheidung und keinen verworfenen Wert.
- Zusammenführen, Einsammeln und Warten stehen im Code des Schritts oder in einem Aggregat, nicht in der Laufzeit.
- Je Kante steht im Graphen, ob die Nachricht **gesichert** oder **verlierbar** zugestellt wird. Beides leistet das Actor-Framework.

## Entscheidungen

1. **Die Adresse eines Schritts = Instanz + Knotenpfad + Rechner.** Jede Instanz eines Graphen hat einen Namen, der ihr Wert in der Umgebung ist
   (heute schon: eine Datei je Instanz, `umgebungen/<name>/graphen/<instanz>.json`). Aus Instanz und Knotenpfad folgt der **Ort**
   `<instanz>/<knoten>` (heute schon der Schlüssel der Command-Ids und der Spur), daraus der Actor-Name `funktionen/<instanz>/<knoten>`. Welche
   Rechner eine Instanz halten, steht wie heute im Gossip-Zustand des Clusters; der Absender nimmt den eigenen Rechner, wenn er die Instanz hält,
   sonst reihum. Ein Schritt mit gesichertem Zustand läuft im Cluster auf genau einem Rechner (die Sperre wie heute). Wo ein Schritt rechnet
   (Host, Extern, Hand), bleibt Konfig je Knoten. Keine virtuellen Actors.
2. **Die Wegtabelle ist generiert:** je Instanz-unabhängigem Graphen (Knoten, Ausgabetyp) → Liste (Empfängerknoten, Zustellung). Sie kommt aus
   `Graphform.Lies` (Kanten aus `[Von]`, Köpfe aus `[Kopf]`), wie heute die Kanten. Zur Laufzeit wird nur nachgeschlagen.
3. **Ein `Auf` nimmt genau eine Nachricht.** Die Eingangsformen `T?`, `OneOf<…>` als Rennen, `IReadOnlyList<T>` und das Zeitlimit eines Knotens
   entfallen; ein `Auf` mit mehreren Nachrichten-Parametern ist ein Build-Fehler mit Hinweis auf die Wege unten. Eine Funktion darf weiter mehrere
   `Auf` haben (je Nachrichtentyp eines). Die Ausgaben bleiben: ein Typ, ein `OneOf` (jeder Fall hat seinen Weg) und ein Strom (jedes Element geht
   für sich hinaus).
4. **Zustellung je Kante:** `[Von(nameof(A))]` ist gesichert, `[Von(nameof(A), Verlierbar = true)]` verlierbar. Verschiedene Zustellungen an
   einem Knoten stehen in eigenen `[Von]`. Gesichert ist der Standard, weil ein vergessenes „verlierbar“ nur Tempo kostet, ein vergessenes
   „gesichert“ aber still Daten.
5. **Was „gesichert“ heißt.** Der Absender fragt den Empfänger über den Boten (`FrageAsync` mit Frist) und wiederholt mit derselben Kennung, bis
   eine Antwort kommt. Der Empfänger antwortet „fertig“ erst, wenn er gerechnet hat und seine eigenen gesicherten Ausgaben ihrerseits „fertig“
   gemeldet haben. So reicht die Rückmeldung wie bei verschachtelten Anrufen bis zum Kopf, ohne dass jemand einen Gesamtüberblick führt; jeder
   Schritt wartet nur auf seine eigenen Anrufe. Über einen Absturz hinweg trägt das nicht das Actor-Framework, sondern der durable Kopf: die Zeile in
   `cqrs_eingaenge` wird erst mit dem „fertig“ des ersten Schritts ausgetragen. Fehlt es, stellt der Zusteller den Eingang nach der Frist erneut zu,
   die Kette rechnet noch einmal, und weil die Kennungen gleich bleiben, verwirft die Inbox des Aggregats jeden doppelten Command. Antwortet ein
   Schritt „später“ (Warteschlange voll), wartet der Absender und fragt erneut; der Rückstau reicht so bis zum Kopf.
6. **Was „verlierbar“ heißt.** Der Absender meldet (`Melde`), ohne Antwort und ohne Wiederholung. Was dahinter geschieht, hält die Rückmeldung zum
   Kopf nicht auf; geht die Nachricht verloren, merkt es niemand. Für Vorschauen, Zwischenstände, alles, was beim nächsten Mal ohnehin neu kommt.
7. **Kennungen aus der Nachricht statt aus dem Lauf.** Jede Nachricht trägt ihre Kennung, abgeleitet aus der Kennung der Nachricht, aus der sie
   entstand, dem Ort ihres Erzeugers und ihrer Stelle in dessen Ausgabe (`Kettenvertrag.Ausgabe`, heute schon da). Command-Ids an Aggregate genauso
   (`Kettenvertrag.Befehl`). Wird eine Kette nach einem Absturz neu gerechnet, entstehen dieselben Kennungen, unabhängig von der Reihenfolge, in
   der Nachrichten ankommen.
8. **Zustand.** `IZustand<T>` bleibt. Gesichert wird nach jedem Aufruf, der „fertig“ ist, zusammen mit den Kennungen der zuletzt verarbeiteten
   Nachrichten. Kommt eine davon erneut (Wiederholung nach Frist, Neustart am Kopf), antwortet der Schritt „fertig“, ohne neu zu rechnen; so zählt
   ein Zähler auch nach einem Absturz nichts doppelt. Die heutige Regel „gesichert wird der letzte, dessen Lauf und alle früheren fertig sind“
   entfällt mit dem Lauf.
9. **Fehler.** Scheitert ein Schritt nach seinen Wiederholungen, antwortet er auf einer gesicherten Kante „gescheitert“; das reicht bis zum Kopf,
   der die Zeile parkt (wie heute) und den Akteuren der Instanz einen Hinweis gibt. Auf einer verlierbaren Kante bleibt nur die Spur.
10. **Zusammenführen und Einsammeln im Logik-Code.** Drei Wege, in dieser Reihenfolge zu prüfen:
    - *Die Nachricht trägt mit, was danach gebraucht wird.* Meist ist kein Zusammenführen nötig, wenn ein Schritt das, was er bekam, in seiner
      Ausgabe weiterreicht.
    - *Ein Schritt mit Zustand* nimmt beide Nachrichten in zwei `Auf`, merkt sich die erste bis zur zweiten (schnell, für Kurzlebiges).
    - *Ein Aggregat* zählt oder führt zusammen, wenn das Ergebnis fachlich zählt (Haltung B: Fan-in über ein Zähl-Aggregat, wie bei Abläufen).
11. **Unverändert:** `[Kopf]`, `[Von]` als einzige Kanten, Graph im Graphen, Instanzen, kreisfrei über Knoten, Quellen, Werkstatt und Arbeiter
    draußen, die Wege zwischen Bausteinen (Typ-Routing mit genau einem Empfänger), die Spur (`knoten:<Instanz>/<Knotenpfad>`).
12. **Zurückgenommen:** die Regel „zwei Werte an einem T-Port“ aus 1a1b768 und 25d5b92. Sie hat mit dem Laufzettel keinen Grund mehr.

## Beispiel

Die Bildaufbereitung führt heute das eingelesene Bild mit seiner kontrastierten Vorschau zusammen:

```csharp
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Einlesen), nameof(Kontrast))] Bildeingang Eingang) : IGraph;

public OneOf<NimmBildAuf, DateinameUnbekannt> Auf(BildEingelesen bild, VorschauErstellt vorschau)   // wartet auf beide
```

Danach trägt die Vorschau das eingelesene Bild mit; der Eingang braucht nur noch eine Nachricht, und der Join verschwindet:

```csharp
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Kontrast))] Bildeingang Eingang) : IGraph;

public OneOf<NimmBildAuf, DateinameUnbekannt> Auf(VorschauErstellt vorschau)   // vorschau.Bild ist das eingelesene Bild
```

Gibt `Bildeingang` `NimmBildAuf` zurück, geht der Command an das Paar-Aggregat; gibt er `DateinameUnbekannt` zurück, geht der Hinweis an die
Akteure, die ihn hören. Beides steht in der Wegtabelle. Der Verteilabschluss der Prüfdomäne, der heute mit `IReadOnlyList<ZielGebucht>` alle
gebuchten Ziele einsammelt, wird zum Zähl-Aggregat: die Verteilung kennt ihre Zahl der Ziele und schließt ab, wenn alle gebucht sind.

## Regeln mit Build-Gegenstück (W5)

| Regel | Gegenstück |
|---|---|
| Ein `Auf` nimmt genau eine Nachricht; `T?`, `IReadOnlyList<T>`, `OneOf` als Eingang sind Fehler mit Hinweis auf Entscheidung 10 | CQRS100 (Funktionsform) |
| `Verlierbar` nur als benannter Wert von `[Von]`; eine Quelle höchstens in einem `[Von]` je Knoten | CQRS115 (Graphform) |
| Die Wegtabelle deckt jede Ausgabe jedes Knotens ab (innen ein Empfänger oder hinaus) | Generator + Boot-Guard |

Grammatik: GR-JOIN entfällt (bzw. wird „GR-EINGANG: ein Auf, eine Nachricht“), GR-KANTE nimmt die Zustellung auf.

## Betroffen

- **Laufzeit** (`Infrastructure/Funktionen/`): `Lauf.cs` entfällt; `GraphActor` wird vom Koordinator zum Halter der Schritte einer Instanz (Start,
  Sperre, Gossip) ohne Laufzettel; `FunktionsParent` nimmt Nachrichten direkt, rechnet, schlägt die Wege nach, fragt bzw. meldet weiter und
  antwortet seinem Absender. `Graph.cs`: `Portform`, `PortPlan.Erzeuger`, Ebenen und Sammler entfallen, dazu die Wegtabelle.
- **Generatoren und Analyzer:** `Graphform` (ohne Ebenen und Vielfachheit, mit Zustellung je Kante), `Funktionsform` (ein Eingang je `Auf`),
  `GraphGenerator` (Wegtabelle), `FunktionsBausteinGenerator` (ein Port je Aufruf).
- **Werkzeuge:** Extractor, Modell (`GraphKnoten.Von` mit Zustellung), Scaffolder, Abgleich, Parität, `--graph`, Sonde und `soll.txt`.
- **Bestand:** `Bildeingang` und `Bildaufbereitung` (Beispiel oben), `Verteilabschluss` und die Verteilung der Prüfdomäne, die Sonde.
- **Tests:** Prüfstand (Graphform, Funktionsform, Laufzeit der Funktionen ohne Lauf), Prüfdomäne (Verteilung), Verhaltens-Netz.
- **Doku:** docs/04 §4.4 (Ports, Lauf, Zustand, offene Eingänge), docs/10 (Grammatik, Port-Tabelle, R1, E5), docs/11 (Zustellung im Graphen),
  docs/13, CLAUDE.md (Funktion vor dem Aggregat, Graph).

## Schritte

Jeder Schritt endet mit grünem Build, grüner schneller Stufe, Commit und Push.

1. **Grammatik und Bestand.** Die Regel aus 1a1b768/25d5b92 zurücknehmen. Ein Eingang je `Auf` als Build-Regel; `Bildeingang` und Verteilabschluss
   umbauen, die Sonde ebenso. Die alte Laufzeit läuft danach nur noch mit einfachen Eingängen und bleibt grün.
2. **Laufzeit.** Wegtabelle generieren; Schritte nehmen Nachrichten direkt und schlagen ihre Wege nach; gesichert über Frage und Antwort bis zum
   Kopf, verlierbar über Melde; Kennungen aus der Nachricht; Zustand mit Kennungs-Gedächtnis; Fehler und Rückstau. `Lauf` und der Laufzettel
   entfallen. Tests für: zwei Werte an einem Schritt (zweimal gerechnet), Absturz mitten in der Kette (neu gerechnet, nichts doppelt), verlierbare
   Kante (hält den Kopf nicht auf), Rückstau.
3. **Werkzeuge.** Zustellung je Kante lesen und schreiben (Extractor, Modell, Scaffolder, Abgleich, Parität, `--graph`); `--check` grün.
4. **Sonde, Doku, Abschluss.** Sonde mit einer verlierbaren Kante, `--sonde` und `--grammatik` grün; Doku; volle Stufe grün; dieses Dokument
   löschen (eigener Commit, Eintrag in docs/README.md entfernen, in CLAUDE.md unter „Gelöschte Dokumente“).
