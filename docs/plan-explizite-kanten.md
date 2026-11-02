# Plan: Ausdrückliche Kanten in Graphen

Stand 2026-11-02, mit Tobi entschieden. Maßstab für den Umbau auf dem Branch `funktionsgraphen` (nach dem Umbau „Commands und Funktionsgraphen“).
Nach der Abnahme wird dieses Dokument gelöscht (Git-Verlauf); was bleibt, steht dann in docs/04 §4.4, docs/10 und CLAUDE.md.

## Warum

Heute folgen die Kanten innerhalb eines Graphen aus den Typen: gibt ein Knoten `T` aus, bekommt es jeder Knoten desselben Graphen, der `T` nimmt; was
innen keiner nimmt, verlässt den Graphen. Das trägt den strukturierten Kern (Folge, Gabel, Join, Wahl, Rennen, Je und Sammeln, Graph im Graphen), aber
an drei Stellen nicht — alle mit derselben Ursache, **ein Typ bestimmt eine Kante, und zwar für den ganzen Graphen**:

1. **Dieselbe Funktion zweimal geht nicht.** Eine Vorschau mit 512 und ein Thumbnail mit 128 Pixeln aus demselben Bild: zwei `Bildverkleinerung`
   nähmen beide `BildEingelesen` und gäben beide `BildVerkleinert` aus, dahinter unterscheidet sie niemand. Der Build verbietet es heute
   („je Funktion ein Knoten“); der Ausweg über Typargumente oder Wrapper-Typen bläht die Typen auf, nur damit das Routing eindeutig wird.
2. **Keine Filterketten mit gleichem Typ** (`Bild → Bild → Bild`: Normalisieren, Schärfen, Normalisieren) — für das Typ-Routing ein Kreis.
3. **Fernwirkung.** Ein neuer Knoten, der zufällig einen Typ nimmt, bekommt jede Nachricht dieses Typs zusätzlich (eine ungewollte Gabel), und der Typ
   verlässt den Graphen nicht mehr. Für Gesten und LLM ist das gefährlich: der Code baut, der Fluss ist ein anderer.

Generics als Verdrahtung (Typargument als Kanal) wurden verworfen: die Kante stünde in den Signaturen der Funktionen, nicht im Graphen, und jede
wiederverwendbare Funktion bräuchte Kanal-Typparameter ohne fachliche Bedeutung. `nameof` zeigt die Kante dort, wo der Graph steht.

**Das kehrt eine frühere Entscheidung um:** „Kanten nie schreiben, sie folgen aus den Typen“ (CLAUDE.md, docs/10 E8, E13, §10.3.7). Typ-Routing
bleibt ZWISCHEN den Bausteinen (Decide → Kopf eines Graphen oder Akteur, Leitung → Kopf, Ausgang eines Graphen → Kopf eines anderen; genau ein
Empfänger, Invariante 3). INNERHALB eines Graphen ist jede Kante ausdrücklich.

## Entscheidungen

1. **Jede innere Kante steht am nehmenden Knoten:** `[Von(nameof(A), nameof(B), …)]` an einem Parameter des Graph-Records nennt die Knoten desselben
   Graphen, von denen dieser Knoten etwas bekommt. Innerhalb eines Graphen wird keine Kante mehr aus Typen abgeleitet.
2. **Jeder Kopf steht ausdrücklich da:** `[Kopf]` an einem Knoten, der von außerhalb des Graphen nimmt. Jeder Knoten trägt `[Kopf]`, `[Von(…)]` oder
   beides (ein Knoten mit zwei `Auf`, eines von außen, eines von innen). Ein Knoten ohne beides ist ein Fehler — Ausnahme: ein Quellknoten (eine
   Funktion mit `Lausche`) nimmt nichts und trägt keins von beiden. Je Eingangstyp des Graphen genau ein Knoten mit `[Kopf]`, der ihn nimmt.
3. **Quelle → Port über den Typ.** Eine genannte Quelle speist die Ports des Knotens, deren Typ sie ausgibt. Jede genannte Quelle speist mindestens
   einen Port. Ein Port eines Knotens mit `Von` wird von mindestens einer genannten Quelle gespeist — oder, wenn der Knoten `[Kopf]` trägt und keine
   genannte Quelle seinen Typ ausgibt, von außen (dann ist sein Typ ein Eingang des Graphen). Ein `[Kopf]`, an dem nichts von außen ankommt, ist ein
   Fehler.
4. **Ein Typ höchstens einmal je `Auf`.** `Auf(Bild links, Bild rechts)` ist ein Fehler mit Vorschlag (eigene Typen `BildLinks`, `BildRechts`) — so
   hängt die Zuordnung nie an einer Reihenfolge.
5. **Mehrere Quellen je Port** sind erlaubt, wenn die Form es trägt; die Vielfachheit bleibt, wie sie ist, und läuft auf den ausdrücklichen Kanten:
   `T` hinter einer Wahl → `T?` oder `OneOf`, `OneOf` als Rennen, `IReadOnlyList<T>` sammelt nach einem Strom.
