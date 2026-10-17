#!/usr/bin/env python3
"""Auswertung der Engpass-Sonde (scripts/sonde-engpass.py) je Abschnitt eines Messlaufs.

  scripts/sonde-auswertung.py <ergebnis-ordner>        liest <ordner>/host.csv (Abschnitte des Treibers) und <ordner>/sonde/*.csv(.gz),
                                                       schreibt <ordner>/sonde/sonde.md und sonde.json

Je Abschnitt (S3-…, S5-…, L1, L5): mittlere CPU je Prozessgruppe in Kernen, Auslastung des Rechners, CPU- und IO-Druck, die heißesten
Host-Threads, die mittlere Zahl der Postgres-Verbindungen je Zustand und Wartegrund, die häufigsten laufenden Anweisungen, WAL-Raten und
die dotnet-counters (Threadpool, Npgsql-Pool) des Hosts.
"""
import csv
import glob
import gzip
import io
import json
import os
import re
import sys
from collections import defaultdict
from datetime import datetime, timedelta, timezone


def oeffne(pfad):
    if os.path.exists(pfad):
        return open(pfad, newline="", encoding="utf-8-sig")
    if os.path.exists(pfad + ".gz"):
        return io.TextIOWrapper(gzip.open(pfad + ".gz"), newline="", encoding="utf-8-sig")
    return None


def zeit(s):
    s = s.strip()
    if s.endswith("Z"):
        s = s[:-1] + "+00:00"
    # .NET schreibt sieben Nachkommastellen, Python versteht sechs
    s = re.sub(r"(\.\d{6})\d+", r"\1", s)
    t = datetime.fromisoformat(s)
    return t if t.tzinfo else t.replace(tzinfo=timezone.utc)


def abschnitte(ordner):
    """Abschnitt → (von, bis). Der Treiber setzt den Namen zu Beginn; seine Proben kommen jede Sekunde."""
    erst, letzt = {}, {}
    with oeffne(os.path.join(ordner, "host.csv")) as f:
        for z in csv.DictReader(f):
            a = z["abschnitt"]
            if not a:
                continue
            t = zeit(z["zeit_utc"])
            erst.setdefault(a, t)
            letzt[a] = t
    return {a: (erst[a] - timedelta(seconds=1), letzt[a]) for a in erst}


def mittel(xs):
    return sum(xs) / len(xs) if xs else float("nan")


def rolle_des_abschnitts(a):
    return "host-pruefdomaene" if a.startswith(("S5", "L5")) else "host-beispiel"


def lade_csv(pfad, trenner=","):
    f = oeffne(pfad)
    if f is None:
        return []
    with f:
        return list(csv.DictReader(f, delimiter=trenner))


def counters(sonde):
    """dotnet-counters-CSV je (rolle, pid) → Liste (zeit, name, wert). Zeitstempel in Ortszeit des Servers (dort UTC)."""
    erg = []
    for pfad in sorted(glob.glob(os.path.join(sonde, "counters-*.csv*"))):
        m = re.match(r"counters-(.+)-(\d+)\.csv", os.path.basename(pfad))
        if not m:
            continue
        rolle = m.group(1)
        f = oeffne(pfad.removesuffix(".gz"))
        with f:
            for z in csv.reader(f):
                if len(z) < 5 or z[0] == "Timestamp":
                    continue
                try:
                    t = datetime.strptime(z[0], "%m/%d/%Y %H:%M:%S").replace(tzinfo=timezone.utc)
                    erg.append((rolle, t, z[2], float(z[4])))
                except ValueError:
                    continue
    return erg


WICHTIGE_ZAEHLER = [
    ("dotnet.thread_pool.queue.length", "Threadpool-Warteschlange"),
    ("dotnet.thread_pool.thread.count", "Threadpool-Threads"),
    ("dotnet.monitor.lock_contentions", "Lock-Konflikte (je s)"),
    ("dotnet.gc.pause.time", "GC-Pause (s je s)"),
    ("db.client.connections.usage", "Npgsql-Verbindungen"),
    ("db.client.connections.pending_requests", "Npgsql wartende Anfragen"),
    ("db.client.connections.max", "Npgsql höchstens"),
    ("db.client.connections.timeouts", "Npgsql Timeouts"),
    ("db.client.commands.executing", "Npgsql laufende Befehle"),
]


