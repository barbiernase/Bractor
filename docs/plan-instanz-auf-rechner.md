# Plan: Eine Instanz läuft auf ihrem Rechner

Stand 2026-11-03, Vorschlag nach dem Gespräch mit Tobi. Maßstab für den Umbau auf dem Branch `funktionsgraphen` (nach „Graphen ohne Lauf-Steuerung“).
Nach der Abnahme wird dieses Dokument gelöscht (Git-Verlauf); was bleibt, steht dann in docs/04 §4.4, docs/06, docs/11 und CLAUDE.md.

## Warum

Eine Instanz mit Zustand oder Quelle gehört in der Regel an eine bestimmte Maschine: die Quelle, die per OPC-UA an einer Steuerung hängt, steht im
Netz der Halle; das Modell, das weiterlernt, braucht die GPU. Woanders kann sie gar nicht laufen.

Heute entscheidet das der Zufall. Eine Instanz ohne Zustand läuft auf jedem Rechner im Cluster. Eine mit Zustand oder Quelle läuft auf dem, der als
erster eine Postgres-Sperre bekommt (`ZustandsSperre`, ein Advisory-Lock je Instanz, alle 2 s versucht); fällt er aus, übernimmt ein beliebiger
anderer. Für Hardware ist das falsch: der andere Rechner hat weder die Steuerung noch die GPU. Auch virtuelle Cluster-Actors (Proto.Actor) helfen
nicht — sie legen den Ort ebenso selbst fest und ziehen bei Ausfall um.

Wo etwas läuft, ist eine Frage der Umgebung, nicht des Codes (W7: Struktur ist Code, Auswahl und Werte sind Konfig). Steht der Rechner in der
Konfiguration, gibt es genau einen Schreiber schon durch sie, und die Sperre wird überflüssig.

## Entscheidungen

1. **Jeder Host hat einen Rechnernamen** — `Cluster:Rechner` in der Umgebung, wie jeder andere Wert auch mit `${env:…}` (ein Name je Maschine, z. B.
   `gpu-1`, `halle3-opcua`). Ohne Angabe hat der Host keinen Namen und bedient nur Instanzen, die keinen Rechner nennen.
2. **Eine Instanz nennt ihren Rechner** — `"Rechner": "gpu-1"` in ihrer Datei (`umgebungen/<name>/graphen/<instanz>.json`). Die Einheit ist die
   ganze Instanz, nicht ein einzelner Knoten: so bleibt eine Kette auf einer Maschine, und ihre Aufrufe bleiben im Speicher. Soll nur ein Schritt
   woanders rechnen, ist das Arbeit für einen Akteur draußen (Werkstatt, ein Arbeiter auf der GPU-Maschine) — das gibt es schon.
3. **Nur der genannte Rechner hält die Instanz.** Die Wurzel jedes Hosts legt den Graph-Actor einer festgelegten Instanz nur an, wenn der Rechnername
   passt, und meldet sie nur dann im Gossip-Zustand. Alle anderen Hosts schicken Eingänge über die vorhandenen Wege dorthin.
4. **Mit Zustand oder Quelle ist der Rechner Pflicht**, sobald der Cluster mehr als einen Knoten haben kann (`Cluster:Art` = `Consul`): fehlt er, ist
   das ein Boot-Fehler mit der Instanz-Datei als Karte. Im Einzelknoten-Betrieb (Prüfstände, Tests ohne Cluster) ist der eine Host der Rechner.
5. **Ohne Zustand ist der Rechner frei wählbar.** Nennt die Instanz keinen, läuft sie wie heute auf jedem Host (parallel, ausfallsicher); nennt sie
   einen, nur dort (eine GPU-Rechnung ohne Gedächtnis).
6. **Die Sperre fällt weg.** `ZustandsSperre`, ihre Verbindung je Instanz, die Meldung „gehalten“ an die Wurzel und die Schleife alle 2 s entfallen;
   genau ein Schreiber folgt aus Entscheidung 3 und 4.
7. **Fällt der Rechner aus, zieht nichts um.** Durable Eingänge warten in `cqrs_eingaenge`, bis er wieder da ist (der Zusteller findet keinen Halter
   und versucht es später); die Quelle setzt nach dem Neustart ab ihrem gesicherten Stand fort. Was in der Zeit auf dem schnellen Weg kam, geht
   verloren wie heute — der durable Rand bringt es wieder.
