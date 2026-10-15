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
