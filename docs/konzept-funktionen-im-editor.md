# Konzept: Funktionen und Graphen im Editor, losgelöst von der Domäne

Stand 2026-11-07, zweite Fassung nach Tobis Rückmeldung. **Stufe 1 (Katalog-Modal) und Stufe 2 (Graph-Modal im Katalog) sind umgesetzt** (docs/10 §10.9 „Kataloge“, „Graphen im Katalog“); offen aus ihr: Zustand (`IZustand<T>`) und „lauscht“ im Modal. Baut auf [plan-katalog-und-graphen.md](plan-katalog-und-graphen.md) auf (Schritte 1–5
umgesetzt) und ersetzt dessen Schritte 6 und 7, soweit sie den Editor betreffen. Keine Abwärtskompatibilität.

## 1. Der Weg in drei Stufen

Man baut von unten nach oben, und jede Stufe hat genau einen Ort im Editor:

1. **Funktionen im Katalog anlegen** (Katalog-Modal). Je Funktion ein Input-Command, die Funktion selbst und ihre Output-Commands, jeweils neu
   oder aus einem Katalog gewählt. Jeder Command, der dabei entsteht, landet in der Command-Liste des Katalogs.
2. **Funktionen zu einem Graphen zusammenklicken** (Graph-Modal). Man startet mit einer Funktion und hängt an jeden Output eine Funktion, deren
   Input dazu passt. So entsteht eine Kette.
3. **Den Graphen an eine Domäne anbinden** (Hauptansicht). Jede Domäne hat einen Knopf „＋ Graph“. Der Graph gehört damit eindeutig zu ihr,
   erscheint in ihrem Band vollständig aufgeklappt, mit seinen Outputs, und an jeden Output setzt man einen Übersetzungsknoten in einen Command
   eines Aggregats dieser Domäne.

Stufe 1 und 2 kennen keine Domäne, Stufe 3 ist die einzige Stelle, an der Funktionswelt und Domäne zusammenkommen.

## 2. Stufe 1: Das Katalog-Modal

Geöffnet über „📚 Kataloge“ in der Kopfleiste. Links die Kataloge, darunter immer der **allgemeine Katalog** (s. u.). In der Mitte zwei Listen des
gewählten Katalogs: **Funktionen** und **Commands**; dazu Graphen (Stufe 2) und, eingeklappt, Konfig und Technik.

**Eine Funktion anlegen.** „＋ Funktion“ fragt nach dem Namen und zeigt die Funktion als drei Knoten in einer Reihe:

```
 [ Input-Command ] ──▶ [ ƒ Funktion ] ──▶ [ Output-Command ]
                                     ├─▶ [ Output-Command ]     mehrere Outputs = ein OneOf, je Fall ein Port
                                     └─▶ [ ＋ Output ]
```

- **Input:** „＋ Input“ bietet „neuer Command“ (Name und Felder) oder einen vorhandenen aus der Command-Liste dieses Katalogs bzw. des
  allgemeinen Katalogs. Ein zweiter Input ist ein zweites `Auf` derselben Funktion mit eigenen Outputs, eine eigene Zeile.
- **Output:** genauso, „neuer Command“ oder ein vorhandener. Ein Output ist ein Port, mehrere sind ein `OneOf` mit je einem Port pro Fall. Ein
  Schalter am Output macht daraus einen Strom (0..n). Statt eines Commands kann ein Output auch ein **Hinweis** sein (flüchtig, an Akteure).
- **Die Funktion selbst:** am Funktionsknoten Konfig, Technik, Zustand, „lauscht“ (Quelle) und ✎ für den Rumpf. Doppelklick öffnet bei Bedarf das
  ausführliche Funktions-Modal von heute.
- **Commands bearbeiten:** in der Command-Liste (✎ Felder, ✕ mit Löschplan, „verwendet von“). Ein Command, den man hier ändert, ändert sich für
  alle Funktionen, die ihn nehmen oder geben.

