# Vermessung

Ablage aller Messergebnisse (`docs/plan-garantien-und-durchsatz.md` §0.1). Jeder Lauf hat einen eigenen Ordner
`ergebnisse/<JJJJ-MM-TT_HHMM>_<commit>_<anlass>/` und wird committet; nur anhängen, nie überschreiben oder löschen — auch ungültige Läufe
(die Begründung steht in `ergebnis.json`). Ein Commit mit dem Zusatz `-geaendert` wurde auf einem nicht committeten Arbeitsbaum gemessen.

- `ergebnis.json`: Metadaten (Rechner, Commit, Branch, Umgebungen, Parameter, Fremdlast vorher/nachher und je Szenario, Anlass, Spalte),
  bei E2E-Läufen je Block die Kennzahlen und die Exactly-once-Prüfungen; `gueltig` und `begruendung`;
  `zusammenfassung` (die Zeilen der Tabelle in docs/12 §12.5).
- `latenzen/<szenario>.csv[.gz]`: jeder Vorgang mit Start und Latenz (ab 1 MB gzip-komprimiert).
- `pg_stat_statements.csv`: je Szenario und Wiederholung die 40 häufigsten Statements des Messfensters (ohne die Lese-Rolle des Treibers).
- `host.csv`: RSS und CPU des Hosts (und der Worker) jede Sekunde.
- `zusammenfassung.md`: die Tabelle des Laufs; bei Fehlern `log-ende-*.log` (das Ende der Host-Logs, die vollen Logs bleiben im Temp-Verzeichnis).

Erzeugt von `scripts/vermessung-e2e.sh` (Ende zu Ende unter Höchstlast, S1–S5, Störungslauf) und `scripts/vermessung-postgres.sh`
(Postgres-Grundgrößen). `--probe` (Funktionstest des Treibers) schreibt nicht hierher. Die fortlaufende Tabelle in
`docs/12-tests-und-vermessung.md` §12.5 erzeugt `scripts/vermessung-tabelle.sh --schreibe` aus den Ordnern mit `spalte` und `gueltig`.

## Was gemessen wird (in Worten)

| Block | was ein „Vorgang“ ist | wann er zählt (sichtbar) |
|---|---|---|
| **S1** Bildeingang (Beispieldomäne) | ein **Bildpaar**: zwei TIFF-Dateien `…_DC0.tiff` und `…_DC2.tiff` derselben Aufnahme (Aggregat `ImagePair`). Der Treiber legt 600 Paare auf einmal ins Quell-Verzeichnis (wie eine Kamera nach einem Ausfall); Datei-Quelle → Fluss `Bildeingang` (Dateiname deuten, Paar anlegen, verkleinern, Kontrast) → Projektionen | die Query `GetImagePair` liefert das Paar komplett |
| **S2** wie S1, Bild-Funktionen im Python-Worker | 200 Paare | wie S1 |
| **S3** Sammelvorgang heiß (Beispieldomäne) | ein **Teil**: `StarteTeilauftrag` + `SchließeTeilauftragAb`; der `TeilFertigProzess` meldet es beim **einen** Sammelvorgang (+1 fertig). Alle Teile treffen dasselbe Aggregat — gemessen wird die Decke eines einzelnen, umkämpften Aggregats; 8 bzw. 64 gleichzeitige Sender | `TeilVerbucht` im Log |
| **S4** Datensatz | alle Paare aus S1 in einen Datensatz aufnehmen und einfrieren (Flüsse mit Lese-Fähigkeit, großes Event) | `HoleDatensatz` meldet `Eingefroren` |
| **S5** Prüfdomäne | ein **Vorgang** = eine Prozess-Instanz über mehrere Aggregate der Prüfdomäne (s. u.): je zehn 8 Aufträge, die gelingen, 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung auf 4 Ziele; dazu die Reaktion „Nachschub“; 1, 8 bzw. 64 Sender, verteilt über viele Aggregate | `ProzessBeendet` im Log |
| **L1, L5** Leerlauf | 15 s ohne Eingang nach den Blöcken | Grundlast: was der Host ohne Last tut |

Die **Prüfdomäne** (`Pruefdomaene*`) ist eine zweite, bewusst fachneutrale Anwendung im Repo, nur für Framework-Tests: **Posten** (Bestand,
reservieren), **Kontingent** (Kapazität, belegen), **Auftrag** (reserviert Posten und belegt Kontingent, bestätigt erst, wenn beides klappt —
sonst wird zurückgenommen), **Verteilung** (bucht von einem Posten auf mehrere andere, schließt nach allen ab), Reaktion
**NachschubBeiMindestbestand** (bucht nach, wenn eine Reservierung den Mindestbestand unterschreitet). Host: `Host.Pruefdomaene`.

Kennzahlen je Block: **angenommen/s** (so schnell nimmt der Eingang an — Schreibseite), **sichtbar/s** und **Durchsatz bis alles sichtbar**
(Ende zu Ende), **Rückstand** am Ende der 15 s und **Abbauzeit**, Latenz, **Transaktionen und Queries je Vorgang** (Datenbank-Arbeit je Paar,
Teil bzw. Vorgang), CPU und RSS des Hosts.

## Wiederholen

```bash
scripts/vermessung-e2e.sh --anlass phase-1 --spalte "Phase 1"          # ≈ 5 min, Höchstlast S1–S5
scripts/vermessung-e2e.sh --art stoerung --anlass phase-1-stoerung     # ≈ 3 min, harte Host-Abbrüche, Redis weg
scripts/vermessung-tabelle.sh --schreibe                               # Tabelle in docs/12 §12.5 (eine Spalte je Anlass)
```

- Auf einem **sauberen, committeten** Arbeitsbaum messen (sonst trägt der Ordner `-geaendert`), danach den Ordner mitcommitten.
- **Nie** gleichzeitig mit einer Integrations-Suite, der Werkbank-Abnahme oder einem Build; keine Mehrstunden-Läufe (Tobi, 2026-10-15).
- Fremdlast steht im Ergebnis; Läufe auf dem MacBook Air vergleichen **Verhältnisse** zur Baseline, nicht absolute Zahlen. Bei starker Fremdlast
  lieber wiederholen.
- Baseline: `ergebnisse/2026-10-15_0429_3cfec32_baseline` und `…_0433_3cfec32_baseline-stoerung` (Commit `3cfec32`).

