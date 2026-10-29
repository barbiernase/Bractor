#!/usr/bin/env python3
"""Auswertung der Gesamtprüfung (docs/12 §12.6) — von scripts/gesamtpruefung.sh gerufen.

  gesamtpruefung.py korrektheit <tests.log>          rote Tests der vollen Stufe bis zu zweimal einzeln wiederholen → JSON auf stdout
  gesamtpruefung.py bericht --korrektheit <json> --messung <ordner> [--stoerung <ordner>] [--profil <ordner>] [--vergleich <ordner>]
                            [--dauer <s>]             → gesamtpruefung.md im Messungs-Ordner (und auf stdout); auffällige Szenarien in
                                                        <messung>/auffaellig.txt (eins je Zeile, für die A/B-Nachmessung)
  gesamtpruefung.py ab --a <ordner> --b <ordner> --messung <ordner>   A/B-Nachmessung an den Bericht anhängen

Rauschen: je Kennzahl ein Band aus den bisher gemessenen Streuungen gleicher Commits (S1 ±10 % Durchsatz, „angenommen“ bis ±20 %, Kosten je
Vorgang ±5 %). Innerhalb des Bands „im Rauschen“, außerhalb „besser“ oder „auffällig“ — nur „auffällig“ löst die A/B-Nachmessung aus.
"""
import argparse
import json
import os
import re
import subprocess
import sys

# Kennzahl → (Band als Anteil, Mindest-Abweichung in der Einheit, höher ist besser?, Beschriftung). Auffällig ist nur, was beides
#   überschreitet — sonst wären 13 → 27 ms Latenz oder 0 → 2 s Nachlauf ein Alarm. Was hier fehlt, wird gezeigt, aber nicht bewertet.
BEWERTUNG = {
    "durchsatz_je_s": (0.15, 0, True, "Durchsatz bis alles sichtbar"),
    "angenommen_je_s": (0.25, 0, True, "angenommen"),
    "latenz_p50_ms": (0.25, 25, False, "Latenz p50"),
    "latenz_p99_ms": (0.30, 50, False, "Latenz p99"),
    "quittung_p50_ms": (0.25, 10, False, "Quittung p50"),
    "quittung_p99_ms": (0.30, 25, False, "Quittung p99"),
    "abbau_s": (0.30, 1, False, "Abbauzeit"),
    "tx_je_vorgang": (0.05, 0.2, False, "Transaktionen je Vorgang"),
    "queries_je_vorgang": (0.08, 2, False, "Queries je Vorgang"),
    "cpu_prozent": (0.20, 25, False, "CPU Host"),
    "rss_mb": (0.30, 150, False, "RSS Host"),
    "nachlauf_s": (0.50, 10, False, "Nachlauf"),
    "queries_je_s": (0.25, 2, False, "Queries/s (Leerlauf)"),
}
LEERLAUF_KENNZAHLEN = {"queries_je_s", "cpu_prozent", "rss_mb", "nachlauf_s"}
LAST_KENNZAHLEN = set(BEWERTUNG) - {"queries_je_s", "nachlauf_s"}

# Szenarien, die auch beim selben Code weiter streuen als das Band: (Szenario, Kennzahl) → Band. S5 unter Parallelität (PF-36): derselbe
#   Commit `4f43172` lieferte in S5-8 243 bis 314, in S5-64 224 bis 363 Vorgänge/s; p99 und Abbauzeit folgen dem Durchsatz.
STREUUNG = {(sz, key): 0.40 for sz in ("S5-8", "S5-64") for key in ("durchsatz_je_s", "latenz_p99_ms", "abbau_s")}


def kennzahlen(name):
    return LEERLAUF_KENNZAHLEN if name.startswith("L") else LAST_KENNZAHLEN

# Bekannte, unregelmäßig rote Tests mit ihrer Nummer in docs/13 — der Bericht nennt sie, statt sie zu verschweigen.
BEKANNT = {
    "NeustartTests.Nach_dem_harten_Neustart_unter_Last": "G-18",
    "TwoNodeCommandDispatchTests": "Aufbau zweier Knoten (wackelt)",
}

PROJEKTE = [  # Präfix des voll qualifizierten Testnamens → Projekt
    ("Infrastructure.Pruefstand.", "Infrastructure.Pruefstand.Tests"),
    ("Infrastructure.Integration.Tests.", "Infrastructure.Integration.Tests"),
    ("Pruefdomaene.Integration.Tests.", "Pruefdomaene.Integration.Tests"),
    ("Verhalten.Tests.", "Verhalten.Tests"),
    ("Client.Infrastructure.CollectionTests", "Client.Infrastructure.CollectionTests"),
]


# ── Korrektheit ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