Die Command-Liste ist der Kern der Verkettung: Wer beim Anlegen einer Funktion einen vorhandenen Command als Input wählt, macht sie im Graph-Modal
automatisch zum passenden Nachfolger jeder Funktion, die diesen Command ausgibt.

**Der allgemeine Katalog** ist ein Katalog wie jeder andere (`Allgemein.Katalog`), nur dass alle anderen Kataloge ihn nutzen dürfen. Er hält
Commands, die mehrere Kataloge teilen („ein Bild liegt vor“, „eine Datei ist da“), und entsteht beim ersten Gebrauch. Wählt man einen Command aus
ihm, setzt der Editor die Projektreferenz. Die Quell-Commands des Frameworks (`DateiGefunden`, `Takt`, `Aufruf`) erscheinen dort mit, nur lesend.

Im Code ändert sich nichts: eine Funktion ist `sealed class X : IFunktion` mit `Auf(Input) → Output | OneOf<…>`, die Commands sind
`record … : ICommand` in `Commands.cs` des Katalogs.

## 3. Stufe 2: Das Graph-Modal

Geöffnet über „＋ Graph“ im Katalog-Modal oder per Doppelklick auf einen Graphen. Es arbeitet nur mit Funktionen aus Katalogen.

- **Start:** „＋ Graph“ fragt nach dem Namen und der ersten Funktion. Der Graph entsteht mit ihr als Kopf (`[Kopf]`) und baut sofort.
- **Verketten:** jeder Output-Port der Funktion (jeder Fall eines `OneOf`) hat ein „＋“. Es zeigt die Funktionen aller nutzbaren Kataloge, deren
  Input genau dieser Command ist, und gestrichelt „neue Funktion mit diesem Input“ (öffnet Stufe 1 mit dem Input schon gesetzt, danach zurück).
  Ein Klick setzt den Knoten ein (`[Von(nameof(…))]` am Nachfolger). Gibt eine Funktion im Graphen den Typ schon aus, steht sie mit in der Liste.
- **Outputs des Graphen:** was kein Knoten weiter nimmt, steht am rechten Rand. Das ist die Signatur, die Stufe 3 übersetzt. Ein offener Output
  ist hier kein Fehler.
- Kanten gesichert ⇄ verlierbar und lösen per Klick, ✕ am Knoten, ein Graph als Knoten ist eine Schachtel (Doppelklick eine Ebene tiefer).
- Kein Client, kein Aggregat, keine Übersetzung, keine Instanzen in diesem Modal.

Code: `record X(…) : IGraph` in `Graphen.cs` des Katalogs, wie heute.

## 4. Stufe 3: Die Hauptansicht

Die Fläche bleibt in ihren Spalten, die Domänen bleiben Bänder über die Spalten. Neu ist der Knopf **„＋ Graph“ an jeder Domäne** (im Kopf
ihres Bands) und die Art, wie ein angebundener Graph in diesem Band steht, links neben den Aggregaten der Domäne:

```
 Domäne Messung ──────────────────────────────────────────────────────────────────────────────────────────── ＋ Graph ──
 Eingang          Graph (vollständig, nur lesend)                  Übersetzung              Aggregate der Domäne
 ┌────────┐      ┌──────────────────────────────────────────┐                              ┌──────────────────────┐
 │ Client │─ ↦ ─▶│ ƒ Einlesen ─▶ ƒ Skalierer ─┬─ Skaliert ──│─▶ ↦ Skaliert → MeldeWert ───▶│ ▣ Messstelle         │
 └────────┘      │                            └─ Ausreißer ─│─▶ ↦ Ausreißer → Markiere ───▶│   MeldeWert          │
                 └──────────────────────────────────────────┘                              │   MarkiereAusreisser │
                                                                                           └──────────────────────┘
```