8. **Zwei Hosts mit demselben Rechnernamen** sind ein Konfigurationsfehler. Jeder Host meldet seinen Namen im Gossip-Zustand; sieht einer einen
   zweiten mit demselben Namen, hält nur der mit der kleineren Adresse die festgelegten Instanzen, der andere gibt sie ab und meldet den Fehler laut
   (Log und Einblick). Bis der Gossip das zeigt (Sekunden), können beide rechnen — dieselbe Grenze wie heute beim Wechsel der Sperre; fachliche
   Wirkungen schützt die Inbox.
9. **Nebenbei:** der gesicherte Zustand trägt die Kennungen der Aufrufe, die er enthält (Schutz vor Doppelzählung nach einem Absturz). Heute sind es
   die letzten 1 024, bei jeder Sicherung neu gepackt (16 KB). Künftig 256 — genug für die Wiederholungen nach einem Absturz, ein Viertel der Last.

## Beispiel

```json
// umgebungen/halle3/umgebung.json — der Host in der Halle
{ "Cluster": { "Art": "Consul", "Rechner": "${env:CQRS_RECHNER}" } }

// umgebungen/halle3/graphen/linie-halle3.json — die Quelle hängt an der Steuerung, sie läuft nur dort
{ "Graph": "Linienimport", "Akteur": "KameraSystem", "Rechner": "halle3-opcua",
  "Eingang": { "Pfad": "/daten/halle3" } }

// umgebungen/halle3/graphen/bildaufbereitung.json — ohne Zustand, ohne Rechner: läuft auf jedem Host
{ "Graph": "Bildaufbereitung" }
```

## Regeln mit Gegenstück (W5)

| Regel | Gegenstück |
|---|---|
| Eine Instanz mit Zustand oder Quelle nennt im Cluster ihren Rechner | Boot-Fehler (`GraphInstanzen.Aus`) |
| Ein Rechner, den eine Instanz nennt, ist ein Name (keine Liste, kein Muster) | Boot-Fehler |
| Zwei Hosts mit demselben Rechnernamen | Laufzeit: der zweite hält nichts Festgelegtes, Fehler im Log und im Einblick |

Grammatik: GR-INSTANZ nimmt den Rechner auf; GR-FUNKTION-ZUSTAND und GR-QUELLE nennen „ein Rechner“ statt „ein Knoten mit der Sperre“.

## Betroffen

- **Konfig:** `ClusterKonfig.Rechner`, `GraphInstanzKonfig.Rechner` (`Abstractions/Umgebung/UmgebungKonfig.cs`), `GraphInstanz` (Graph.cs), die Prüfung
  in `GraphInstanzen.Aus`.
- **Laufzeit:** `FunktionenWurzel` (legt festgelegte Instanzen nur auf ihrem Rechner an, `Haelt` ohne Sperre), `FunktionsOrte` (Rechnername im
  Gossip, doppelte Namen erkennen), `FunktionsLaufzeitWiring` (keine `ZustandsSperre`), `ZustandsSperre.cs` entfällt, `FunktionsParent`
  (`GesichertGesehen` 256).
- **Umgebungen:** `umgebungen/*/` — Instanzen mit Zustand oder Quelle bekommen ihren Rechner, Hosts ihren Namen (`deploy-multinode`).
- **Tests:** Prüfstand (Prüfung beim Start, Wurzel mit und ohne Rechner, doppelter Name), Prüfdomäne (`FunktionsZustandGarantieTests`: „rechnet
  auf genau einem Knoten“ jetzt über den Rechner), Verhaltens-Netz (Hosts mit Rechnernamen, Ausfall heißt warten).
- **Doku:** docs/04 §4.4 (Laufzeit, Zustand), docs/06 (Multi-Node: Rechnername je Host), docs/11 (ein Schreiber durch Konfiguration statt Sperre),
  docs/13 (PF-34 neu gefasst), CLAUDE.md.

## Schritte

Jeder Schritt endet mit grünem Build, grüner schneller Stufe, Commit und Push.

1. **Konfig und Prüfung.** `Rechner` an Host und Instanz, Boot-Fehler bei Zustand oder Quelle ohne Rechner im Cluster; die Umgebungen nachziehen.
   Die Laufzeit nutzt den Rechner noch nicht.
2. **Laufzeit.** Die Wurzel legt festgelegte Instanzen nur auf ihrem Rechner an und hält sie ohne Sperre; Rechnernamen im Gossip, doppelte erkannt;
   `ZustandsSperre` raus; `GesichertGesehen` 256. Tests dazu, auch für Ausfall (Eingänge warten, nichts zieht um).
3. **Doku und Abschluss.** Doku; volle Stufe grün; dieses Dokument löschen (eigener Commit, Eintrag in docs/README.md entfernen, in CLAUDE.md unter
   „Gelöschte Dokumente“).