def korrektheit(log):
    text = open(log, encoding="utf-8", errors="replace").read()
    stufe = []   # Zeilen der Zusammenfassung: (Name, Ergebnis)
    in_tabelle = False
    for z in text.splitlines():
        if z.startswith("== Stufe voll"):
            in_tabelle = True
            continue
        if in_tabelle:
            m = re.match(r"^(.*?)\s+\d+s\s\s(.*)$", z)
            if m:
                stufe.append({"name": m.group(1).strip(), "ergebnis": m.group(2).strip()})
    rot = sorted(set(re.findall(r"^\s+Failed ([A-Za-z0-9_.]+)", text, re.M)))
    tests = []
    for name in rot:
        projekt = next((p for praefix, p in PROJEKTE if name.startswith(praefix)), None)
        eintrag = {"test": name, "projekt": projekt, "wiederholungen": [], "bekannt": next((v for k, v in BEKANNT.items() if k in name), None)}
        if projekt:
            for _ in range(2):
                aus = subprocess.run(["scripts/tests.sh", "suite", projekt, f"FullyQualifiedName~{name}"], capture_output=True, text=True)
                eintrag["wiederholungen"].append("grün" if aus.returncode == 0 else "rot")
        eintrag["urteil"] = ("wackelt" if "grün" in eintrag["wiederholungen"] else "rot") if eintrag["wiederholungen"] else "rot"
        tests.append(eintrag)
    schritte_rot = [s["name"] for s in stufe if "ROT" in s["ergebnis"] and not re.search(r"\d+ grün, \d+ rot", s["ergebnis"])]
    json.dump({"stufe": stufe, "tests": tests, "schritte_rot": schritte_rot}, sys.stdout, ensure_ascii=False, indent=1)


# ── Bericht ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

def lade(ordner):
    return json.load(open(os.path.join(ordner, "ergebnis.json"), encoding="utf-8")) if ordner else None


def zahl(x):
    if x is None:
        return "–"
    if isinstance(x, float) and (x != x):
        return "–"
    return f"{x:,.0f}".replace(",", " ") if abs(x) >= 100 else f"{x:.1f}".replace(".", ",")


def urteil(name, key, ist, soll):
    if key not in BEWERTUNG or ist is None or soll is None or soll == 0 or ist != ist or soll != soll:
        return "–"
    band, mindest, hoeher_besser, _ = BEWERTUNG[key]
    band = STREUUNG.get((name, key), band)
    rel = (ist - soll) / abs(soll)
    if abs(rel) <= band or abs(ist - soll) <= mindest:
        return "im Rauschen"
    besser = rel > 0 if hoeher_besser else rel < 0
    wie = f"{ist / soll:.0f}×" if abs(rel) >= 10 else f"{rel:+.0%}"
    return f"besser ({wie})" if besser else f"**auffällig ({wie})**"