- **Die Zuordnung ist der Ort.** Ein Ablauf gehört genau der Domäne, in deren Bereich bei den Abläufen er liegt (`<Wurzel>.Pipeline/<Domäne>/
  Graphen.cs`, nach der Projektstruktur). „＋ Graph“ an einer Domäne legt ihn dort an; das Band rechnet der Server aus diesem Ort, nicht mehr aus
  den Aggregaten, an die der Graph schreibt (heute: „das Band eines Graphen sind die Aggregate, an die er schreibt“).
- **„＋ Graph“ an der Domäne** öffnet eine Auswahl: die Graphen aller Kataloge (mit ihren Outputs als Vorschau) und gestrichelt „neuer Graph“ (öffnet
  Stufe 2, danach zurück), dann der Name des Ablaufs. Läuft derselbe Katalog-Graph schon in einer anderen Domäne, sagt die Auswahl es dazu: ein
  zweiter Ablauf ist ein zweiter, eigener Lauf (eine zweite Rechnung).
- **Der Graph erscheint vollständig:** alle Funktionsknoten, ihre Kanten, rechts seine Outputs. Umstecken lässt sich hier nichts; Doppelklick auf
  einen Knoten öffnet Stufe 1, auf den Rahmen Stufe 2.
- **Outputs übersetzen:** jeder Output ohne Übersetzung ist orange und hat „＋ ↦“. Es bietet die Commands der Aggregate dieser Domäne, je Aggregat
  geordnet danach, wie viele Felder sich von selbst zuordnen (✓ alle, ⚠ n offen), und gestrichelt „neuer Command an ▣ …“ (mit den Feldern des
  Outputs, samt Decide, Event und Apply). Commands anderer Domänen bietet es nicht an (§7.5). Der Übersetzungsknoten steht dann zwischen Graph und
  Aggregat; Doppelklick öffnet die
  Feldzuordnung (links die Felder des Outputs, rechts die des Commands, „＋“ an einem offenen Feld hebt passende hervor).
- **Hinweise** unter den Outputs brauchen keine Übersetzung; sie gehen an die Akteure, die sie hören (Kante zum Client-Eingang).
- **Eingang:** links am Graphen „＋ Eingang“: ein Client (Akteur, eine Nachricht der Anwendung, die Übersetzung in den Input der ersten Funktion
  entsteht mit, der Akteur bekommt `IDarf`) oder eine Quelle (Dateiwächter, Taktgeber, Webhook).
- **Instanzen** stehen im Kopf des angebundenen Graphen (Zahl, Klick öffnet die Liste).

Im Code ist die Anbindung ein **Ablauf**: ein `IGraph` bei den Abläufen der Anwendung, im Bereich seiner Domäne. Er enthält den Katalog-Graphen
als einen Knoten, davor den Eingang, dahinter je Output eine Übersetzung — genau die Form, die das Framework seit Schritt 5 kann:

```csharp
// Pruefdomaene.Pipeline/Messung/Graphen.cs — entsteht durch „＋ Graph“ an der Domäne Messung
public sealed record Hochrechnung(
    [Kopf, Feld(nameof(Skalieren.Bezug), nameof(HochrechnungAngefordert.Stelle))] Uebersetze<HochrechnungAngefordert, Skalieren> Hinein,
    [Von(nameof(Hinein))] Skalierung Rechnen,                                       // der Graph aus dem Katalog Rechenwerk
    [Von(nameof(Rechnen)), Feld(nameof(MeldeAusreisser.AggregateId), nameof(Skaliert.Bezug))] Uebersetze<Skaliert, MeldeAusreisser> Melden) : IGraph;
```

Das Aggregat-Modal zeigt an jedem Command weiter, wer ihn schickt; „＋ Graph, der ihn schickt“ entfällt, das macht jetzt „＋ Graph“ auf der
Fläche.

## 5. Was im Framework nachzieht

- **Lage:** Funktionen, ihre Commands, Konfig und Technik liegen nur noch in Katalogen. Bei den Abläufen bleiben die Anbindungen und die
  Eingangsnachrichten der Clients. Eine Funktion bei den Abläufen meldet CQRS098.
