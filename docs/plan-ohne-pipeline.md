# Plan: Graphen im Katalog, Übersetzungen in der Domäne — ohne Pipeline

Stand 2026-11-09. Ersetzt den gleichnamigen Plan vom Vormittag (Graphen in die Schreibseite). Tobi am 2026-11-09: „Wir brauchen Pipeline nicht.
Graphen sind auch domänenagnostisch, sie werden erst domänenspezifisch durch die Übersetzung. Die Übersetzungen müssen dann in den Domänen leben.“
Umgesetzt wird schrittweise; geprüft je Schritt mit `dotnet build` und in der Werkbank, Server-Suiten nur zur Abnahme und auf Tobis Wort.

## 1. Das Bild

Es gibt zwei Arten von Code, die nichts voneinander wissen müssen, und eine dünne Naht dazwischen:

| | wo | was | kennt |
|---|---|---|---|
| **Katalog** | `<Name>.Katalog` | Funktionen, **Graphen**, ihre Commands ohne Ziel und Hinweise, Konfig, Technik, Lese-Fähigkeiten | Abstractions, Core, andere Kataloge |
| **Domäne** | `<Wurzel>` (Schreibseite) | Aggregate, Commands, Events, Hinweise, Wertobjekte, Aggregat-Queries — **und die Übersetzungen** | dazu die Kataloge, die sie übersetzt |
| **Umgebung** | `umgebungen/<name>/graphen/<instanz>.json` | welcher Katalog-Graph wie oft läuft, mit Konfig, Akteur, Rechner | — |

- Ein **Graph** ist immer domänenneutral und liegt in einem Katalog, wie eine Funktion. Es gibt keinen Graphen der Anwendung mehr, keinen „Ablauf“, keine
  Rolle `Ablaeufe`, kein `<Wurzel>.Pipeline`. Was früher ein Ablauf war (Dateiwächter + Bildaufbereitung), ist ein Graph im Katalog.
- Eine **Übersetzung** macht ihn domänenspezifisch. Sie ist eine eigene kleine Deklaration in der Domäne, nicht mehr ein Knoten im Graphen.
- Die **Umgebung** sagt, welche Katalog-Graphen laufen (Instanzen). Struktur ist Code, Auswahl und Werte sind Konfig (W7) — unverändert.

## 2. Die Übersetzung als Code-Fakt

```csharp
// Domain/ImagePair/Uebersetzungen.cs
namespace Domain.ImagePair;

/// <summary>Das gedeutete Bild aus der Bildaufbereitung wird ein Paar: die Id aus der Guid des Schlüssels, alles andere nach Namen.</summary>
[Feld(nameof(NimmBildAuf.AggregateId), nameof(BildGedeutet.Paar))]
public sealed record Aufnehmen : IUebersetzung<BildGedeutet, NimmBildAuf>;
```

- Marker `IUebersetzung<TEin, TAus>` (ersetzt den Knotentyp `Uebersetze<TEin, TAus>`); `[Feld(Ziel, Quelle)]` steht am Record. Der Name des Records
  ist der Name der Übersetzung (Karte, Spur, Command-Ids).
- Die Zuordnung bleibt, wie sie ist (A1, CQRS119): `[Feld]`, sonst gleicher Name mit implizit zuweisbarem Typ, sonst der Standardwert; Feld für Feld,
  ohne Logik (CQRS121).
- Lage: Primitive `Uebersetzung`, Schreibseite, Sammeldatei `Uebersetzungen.cs` im Bereich des Aggregats, an das sie schreibt bzw. dessen Antwort sie
  nimmt (für eine Kopf-Übersetzung: der Bereich der Nachricht, die sie nimmt).

## 3. Wohin eine Nachricht geht — Routing über Typen (Invariante 3)

Eine Übersetzung nennt keinen Graphen und keinen Knoten. Sie nimmt einen **Typ**, und der Typ entscheidet:

1. **Aus einem Graphen hinaus.** Was ein Knoten eines laufenden Graphen ausgibt und eine Übersetzung nimmt, wird übersetzt — inline, im Kind des Knotens.
   Das gilt für einen Ausgang wie für einen Zwischenstand, den der Graph intern weitergibt (Abgriff). Nehmen mehrere Übersetzungen denselben Typ, ist
   das eine Gabel: jede erzeugt ihre Nachricht.
2. **Ergebnis einer Übersetzung.** Ein Command an ein Aggregat geht an das Aggregat; ein Command ohne Ziel geht an den Kopf genau eines Graphen (CQRS116).
3. **Antwort.** Antwortet das Aggregat auf den Command einer Übersetzung mit einem Event, das eine Übersetzung nimmt, rechnet sie gleich weiter, im
   selben Kind (eine **Antwort-Übersetzung**, `IUebersetzung<PostenReserviert, VermerkeReservierung>`). Ihr Ergebnis geht wie in 2. Geht es an den Kopf
   eines Graphen, ist das eine neue Kette, kein Kreis. Ob das Event als Antwort überhaupt kommen kann, prüft CQRS123 (das Event steht in der Signatur
   des `Decide` eines Commands, den eine Übersetzung erzeugt).
4. **Von einem Client.** Ein Akteur gibt entweder den Eingang des Katalog-Graphen direkt hinein (`IDarf<DateiGefunden>`) oder eine Nachricht der Domäne,
   die eine **Kopf-Übersetzung** in diesen Eingang übersetzt (`IUebersetzung<DateiGefundenAngefordert, DateiGefunden>`). Übersetzt wird beim Annehmen
   des Eingangs.