def bericht(a):
    k = json.load(open(a.korrektheit, encoding="utf-8"))
    m, s, p, v = lade(a.messung), lade(a.stoerung), lade(a.profil), lade(a.vergleich)
    zeilen = []
    w = zeilen.append
    auffaellig = set()

    rote = [t for t in k["tests"] if t["urteil"] == "rot"]
    wackel = [t for t in k["tests"] if t["urteil"] == "wackelt"]
    ampel_k = "rot" if rote or k["schritte_rot"] else ("gelb (wackelt)" if wackel else "grün")
    ampel_m = "grün" if m and m.get("gueltig") else "rot"
    ampel_s = ("grün" if s.get("gueltig") else "rot") if s else "–"

    w(f"# Gesamtprüfung `{m['commit']}` ({m['zeit'][:16].replace('T', ' ')} UTC)")
    w("")
    w(f"Rechner: {m['rechner']}  ")
    if a.dauer:
        w(f"Dauer: {int(a.dauer) // 60} min  ")
    w(f"Vergleich: {'`' + v['commit'] + '` (' + os.path.basename(a.vergleich) + ')' if v else 'keiner'}")
    w("")
    w("| Teil | Ergebnis |")
    w("|---|---|")
    w(f"| Korrektheit (volle Stufe) | {ampel_k} |")
    w(f"| Messung (Exactly-once unter Last) | {ampel_m} |")
    w(f"| Störung (harte Abbrüche, Exactly-once, Vollständigkeit) | {ampel_s} |")
    w(f"| Profil | {'ja' if p else '–'} |")
    w("")

    w("## Korrektheit")
    w("")
    for z in k["stufe"]:
        w(f"- {z['name']}: {z['ergebnis']}")
    if k["tests"]:
        w("")
        w("| roter Test | Wiederholungen | Urteil | bekannt |")
        w("|---|---|---|---|")
        for t in k["tests"]:
            w(f"| `{t['test']}` | {', '.join(t['wiederholungen']) or '–'} | {t['urteil']} | {t['bekannt'] or '–'} |")
    w("")

    w("## Messung")
    w("")
    if not m.get("gueltig"):
        w(f"**Ungültig:** {'; '.join(m.get('begruendung', []))}")
        w("")
    w(f"Fremdlast vorher: {m.get('fremdlast_vorher', '–')}  ")
    w(f"Fremdlast nachher: {m.get('fremdlast_nachher', '–')}")
    w("")
    w("| Szenario | Kennzahl | jetzt | Vergleich | Urteil |")
    w("|---|---|---:|---:|---|")
    for name, sz in sorted(m["szenarien"].items()):
        med = sz["median"]
        vmed = (v or {}).get("szenarien", {}).get(name, {}).get("median", {})
        for key in [k_ for k_ in BEWERTUNG if k_ in kennzahlen(name) and k_ in med]:
            u = urteil(name, key, med.get(key), vmed.get(key))
            if u.startswith("**auffällig"):
                auffaellig.add(name)
            w(f"| {name} {sz.get('titel', '')} | {BEWERTUNG[key][3]} | {zahl(med.get(key))} | {zahl(vmed.get(key))} | {u} |")
        if not sz["exactly_once"]["ok"]:
            w(f"| {name} | **Exactly-once rot** | | | {', '.join(x['name'] for x in sz['exactly_once']['pruefungen'] if not x['ok'])} |")
    w("")
    if auffaellig:
        w(f"Auffällig (A/B-Nachmessung): {', '.join(sorted(auffaellig))}")
        w("")

    if s:
        w("## Störung")
        w("")
        w(f"Gültig: {'ja' if s.get('gueltig') else 'nein — ' + '; '.join(s.get('begruendung', []))}  ")
        w(f"Exactly-once: {'grün' if s.get('exactly_once_ok') else 'rot'}  ")
        if s.get("nicht_abgebaut"):
            w(f"Nicht abgebaut: {', '.join(s['nicht_abgebaut'])}")
        w("")

    if a.profil:
        w("## Profil")
        w("")
        sonde = os.path.join(a.profil, "sonde")
        for datei in sorted(os.listdir(sonde)) if os.path.isdir(sonde) else []:
            if datei.startswith("profil-") and datei.endswith(".md"):
                w(f"### {datei[7:-3]}")
                w("")
                # Ohne die eigene Überschrift des Profils; seine Abschnitte eine Ebene unter dem Abschnitt hier.
                inhalt = open(os.path.join(sonde, datei), encoding="utf-8").read().splitlines()[:40]
                w("\n".join("##" + z if z.startswith("## ") else z for z in inhalt if not z.startswith("# ")))
                w("")
        if os.path.exists(os.path.join(sonde, "sonde.md")):
            w(f"Engpass-Sonde je Abschnitt (CPU je Prozess, Postgres-Wartezeiten, Zähler): `{os.path.relpath(os.path.join(sonde, 'auswertung.md'))}`")
            w("")

    text = "\n".join(zeilen) + "\n"
    open(os.path.join(a.messung, "gesamtpruefung.md"), "w", encoding="utf-8").write(text)
    open(os.path.join(a.messung, "auffaellig.txt"), "w", encoding="utf-8").write("".join(f"{x}\n" for x in sorted(auffaellig)))
    print(text)


def ab(a):
    ea, eb = lade(a.a), lade(a.b)
    zeilen = ["## A/B-Nachmessung", "", f"A = `{ea['commit']}` ({os.path.basename(a.a)}), B = `{eb['commit']}` ({os.path.basename(a.b)})", "",
              "| Szenario | Kennzahl | A | B | Urteil |", "|---|---|---:|---:|---|"]
    for name, sz in sorted(eb["szenarien"].items()):
        amed = ea.get("szenarien", {}).get(name, {}).get("median", {})
        for key in [k for k in BEWERTUNG if k in kennzahlen(name) and k in sz["median"]]:
            zeilen.append(f"| {name} | {BEWERTUNG[key][3]} | {zahl(amed.get(key))} | {zahl(sz['median'].get(key))} | "
                          f"{urteil(name, key, sz['median'].get(key), amed.get(key))} |")
    text = "\n".join(zeilen) + "\n"
    with open(os.path.join(a.messung, "gesamtpruefung.md"), "a", encoding="utf-8") as f:
        f.write("\n" + text)
    print(text)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="was", required=True)
    k = sub.add_parser("korrektheit")
    k.add_argument("log")
    b = sub.add_parser("bericht")
    for o in ("--korrektheit", "--messung"):
        b.add_argument(o, required=True)
    for o in ("--stoerung", "--profil", "--vergleich"):
        b.add_argument(o, default="")
    b.add_argument("--dauer", type=float, default=0)
    x = sub.add_parser("ab")
    for o in ("--a", "--b", "--messung"):
        x.add_argument(o, required=True)
    a = ap.parse_args()
    {"korrektheit": lambda: korrektheit(a.log), "bericht": lambda: bericht(a), "ab": lambda: ab(a)}[a.was]()


if __name__ == "__main__":
    main()