- **Form einer Anbindung** (neue Regel): genau ein Katalog-Graph, dazu höchstens ein Eingang (Quelle oder Übersetzung) und Übersetzungen hinaus;
  jede Übersetzung hinaus zielt auf einen Command eines Aggregats der eigenen Domäne (die Domäne folgt aus dem Namespace des Ablaufs).
- **Form eines Katalog-Graphen** (neue Regel): keine Quelle, keine Übersetzung.
- **Weg fällt**, was nur Anwendungsfunktionen brauchten: Aggregat-Commands aus Funktionen, die Antwort an die Funktion (CQRS108), Lese-Fähigkeiten
  in Funktionen. Zusammen mit dem Abbau aus dem alten Schritt 6 (Arbeit für Akteure, Aufträge aus dem Decide).
- Grammatik, Sonde (`soll.txt`, Gesten-Skript) und `--check` ziehen mit.

Der Server rechnet, was passt (W3): zu einem Output die Funktionen mit diesem Input, zu einem Output die Commands eines Aggregats samt Grad der
Feldzuordnung, je Katalog die Command-Liste mit „verwendet von“.

## 6. Bestand

Alle 20 Funktionen der Abläufe (7 Beispieldomäne, 13 Prüfdomäne) ziehen in Kataloge; wo sie heute Aggregat-Commands geben, geben sie
Katalog-Commands, und die Anbindung übersetzt. Drei lesen eine Projektion (`RangeAufloesung`, `MitgliederEinfrieren`, `Schaetzer`), dazu §7.1.

## 7. Offene Entscheidungen (mit Empfehlung, gilt bis zum Widerspruch)

1. **Lesen aus der Domäne:** eine Katalogfunktion liest keine Projektion; was sie braucht, trägt der Input mit, gelesen hat vorher der Client.
2. **Output, den die Domäne nicht braucht:** jeder Command-Output muss übersetzt werden (sonst baut es nicht, CQRS120). Empfehlung: dabei bleiben;
   wer einen Output nicht braucht, nimmt einen Graphen ohne ihn. Ein „verwerfen“ kommt erst, wenn ein echter Fall es verlangt.
3. **Allgemeiner Katalog:** genau einer je Lösung, `Allgemein.Katalog`, nur Commands (keine Funktionen). Alternative: jeder Katalog darf die
   Commands jedes anderen nutzen, ohne besonderen Katalog.
4. **Zwischenergebnisse anbinden** (ein Output, den im Graphen noch ein Knoten weiter nimmt): vorerst nicht, nur die Outputs des Graphen. Später
   über den Typ möglich, ohne neue Code-API.

5. **Ein Output für eine andere Domäne:** ein Ablauf übersetzt nur in Commands der eigenen Domäne, so bleibt die Zuordnung eindeutig. Braucht
   eine zweite Domäne das Ergebnis, bekommt sie einen eigenen Ablauf (eigener Lauf), oder der Client stößt sie über die Projektion an.
   Alternative: Commands fremder Domänen zulassen, dann gehört der Ablauf trotzdem der Domäne, an der er angelegt wurde, und die Kante zeigt
   sichtbar über die Bandgrenze.

## 8. Reihenfolge

1. **Katalog-Modal** mit Funktionen (Input · Funktion · Outputs), Command-Liste und allgemeinem Katalog.
2. **Graph-Modal** nur mit Katalogen, „＋“ am Output mit den passenden Funktionen.
3. **Hauptansicht:** „＋ Graph“ an jeder Domäne, Band aus dem Ort des Ablaufs, Graph aufgeklappt, „＋ ↦“ an jedem Output, Feldzuordnung, Eingang.
4. **Bestand umziehen und Regeln scharf** (§5, §6), Abbau.
5. **Doku** (docs/04, 07, 10 §10.9, CLAUDE.md) und Abnahme in einer Probe-Werkbank.