def werte(ordner):
    sonde = os.path.join(ordner, "sonde")
    ab = abschnitte(ordner)
    gruppen = lade_csv(os.path.join(sonde, "cpu_gruppen.csv"))
    kerne = lade_csv(os.path.join(sonde, "cpu_kerne.csv"))
    threads = lade_csv(os.path.join(sonde, "threads.csv"))
    aktiv = lade_csv(os.path.join(sonde, "pg_aktivitaet.csv"), "\t")
    wal = lade_csv(os.path.join(sonde, "pg_wal.csv"))
    zaehler = counters(sonde)
    for liste in (gruppen, kerne, threads, aktiv, wal):
        for z in liste:
            z["_t"] = zeit(z["zeit_utc"])

    erg = {}
    for a, (von, bis) in sorted(ab.items(), key=lambda kv: kv[1][0]):
        if not re.match(r"(S3|S5|L1|L5)", a):
            continue
        rolle = rolle_des_abschnitts(a)
        w = {"von": von.isoformat(), "bis": bis.isoformat(), "dauer_s": (bis - von).total_seconds()}

        cpu = defaultdict(list)
        for z in gruppen:
            if von <= z["_t"] <= bis:
                cpu[z["gruppe"]].append(float(z["cpu_prozent"]))
        w["cpu_kerne"] = {g: round(mittel(v) / 100, 2) for g, v in sorted(cpu.items(), key=lambda kv: -mittel(kv[1])) if mittel(v) >= 1}

        kz = [z for z in kerne if von <= z["_t"] <= bis]
        if kz:
            n = len([k for k in kz[0] if re.fullmatch(r"k\d+", k)])
            w["rechner"] = {
                "auslastung_prozent": round(mittel([float(z["gesamt_prozent"]) for z in kz]), 1),
                "iowait_prozent": round(mittel([float(z["iowait_prozent"]) for z in kz]), 1),
                "cpu_druck": round(mittel([float(z["psi_cpu_some"]) for z in kz]), 3),
                "io_druck": round(mittel([float(z["psi_io_some"]) for z in kz]), 3),
                "kerne_ueber_90_prozent": round(mittel([sum(1 for i in range(n) if float(z[f"k{i}"]) > 90) for z in kz]), 1),
                "je_kern_prozent": [round(mittel([float(z[f"k{i}"]) for z in kz])) for i in range(n)],
            }

        th = defaultdict(list)
        namen = {}
        for z in threads:
            if von <= z["_t"] <= bis and z["rolle"] == rolle:
                th[z["tid"]].append(float(z["cpu_prozent"]))
                namen[z["tid"]] = z["name"]
        sek = max(1.0, w["dauer_s"])
        heiss = sorted(((namen[t], sum(v) / sek, max(v)) for t, v in th.items()), key=lambda x: -x[1])
        w["threads"] = {
            "ueber_5_prozent": len(th),
            "hoechster_einzelwert_prozent": round(max((x[2] for x in heiss), default=0), 1),
            "heisseste": [{"name": n, "mittel_prozent": round(m, 1), "hoechst_prozent": round(h, 1)} for n, m, h in heiss[:6]],
        }

        az = [z for z in aktiv if von <= z["_t"] <= bis]
        proben = len({z["zeit_utc"] for z in az}) or 1
        zustand = defaultdict(float)
        anweisung = defaultdict(float)
        for z in az:
            if z["rolle"] != "postgres":
                continue
            wg = "CPU" if z["warteart"] == "-" else f'{z["warteart"]}:{z["wartegrund"]}'
            schluessel = z["zustand"] if z["zustand"] != "active" else f"active {wg}"
            zustand[schluessel] += int(z["anzahl"]) / proben
            if z["zustand"] in ("active", "idle in transaction"):
                anweisung[(z["zustand"], z["anweisung"])] += int(z["anzahl"]) / proben
        w["postgres"] = {
            "proben": proben,
            "verbindungen_je_zustand": {k: round(v, 2) for k, v in sorted(zustand.items(), key=lambda kv: -kv[1]) if v >= 0.01},
            "verbindungen_gesamt": round(sum(zustand.values()), 1),
            "aktiv": round(sum(v for k, v in zustand.items() if k.startswith("active")), 2),
            "idle_in_transaction": round(zustand.get("idle in transaction", 0), 2),
            "anweisungen": [{"zustand": k[0], "anweisung": k[1], "mittel": round(v, 2)}
                            for k, v in sorted(anweisung.items(), key=lambda kv: -kv[1])[:8] if v >= 0.05],
        }

        wz = [z for z in wal if von <= z["_t"] <= bis]
        if len(wz) >= 2:
            dt = (wz[-1]["_t"] - wz[0]["_t"]).total_seconds() or 1
            w["wal_je_s"] = {k: round((float(wz[-1][k]) - float(wz[0][k])) / dt, 1) for k in ("wal_records", "wal_bytes", "wal_write", "wal_sync")}

        zz = defaultdict(list)
        for r, t, name, wert in zaehler:
            if r in (rolle, "treiber") and von <= t <= bis:
                zz[f"{r} · {name}"].append(wert)
        w["counters"] = {k: {"mittel": round(mittel(v), 3), "hoechst": round(max(v), 3)} for k, v in sorted(zz.items())}
        erg[a] = w
    return erg