6. **Ausgang:** Was ein Knoten ausgibt und kein Knoten über `Von` von ihm nimmt, verlässt den Graphen — ein Command mit Ziel an sein Aggregat, ein
   Command ohne Ziel an seinen einen Empfänger draußen (Kopf genau eines Graphen oder Akteur, wie heute geprüft), ein Hinweis an die hörenden Akteure.
   Nimmt ein Knoten von einer Quelle nur einen Teil ihrer Ausgaben, verlassen die übrigen den Graphen, sofern kein anderer Knoten sie nimmt.
7. **Graph im Graphen:** `[Von(nameof(X))] Innerer I` speist die `[Kopf]`-Knoten des inneren Graphen über die Typen; `[Kopf] Innerer I` macht seine
   Köpfe zu Köpfen des äußeren. `Von(nameof(I))` an einem anderen Knoten meint die Ausgänge des inneren Graphen (seine Signatur).
8. **Nur `nameof`:** jedes Argument von `Von` ist `nameof` eines anderen Parameters desselben Records — kein String-Literal, nicht der Knoten selbst,
   kein Name doppelt. So bleibt es bei „nie ein handgebauter Identitäts-String“ (Invariante 3); ein Umbenennen zieht der Compiler nach.
9. **Kreisfrei über Knoten:** die Kreis-Prüfung läuft auf den ausdrücklichen Kanten; Ketten mit gleichem Typ hinein und hinaus sind kein Kreis mehr.
10. **Eine Funktion darf mehrfach im Graphen stehen;** die Regel „je Funktion ein Knoten“ entfällt. Generische Funktionen bleiben erlaubt, wenn das
    Typargument fachlich etwas bedeutet (Sonde: `Glaettung<Kurzfenster>`), sie sind aber kein Mittel der Verdrahtung.
11. **Join nur innerhalb eines Laufs** (unverändert): die Ports eines `Auf` mit mehr als einem Port kommen alle von genannten Quellen; zwei Eingänge von
    außen treffen sich nie. Wer zwei Eingänge von außen zusammenbringen muss, sammelt sie in einem Vorgangs-Aggregat, dessen Decide den
    zusammengeführten Command ohne Ziel an den Kopf eines Graphen gibt.
12. **Die Kanten sind Code-Fakten** (W2): Generator, Analyzer, Extractor und Editor lesen sie aus `Von` und `Kopf` (eine Lesart: `Graphform`), der
    Scaffolder schreibt sie, die Geste „verbinden“ setzt bzw. entfernt ein `Von`, „Kopf“ setzt bzw. entfernt `[Kopf]`.

## Beispiel

```csharp
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Einlesen), nameof(Kontrast))] Bildeingang Eingang) : IGraph;

public sealed record Linienimport(
    Dateiwaechter Eingang,
    [Von(nameof(Eingang))] Bildaufbereitung Aufbereitung) : IGraph;

public sealed record Datensatzpflege(
    [Kopf] RangeAufloesung Aufloesung,
    [Kopf] MitgliederEinfrieren Einfrieren) : IGraph;
```

`nameof` auf einen Nachbar-Parameter im Attribut eines Record-Parameters kompiliert (net9.0, geprüft 2026-11-02).

## Regeln mit Build-Gegenstück (W5)

Alle Formfehler laufen weiter über `Graphform.Lies(…).Fehler` und `CQRS115` (`Domain.SourceGeneration/GraphAnalyzer.cs`), je Regel ein Test in
`Infrastructure.Pruefstand.Tests/Analyzers/GraphAnalyzerTests.cs` und ein Eintrag in `DomainEditor/Grammatik.cs`:

| Regel | Fehler (Kurzform) |
|---|---|
| GR-GRAPH (neu gefasst) | Knoten ohne `Kopf` und ohne `Von` (außer Quelle); `Von` nennt keinen Knoten des Records, sich selbst oder einen Namen doppelt; Argument kein `nameof` |
| GR-KANTE (neu gefasst) | genannte Quelle speist keinen Port; Port ohne Quelle und nicht von außen; Kreis über Knoten |
| GR-KOPF (neu) | je Eingangstyp nicht genau ein `[Kopf]`; `[Kopf]`, an dem nichts von außen ankommt |
| GR-JOIN (ergänzt) | ein Typ zweimal in einem `Auf` (mit Vorschlag); Join halb von außen |

Die bisherigen Fehler „je Funktion ein Knoten“ und „je Eingangstyp genau ein Kopf … was mehrere brauchen, gibt ein Kopf weiter“ entfallen bzw. gehen
in GR-KOPF auf. Ebenen, Vielfachheit, Rennen und Sammeln bleiben, wie sie sind.

## Betroffen

