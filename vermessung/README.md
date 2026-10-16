# Vermessung

Ablage aller Messergebnisse (`docs/plan-garantien-und-durchsatz.md` §0.1). Jeder Lauf hat einen eigenen Ordner
`ergebnisse/<JJJJ-MM-TT_HHMM>_<commit>_<anlass>/` und wird committet; nur anhängen, nie überschreiben oder löschen — auch ungültige Läufe
(die Begründung steht in `ergebnis.json`). Ein Commit mit dem Zusatz `-geaendert` wurde auf einem nicht committeten Arbeitsbaum gemessen.

- `ergebnis.json`: Metadaten (Rechner, Commit, Branch, Umgebungen, Parameter, Fremdlast), `format` (2 seit 2026-10-16: Leistung mit
  Wiederholungen im Host; ohne Feld bzw. 1: die Höchstlast-Blöcke S1–S5 davor), je Szenario jede Wiederholung mit allen Kennzahlen, Median, Min,
  Max und die Exactly-once-Prüfungen; `spalten` (welche Kennzahlen die Tabelle zeigt); `gueltig` und `begruendung`.
- `latenzen/<szenario>.csv[.gz]`: jeder Vorgang mit Start und Latenz (ab 1 MB gzip-komprimiert).
- `pg_stat_statements.csv`: je Szenario und Wiederholung die 40 häufigsten Statements des Messfensters (ohne die Lese-Rolle des Treibers).
- `host.csv`: RSS und CPU des Hosts jede Sekunde.
- `zusammenfassung.md`: die Tabellen des Laufs; bei Fehlern `log-ende-*.log` (das Ende der Host-Logs, die vollen Logs bleiben im Temp-Verzeichnis).

Erzeugt von `scripts/vermessung-e2e.sh` (Ebene 3 der Test-Suite, docs/12 §12.5). Die Tabelle in `docs/12-tests-und-vermessung.md` §12.5 erzeugt
`scripts/vermessung-tabelle.sh --schreibe` aus den gültigen Läufen im Format 2 des jüngsten Commits; alle älteren Läufe (S1–S5, die Phasen des Plans,
die Postgres-Grundgrößen von `scripts/vermessung-postgres.sh`) beschreibt eingefroren `docs/12-anhang-messarchiv.md`.

## Was gemessen wird (in Worten)

| Szenario | was ein „Vorgang“ ist | wann er zählt (sichtbar) |
|---|---|---|
| **Bildeingang** (Beispieldomäne) | ein **Bildpaar**: zwei TIFF-Dateien `…_DC0.tiff` und `…_DC2.tiff` derselben Aufnahme. Der Treiber legt 600 Paare auf einmal ins Quell-Verzeichnis (wie eine Kamera nach einem Ausfall); Datei-Quelle → Fluss `Bildeingang` (Dateiname deuten, Paar anlegen, verkleinern, Kontrast) → Projektionen | die Query `GetImagePair` liefert das Paar komplett |
| **Heißer Schreiber** (Beispieldomäne) | ein **Teil**: `StarteTeilauftrag` + `SchließeTeilauftragAb`; der `TeilFertigProzess` meldet es beim **einen** Sammelvorgang. 2 000 Teile von 64 Sendern an dasselbe Aggregat — die Decke eines einzelnen, umkämpften Aggregats | `TeilVerbucht` im Log |
| **Verteilt ruhig / an der Decke** (Prüfdomäne) | ein **Vorgang** = eine Prozess-Instanz über mehrere Aggregate (s. u.): je zehn 8 Aufträge, die gelingen, 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung auf 4 Ziele; dazu die Reaktion „Nachschub“. Ruhig: 1 Sender, 1 000 Vorgänge; Decke: 64 Sender, 3 000 Vorgänge | `ProzessBeendet` im Log |
| **Neuaufbau** | alle Projektionen der Beispieldomäne lesen das Log der Blöcke davor ab 0 (der gespeicherte Projektions-Stand wird gelöscht, wie bei einer Code-Änderung in Sim) | der Rückstand in `/monitoring/metrics` ist null |
| **Leerlauf** (je Anwendung) | ohne Eingang, erst nach dem Nachlauf (alle Konsumenten am Kopf des Logs), dann 60 s | Grundlast |

Die **Prüfdomäne** (`Pruefdomaene*`) ist eine zweite, bewusst fachneutrale Anwendung im Repo, nur für Framework-Tests: **Posten** (Bestand,
reservieren), **Kontingent** (Kapazität, belegen), **Auftrag** (reserviert Posten und belegt Kontingent, bestätigt erst, wenn beides klappt —
sonst wird zurückgenommen), **Verteilung** (bucht von einem Posten auf mehrere andere, schließt nach allen ab), Reaktion
**NachschubBeiMindestbestand** (bucht nach, wenn eine Reservierung den Mindestbestand unterschreitet). Host: `Host.Pruefdomaene`.

Kennzahlen (Tabelle): **Durchsatz** bis alles sichtbar, **Schreiben angenommen** je Sekunde, **Latenz** p50/p99, **Reaktion** p99, **Kosten je
Vorgang** (Transaktionen, Queries, CPU-Millisekunden des Hosts); ohne Eingang Queries/s, CPU, Nachlauf, RSS; Neuaufbau Events/s. Jeder Lastblock läuft
dreimal im selben Host — die Tabelle zeigt Median und Spanne.

## Wiederholen

```bash
scripts/server.sh lauf scripts/vermessung-e2e.sh --anlass server-<anlass>                       # ≈ 6 min, Leistung
scripts/server.sh lauf scripts/vermessung-e2e.sh --stoerung --anlass server-<anlass>-stoerung   # ≈ 4 min, harte Host-Abbrüche, Redis weg
scripts/vermessung-tabelle.sh --schreibe                                                         # Tabelle in docs/12 §12.5
```

- Auf einem **sauberen, committeten und gepushten** Stand messen (sonst trägt der Ordner `-geaendert`), danach den Ordner mitcommitten.
- **Nie** gleichzeitig mit `./pruefe.sh alles` oder einer anderen Messung; keine Mehrstunden-Läufe (Tobi, 2026-10-15).
- Fremdlast steht im Ergebnis. Für „besser oder schlechter“ zwei Läufe je Stand.