def markdown(ordner, erg):
    e = {}
    try:
        e = json.load(open(os.path.join(ordner, "ergebnis.json"), encoding="utf-8-sig"))
    except Exception:
        pass
    knopf = ""
    kp = os.path.join(ordner, "sonde", "knopf.txt")
    if os.path.exists(kp):
        knopf = open(kp, encoding="utf-8").readline().strip()
    z = [f"# Engpass-Sonde — {os.path.basename(ordner.rstrip('/'))}", "",
         f"Commit `{e.get('commit', '?')}` ({e.get('arbeitsbaum', '?')}), Knopf: **{knopf or 'keiner'}**. CPU in Kernen (1 = ein voller Kern), "
         "Postgres-Verbindungen als Mittel über die 100-ms-Proben (nur die Rolle des Hosts).", ""]
    for a, w in erg.items():
        z.append(f"## {a} ({w['dauer_s']:.1f} s)")
        z.append("")
        z.append("- CPU: " + ", ".join(f"{g} {v}" for g, v in w["cpu_kerne"].items()))
        r = w.get("rechner")
        if r:
            z.append(f"- Rechner: {r['auslastung_prozent']} % ausgelastet, iowait {r['iowait_prozent']} %, CPU-Druck {r['cpu_druck']}, "
                     f"IO-Druck {r['io_druck']}, Kerne über 90 %: {r['kerne_ueber_90_prozent']}; je Kern {r['je_kern_prozent']}")
        t = w["threads"]
        z.append(f"- Host-Threads über 5 %: {t['ueber_5_prozent']}, höchster Einzelwert {t['hoechster_einzelwert_prozent']} %; heißeste: "
                 + ", ".join(f"{x['name']} {x['mittel_prozent']} %" for x in t["heisseste"]))
        p = w["postgres"]
        z.append(f"- Postgres: {p['verbindungen_gesamt']} Verbindungen, davon aktiv {p['aktiv']}, idle in transaction {p['idle_in_transaction']}; "
                 + ", ".join(f"{k} {v}" for k, v in p["verbindungen_je_zustand"].items()))
        for x in p["anweisungen"]:
            z.append(f"  - {x['mittel']} × {x['zustand']}: `{x['anweisung']}`")
        if "wal_je_s" in w:
            v = w["wal_je_s"]
            z.append(f"- WAL je s: {v['wal_records']} Records, {v['wal_bytes'] / 1e6:.1f} MB, {v['wal_write']} Writes, {v['wal_sync']} Syncs")
        wichtig = []
        for k, v in w["counters"].items():
            for name, titel in WICHTIGE_ZAEHLER:
                if name in k:
                    wichtig.append(f"{k.split(' · ')[0]} {titel}{k[k.find(name) + len(name):]}: Mittel {v['mittel']}, höchst {v['hoechst']}")
        if wichtig:
            z.append("- Zähler: " + "; ".join(wichtig))
        z.append("")
    return "\n".join(z)


def main():
    ordner = sys.argv[1]
    erg = werte(ordner)
    sonde = os.path.join(ordner, "sonde")
    with open(os.path.join(sonde, "sonde.json"), "w", encoding="utf-8") as f:
        json.dump(erg, f, ensure_ascii=False, indent=1)
    md = markdown(ordner, erg)
    with open(os.path.join(sonde, "sonde.md"), "w", encoding="utf-8") as f:
        f.write(md + "\n")
    print(md)


if __name__ == "__main__":
    main()