- **Abstractions/Graph.cs:** `VonAttribute(params string[] knoten)` und `KopfAttribute`, beide `AttributeTargets.Parameter`.
- **Infrastructure.SourceGeneration/Graphform.cs:** Kanten aus `Von`/`Kopf` statt aus passenden Typen (`Lies`, die Schleife „Kanten“), Köpfe aus
  `[Kopf]`, Ausgänge nach Entscheidung 6, Signatur der Schachtel nach Entscheidung 7, die neuen Prüfungen; das `nameof` über
  `AttributeData.ApplicationSyntaxReference`. `GraphGenerator`, `FunktionsBausteinGenerator`, `GesamtbildAnalyzer` und die Laufzeit (`Lauf`,
  `GraphActor`) lesen nur die fertigen Kanten — prüfen, nicht umbauen.
- **Domäne und Prüfdomäne:** jeder `Graphen.cs` (`Domain.Pipeline/*`, `Pruefdomaene.Pipeline/*`) bekommt `Kopf`/`Von`.
- **Tests mit Graphen im Quelltext:** `GraphAnalyzerTests`, `GesamtbildAnalyzerTests`, `LageAnalyzerTests`, `FunktionsBausteinGeneratorTests`,
  `GestenTests`; neu: Vorschau und Thumbnail aus derselben Funktion, Kette `Bild → Bild → Bild`, Knoten mit `Kopf` und `Von`, Graph im Graphen mit
  `Von`, je neuer Fehler einer.
- **Werkzeuge:** `GraphExtractor.Kern/DomainExtractor.cs` (`ReadGraph`, `GraphRaw`), `ModellMapper`, `DomainEditor/EditorModell.cs` (`Graph.Koepfe`
  und `GraphKante` aus den Attributen statt „abgeleitet“, `Graph.Knoten` mit `Von`/`Kopf`), `DomainEditor/Scaffolder.cs` (`GraphRecord` schreibt die
  Attribute, Datei-usings), Gesten (Knoten hinzufügen mit `Von`/`Kopf`, verbinden, trennen), `ParitaetsPruefung` (Fixpunkt), `GraphText`
  (`--graph`). Die Editor-Oberfläche (`Werkbank/wwwroot/editor`) wird nicht umgebaut.
- **Sonde:** die Graph-Proben in `GraphExtractor.Kern/Sonde/*.cs.txt`, `soll.txt`, `Gezeichnet.gesten.json` (Gesten „Knoten hinzufügen“ und
  „neuer Graph“ mit `Von`/`Kopf`), dazu die neuen Muster aus „Tests“.
- **Werkbank-Abnahme:** die Erwartungen zum Board des Linienimports (Köpfe, Quelle) prüfen.
- **Doku:** docs/04 §4.4, docs/10 (§10.1, Bausteintabelle, §10.3.7, E8, E13, Schluss „keine ausdrücklichen Kanten“), docs/05 (`Graphform`),
  CLAUDE.md (Graph, „Graph statt Klebecode“, „keine ausdrücklichen Kanten“).

## Schritte

Jeder Schritt baut den Bestand direkt um (keine Abwärtskompatibilität), endet mit grünem `dotnet build` und `scripts/server.sh lauf scripts/tests.sh
schnell` und ist ein eigener Commit.

1. **Lesart, Regeln, Bestand.** Attribute, `Graphform` (Kanten, Köpfe, Ausgänge, Signatur, neue Prüfungen), `CQRS115`-Texte, Grammatik (GR-GRAPH,
   GR-KANTE, GR-KOPF, GR-JOIN), alle Graphen in Domäne und Prüfdomäne, die Tests mit Graphen im Quelltext samt den neuen Fällen. Laufzeit und
   Generat müssen unverändert tragen (Integration, Prüfdomäne, Verhaltens-Netz in der schnellen Stufe).
   **Nachgezogen:** `GestenTests` und `SichtTests` (Prüfstand, schnelle Stufe) lesen die Sonden-Domäne über `Graphform` und schreiben über den
   Scaffolder — ohne `Kopf`/`Von` dort wäre Schritt 1 rot. Schritt 1 nimmt deshalb mit: die bestehenden Graph-Records der Sonde und die
   Graph-Gesten in `Gezeichnet.gesten.json` (nur die Attribute), das Lesen und Schreiben von `Kopf`/`Von` am Knoten (`GraphRaw`, `GraphKnoten` im
   `EditorModell`, `ModellMapper`, `Scaffolder.GraphRecord`, `DateiAbgleich`; ein entfernter Knoten nimmt seine `Von` mit).
2. **Werkzeuge.** Gesten „verbinden“, „trennen“ und „Kopf“, Parität, `--graph`; `--check` grün.
3. **Sonde.** Die neuen Muster (Funktion zweimal, Kette gleichen Typs, Knoten mit `Kopf` und `Von`, Graph im Graphen mit `Von`), `soll.txt`, Gesten;
   `--sonde` und `--grammatik` grün.
4. **Doku und Abnahme.** docs/04, docs/05, docs/10, CLAUDE.md; `scripts/server.sh lauf scripts/tests.sh voll` grün; dieses Dokument in eigenem
   Commit löschen (Verweis in docs/README.md entfernen, CLAUDE.md „Gelöschte Dokumente“ ergänzen).

Eine Messung ist nicht nötig: die Laufzeit liest dieselben Kanten-Tabellen wie heute.