5. **Kein Empfänger.** Ein Command ohne Ziel, der einen laufenden Graphen verlässt, ohne dass eine Übersetzung oder ein anderer Graph ihn nimmt, ist ein
   Build-Fehler (CQRS120, A3). Hinweise dürfen hinaus, an die Akteure, die sie hören.

Folge: Routing ist eine Eigenschaft der Typen, nicht einer Instanz. Läuft derselbe Katalog-Graph mehrmals (eine Instanz je Linie), übersetzt dieselbe
Übersetzung die Ausgaben aller Instanzen. Wer je Instanz anderes braucht, braucht verschiedene Typen (A5, generische Ausprägungen) — so wie heute ein
Command genau einen Empfänger hat.

Der **Akteur einer Instanz** (Graph mit Quelle) muss dürfen, was aus seinen Ausgängen nach der Übersetzung entsteht (Start-Prüfung,
`GraphInstanzen.Aus`): beim Linienimport das Kamerasystem `NimmBildAuf`.

## 4. Was bleibt aus dem Plan vom Vormittag

- **Lese-Fähigkeit aus dem Katalog** (umgesetzt, 54fd8181): der Katalog deklariert, was er lesen will, ein Store im Speicher der Anwendung setzt es um;
  CQRS124 meldet eine Fähigkeit ohne Store.
- **`Herkunft.NeueId(zweck)`** (umgesetzt): eine Id ohne Aggregat-Typ für Katalogfunktionen; die Übersetzung macht daraus `Id<T>` (`[Feld]`).
- **CQRS122** (neu, Schritt 4): Zustand, `Decide`, `Apply`, Commands an Aggregate, Events, Hinweise und Aggregat-Queries nennen keinen Katalog-Typ —
  erlaubt sind sie nur in Übersetzungen. Die Domäne referenziert Kataloge, der Analyzer hält sie trotzdem sauber.
- Die Antwort-Übersetzung über `[Von]` auf eine Übersetzung im Graphen (54fd8181) wird durch das Routing über Typen (§3.3) ersetzt.

## 5. Bestand

| heute | wird |
|---|---|
| `Domain.Pipeline/ImagePair`: `Linienimport` (Dateiwächter → Bildaufbereitung → Übersetzung) | Graph im Katalog `Bildvorschau.Katalog` (Dateiwächter → Bildaufbereitung, ohne Übersetzung); Instanzen wie heute |
| `Domain.Pipeline/ImagePair`: `Bildaufnahme` (Kopf Bildaufbereitung → Übersetzung) | entfällt: eine Instanz des Katalog-Graphen `Bildaufbereitung` (Kopf `DateiGefunden`) |
| die Übersetzung `Aufnehmen` in beiden | eine Übersetzung `Domain/ImagePair/Uebersetzungen.cs` |
| `Pruefdomaene.Pipeline`: Funktionen, die die Domäne kennen, ihre Graphen | `Pruefdomaene.Katalog` (neutral), die Abbildungen als Übersetzungen in `Pruefdomaene/<Bereich>/Uebersetzungen.cs` |
| `Pruefdomaene.Pipeline/Skalierung`: `Hochrechnung` mit zwei Übersetzungen | Kopf- und Ausgangs-Übersetzung in `Pruefdomaene/Messung`; `Skalierer` läuft als Instanz eines Graphen im Katalog `Rechenwerk.Katalog` |
| Sonde: `Ablaeufe.Funkortung` (Graph `Ortung` mit Übersetzungen) | Graph `Ortung` im Sonden-Katalog, Übersetzungen in `Funkortung.Ortung` |
| Tobis Versuch `Domain/Pipeline/ImagePair/Graphen.cs` (in-Graph-Übersetzungen) | bleibt liegen, bis Tobi sagt, was daraus wird — im neuen Modell sind es vier Übersetzungen in `Domain/ImagePair` |

## 6. Schritte

1. **Kern:** `IUebersetzung<TEin, TAus>` statt `Uebersetze`; Lesart `Uebersetzungsform` für einen Record; der Graph kennt keine Übersetzung mehr
   (`Graphform`, `GraphGenerator`); eine generierte Tabelle `GeneratedUebersetzungen` (Typ → Übersetzungen); das Kind übersetzt jede Ausgabe nach Typ,
   rechnet Antworten weiter, der Eingang übersetzt am Kopf; Analyzer CQRS116/119–121/123 auf das neue Bild; Start-Prüfung des Akteurs.
2. **Beispieldomäne:** Graphen in den Katalog, `Aufnehmen` in die Domäne, `Domain.Pipeline` weg.
3. **Prüfdomäne:** `Pruefdomaene.Katalog`, Übersetzungen, `Pruefdomaene.Pipeline` weg.
4. **Die Rolle verschwindet:** Projektstruktur, `Lageart` (Graph nur im Katalog, Übersetzung in der Schreibseite), CQRS098, CQRS122, Codegen, Projekte.
5. **Sonde:** Graphen in Sonden-Kataloge, Übersetzungen in die Domänen der Sonde, Gesten-Skript, `soll.txt`.
6. **Sicht und Editor:** Die Spalte Graphen zeigt die Katalog-Graphen, die laufen (ohne Band, sie gehören keiner Domäne); die Spalte Übersetzungen die
   Übersetzungen im Band ihrer Domäne; Kanten nach Typ. „＋ ↦“ an einem Ausgang legt eine Übersetzung in einer Domäne an, das Modal ordnet die Felder zu.
7. **Doku und Abnahme** (docs/04, 05, 07, 10, 11, 13, konzept-funktionen-im-editor, README, CLAUDE.md).
