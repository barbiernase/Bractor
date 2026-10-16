#!/usr/bin/env bash
# Die Leistungstabelle (docs/12 §12.5) — erzeugt aus den Ergebnis-Ordnern, nie von Hand.
#
#   scripts/vermessung-tabelle.sh             druckt die Tabelle (Markdown)
#   scripts/vermessung-tabelle.sh --schreibe  ersetzt sie in docs/12 zwischen <!-- vermessung-tabelle:anfang --> und <!-- …:ende -->
#
# Grundlage: alle gültigen Läufe im Format 2 (ergebnis.json "format": 2) auf dem Messserver ("rechner" enthält Ubuntu), davon die des jüngsten
# Commits. Last und Ruhe: je Szenario Median und Spanne über alle Wiederholungen aller Leistungsläufe dieses Commits; Störung: je Lauf eine
# Zeile. Die Spalten nennt der Treiber selbst ("spalten" im Ergebnis). Ältere Läufe (Format 1, S1–S5) stehen eingefroren im Anhang
# docs/12-anhang-messarchiv.md.
set -euo pipefail
WURZEL="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
exec python3 - "$WURZEL" "${1:-}" <<'PY'
import glob, json, os, re, statistics, sys

wurzel, modus = sys.argv[1], sys.argv[2]
doku = os.path.join(wurzel, "docs", "12-tests-und-vermessung.md")
laeufe = []
for f in sorted(glob.glob(os.path.join(wurzel, "vermessung", "ergebnisse", "*", "ergebnis.json"))):
    e = json.load(open(f, encoding="utf-8"))
    if e.get("format") == 2 and e.get("gueltig") and "Ubuntu" in e.get("rechner", ""):
        e["_ordner"] = os.path.basename(os.path.dirname(f))
        laeufe.append(e)
if not laeufe:
    tabelle = "(noch kein gültiger Lauf im Format 2)"
else:
    commit = laeufe[-1]["commit"]
    jetzt = [e for e in laeufe if e["commit"] == commit]
    leistung = [e for e in jetzt if e["art"] == "leistung"]
    stoerung = [e for e in jetzt if e["art"] == "stoerung"]

    def zahl(d):
        return f"{round(d):,}".replace(",", " ") if abs(d) >= 100 else f"{d:.1f}".replace(".", ",")

    def zelle(werte):
        werte = [w for w in werte if w is not None]
        if not werte:
            return "–"
        m, lo, hi = statistics.median(werte), min(werte), max(werte)
        return f"{zahl(m)} ({zahl(lo)}–{zahl(hi)})" if hi - lo > 0.05 * abs(m) else zahl(m)

    def link(e):
        return f"[{e['_ordner']}](../vermessung/ergebnisse/{e['_ordner']}/)"

    zeilen = [f"Commit `{commit}`, Messserver; Leistung: {', '.join(link(e) for e in leistung) or '–'} "
              f"(je Zelle Median über {sum(e['parameter']['wiederholungen'] for e in leistung)} Wiederholungen, in Klammern die Spanne, "
              f"wenn sie 5 % übersteigt); Störung: {', '.join(link(e) for e in stoerung) or '–'}.", ""]
    if leistung:
        spalten = leistung[-1]["spalten"]
        reihenfolge = list(leistung[-1]["szenarien"].keys())
        for tab in ("last", "ruhe"):
            sp = [s for s in spalten if s["tabelle"] == tab]
            namen = [n for n in reihenfolge if leistung[-1]["szenarien"][n].get("tabelle") == tab]
            if not namen:
                continue
            zeilen.append("| Szenario | " + " | ".join(s["titel"] for s in sp) + " |")
            zeilen.append("|---|" + "---:|" * len(sp))
            for n in namen:
                wd = [k["kennzahlen"] for e in leistung for k in e["szenarien"].get(n, {}).get("wiederholungen", [])]
                titel = leistung[-1]["szenarien"][n]["titel"]
                zeilen.append(f"| {titel} | " + " | ".join(zelle([w.get(s["key"]) for w in wd]) for s in sp) + " |")
            zeilen.append("")
    if stoerung:
        zeilen.append("| Störungslauf | Vorgänge Prüfdomäne | Bildpaare | Abbau nach der Last (s) | p99 Prüfdomäne (ms) | Exactly-once |")
        zeilen.append("|---|---:|---:|---:|---:|:-:|")
        for e in stoerung:
            k = e.get("kennzahlen", {})
            zeilen.append(f"| {link(e)} | {zahl(k.get('vorgaenge', 0))} | {zahl(k.get('bildpaare', 0))} | {zahl(k.get('abbau_s') or 0)} | "
                          f"{zahl(k.get('verteilt_p99_ms') or 0)} | {'grün' if e.get('exactly_once_ok') else 'ROT'} |")
    tabelle = "\n".join(zeilen).rstrip()

if modus == "--schreibe":
    text = open(doku, encoding="utf-8").read()
    if "<!-- vermessung-tabelle:anfang -->" not in text:
        sys.exit(f"Marker vermessung-tabelle fehlt in {doku}")
    neu = "<!-- vermessung-tabelle:anfang -->\n" + tabelle + "\n<!-- vermessung-tabelle:ende -->"
    text = re.sub(r"<!-- vermessung-tabelle:anfang -->.*?<!-- vermessung-tabelle:ende -->", lambda _: neu, text, flags=re.S)
    open(doku, "w", encoding="utf-8").write(text)
    print(f"Tabelle in {doku} geschrieben.")
else:
    print(tabelle)
PY
