# Vermessung

Ablage aller Messergebnisse (`docs/plan-garantien-und-durchsatz.md` §0.1). Jeder Lauf hat einen eigenen Ordner
`ergebnisse/<JJJJ-MM-TT_HHMM>_<commit>_<anlass>/` und wird committet; nur anhängen, nie überschreiben oder löschen.

- `ergebnis.json`: Metadaten (Rechner, Commit, Umgebung, Parameter, Fremdlast, Anlass) und die Werte; bei E2E-Läufen je Szenario alle
  Wiederholungen, der Median und das Ergebnis der Exactly-once-Prüfung.
- weitere Rohdaten je nach Skript (`ausgabe.txt`, `latenzen/*.csv[.gz]`, `pg_stat_statements.csv`, `host.csv`, `zusammenfassung.md`).

Erzeugt von `scripts/vermessung-postgres.sh` (Postgres-Grundgrößen) und `scripts/vermessung-e2e.sh` (Ende-zu-Ende, Szenarien S1–S5).
Die Tabelle in `docs/12-tests-und-vermessung.md` §12.5 verweist auf die Ordner.
