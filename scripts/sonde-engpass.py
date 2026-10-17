#!/usr/bin/env python3
"""Engpass-Sonde (docs/12 §12.5, docs/13 §13.0): läuft auf dem Messserver NEBEN dem Messtreiber und schreibt Rohdaten in --aus.

Was sie mitschreibt (nur Linux, nur Standardbibliothek):
  cpu_gruppen.csv   alle 0,5 s: CPU je Prozessgruppe (Host je Anwendung, Postgres-Backends, WAL-Writer, übriges Postgres, Treiber,
                    Redis je Container, Consul, docker-proxy) in Prozent EINES Kerns, aus /proc/<pid>/stat (Auflösung 10 ms je Prozess)
  cpu_kerne.csv     alle 0,5 s: Auslastung je logischem Kern (0–100) und gesamt, iowait, CPU- und IO-Druck des Kernels (PSI „some“, Anteil
                    der Zeit, in der mindestens ein Thread auf CPU bzw. IO wartete)
  threads.csv       jede Sekunde: die Threads der Hosts mit mehr als 5 % eines Kerns (Name, CPU) — ein einzelner voller Thread wäre eine
                    Decke, die die Summe nicht zeigt
  pg_aktivitaet.csv alle 100 ms: pg_stat_activity gruppiert nach Datenbank, Zustand, Wartegrund und (bei aktiv / idle in transaction)
                    Anfang der Anweisung — über eine einzige psql-Sitzung im Container
  pg_wal.csv        jede Sekunde: pg_stat_wal (Records, Bytes, Writes, Syncs)
  counters-<rolle>-<pid>.csv  dotnet-counters (System.Runtime, Npgsql) je Host- und Treiber-Prozess, jede Sekunde
  profil.nettrace / profil.speedscope.json  mit --profil <abschnitt>: dotnet-trace (Stacks aller .NET-Threads, ~100 Hz) des Hosts für
                    --profil-dauer Sekunden ab dem Beginn des Abschnitts (der Treiber schreibt ihn in host.csv); Auswertung scripts/sonde-profil.py

Ende per SIGTERM/SIGINT oder nach --dauer Sekunden. Auswertung: scripts/sonde-auswertung.py.
"""
import argparse
import os
import shutil
import signal
import subprocess
import threading
import time
from datetime import datetime, timezone

HZ = os.sysconf("SC_CLK_TCK")
STOP = threading.Event()


def jetzt():
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


def lies(pfad):
    try:
        with open(pfad, "rb") as f:
            return f.read()
    except OSError:
        return None


def container_namen():
    """Container-Id → Name (für Redis: zwei Container mit gleichem Befehl)."""
    try:
        aus = subprocess.run(["docker", "ps", "--no-trunc", "--format", "{{.ID}} {{.Names}}"], capture_output=True, text=True, timeout=10).stdout
        return dict(z.split(" ", 1) for z in aus.strip().splitlines() if " " in z)
    except Exception:
        return {}


class Prozesse:
    """Ordnet PIDs einer Gruppe zu (einmal je PID und Startzeit) und liest ihre CPU-Zeit."""

    def __init__(self):
        self.gruppe = {}      # (pid, start, comm) → gruppe; comm, weil ein exec (Skript → dotnet) die PID behält
        self.container = container_namen()

    def klassifiziere(self, pid, comm):
        cmd = (lies(f"/proc/{pid}/cmdline") or b"").replace(b"\0", b" ").decode(errors="replace")
        if "Host.Pruefdomaene.dll" in cmd:
            return "host-pruefdomaene"
        if "Host.Grpc.dll" in cmd:
            return "host-beispiel"
        if "Cqrs.Vermessung.dll" in cmd:
            return "treiber"
        if comm == "postgres" or cmd.startswith("postgres"):
            if "walwriter" in cmd:
                return "pg-walwriter"
            if cmd.startswith("postgres: vermessung_leser "):
                return "pg-backend-treiber"
            if cmd.startswith("postgres: postgres "):
                return "pg-backend"
            return "pg-sonst"
        if comm.startswith("redis-server"):
            cg = (lies(f"/proc/{pid}/cgroup") or b"").decode(errors="replace")
            for cid, name in self.container.items():
                if cid in cg:
                    return name
            return "redis"
        if comm == "consul":
            return "consul"
        if comm == "docker-proxy":
            return "docker-proxy"
        if comm.startswith("python"):
            return "python"
        if comm == "dotnet":
            return "dotnet-sonst"
        return "sonst"

    def stichprobe(self):
        """→ {pid: (gruppe, ticks)} für alle Prozesse."""
        erg = {}
        for name in os.listdir("/proc"):
            if not name.isdigit():
                continue
            roh = lies(f"/proc/{name}/stat")
            if not roh:
                continue
            s = roh.decode(errors="replace")
            rechts = s.rfind(")")
            comm = s[s.find("(") + 1:rechts]
            felder = s[rechts + 2:].split()
            # Felder ab state (Index 0): utime = 11, stime = 12, starttime = 19
            ticks = int(felder[11]) + int(felder[12])
            pid = int(name)
            schluessel = (pid, felder[19], comm)
            g = self.gruppe.get(schluessel)
            if g is None:
                g = self.gruppe[schluessel] = self.klassifiziere(pid, comm)
            erg[pid] = (g, ticks)
        return erg


def kern_zeiten():
    zeilen = (lies("/proc/stat") or b"").decode().splitlines()
    erg = {}
    for z in zeilen:
        if not z.startswith("cpu"):
            break
        t = z.split()
        werte = list(map(int, t[1:9]))
        idle = werte[3] + werte[4]
        erg[t[0]] = (sum(werte), idle, werte[4])
    return erg


def psi(art):
    roh = (lies(f"/proc/pressure/{art}") or b"").decode()
    for z in roh.splitlines():
        if z.startswith("some"):
            return int(z.split("total=")[1])
    return 0


def cpu_schleife(aus, takt):
    pz = Prozesse()
    kerne_n = os.cpu_count()
    with open(os.path.join(aus, "cpu_gruppen.csv"), "w") as fg, open(os.path.join(aus, "cpu_kerne.csv"), "w") as fk:
        fg.write("zeit_utc,gruppe,cpu_prozent,prozesse\n")
        fk.write("zeit_utc,gesamt_prozent,iowait_prozent,psi_cpu_some,psi_io_some," + ",".join(f"k{i}" for i in range(kerne_n)) + "\n")
        vorher = pz.stichprobe()
        k_vorher = kern_zeiten()
        p_vorher = (psi("cpu"), psi("io"))
        t_vorher = time.monotonic()
        while not STOP.wait(takt):
            jetzt_p = pz.stichprobe()
            k_jetzt = kern_zeiten()
            p_jetzt = (psi("cpu"), psi("io"))
            t = time.monotonic()
            dt = t - t_vorher
            z = jetzt()
            summe, anzahl = {}, {}
            for pid, (g, ticks) in jetzt_p.items():
                alt = vorher.get(pid)
                d = ticks - alt[1] if alt and alt[0] == g else 0
                summe[g] = summe.get(g, 0) + d
                anzahl[g] = anzahl.get(g, 0) + 1
            for g in sorted(summe):
                fg.write(f"{z},{g},{summe[g] / HZ / dt * 100:.1f},{anzahl[g]}\n")

            def last(name):
                a, b = k_vorher[name], k_jetzt[name]
                ges = b[0] - a[0]
                return (100.0 * (ges - (b[1] - a[1])) / ges if ges else 0.0), (100.0 * (b[2] - a[2]) / ges if ges else 0.0)

            gesamt, iow = last("cpu")
            je = [last(f"cpu{i}")[0] for i in range(kerne_n)]
            fk.write(f"{z},{gesamt:.1f},{iow:.1f},{(p_jetzt[0] - p_vorher[0]) / 1e6 / dt:.3f},{(p_jetzt[1] - p_vorher[1]) / 1e6 / dt:.3f},"
                     + ",".join(f"{x:.0f}" for x in je) + "\n")
            vorher, k_vorher, p_vorher, t_vorher = jetzt_p, k_jetzt, p_jetzt, t
            fg.flush()
            fk.flush()


def host_pids():
    erg = {}
    for name in os.listdir("/proc"):
        if not name.isdigit():
            continue
        cmd = (lies(f"/proc/{name}/cmdline") or b"").replace(b"\0", b" ").decode(errors="replace")
        if "Host.Pruefdomaene.dll" in cmd:
            erg[int(name)] = "host-pruefdomaene"
        elif "Host.Grpc.dll" in cmd:
            erg[int(name)] = "host-beispiel"
        elif "Cqrs.Vermessung.dll" in cmd:
            erg[int(name)] = "treiber"
    return erg


def thread_schleife(aus):
    """Je Host-Thread die CPU der letzten Sekunde; nur Threads über 5 % eines Kerns."""
    vorher = {}
    with open(os.path.join(aus, "threads.csv"), "w") as f:
        f.write("zeit_utc,rolle,pid,tid,name,cpu_prozent\n")
        t_vorher = time.monotonic()
        while not STOP.wait(1.0):
            t = time.monotonic()
            dt = t - t_vorher
            z = jetzt()
            jetzt_t = {}
            for pid, rolle in host_pids().items():
                if rolle == "treiber":
                    continue
                try:
                    tids = os.listdir(f"/proc/{pid}/task")
                except OSError:
                    continue
                for tid in tids:
                    roh = lies(f"/proc/{pid}/task/{tid}/stat")
                    if not roh:
                        continue
                    s = roh.decode(errors="replace")
                    rechts = s.rfind(")")
                    name = s[s.find("(") + 1:rechts].replace(",", " ")
                    felder = s[rechts + 2:].split()
                    ticks = int(felder[11]) + int(felder[12])
                    jetzt_t[(pid, tid)] = ticks
                    alt = vorher.get((pid, tid))
                    if alt is not None:
                        proz = (ticks - alt) / HZ / dt * 100
                        if proz > 5:
                            f.write(f"{z},{rolle},{pid},{tid},{name},{proz:.1f}\n")
            vorher, t_vorher = jetzt_t, t
            f.flush()


AKTIVITAET = r"""SELECT coalesce(datname,'-'), coalesce(usename,'-'), coalesce(state,'-'), coalesce(wait_event_type,'-'), coalesce(wait_event,'-'),
  CASE WHEN state IN ('active','idle in transaction') THEN left(regexp_replace(query, '\s+', ' ', 'g'), 70) ELSE '' END, count(*)
FROM pg_stat_activity WHERE backend_type = 'client backend' AND pid <> pg_backend_pid() GROUP BY 1,2,3,4,5,6;"""
WAL = "SELECT wal_records, wal_bytes, wal_write, wal_sync FROM pg_stat_wal;"


def pg_schleife(aus, takt):
    psql = subprocess.Popen(["docker", "exec", "-i", "cqrs-postgres", "psql", "-U", "postgres", "-d", "postgres", "-At", "-F", "\t", "-q",
                             "-v", "ON_ERROR_STOP=0"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, bufsize=1)

    def frage(sql):
        psql.stdin.write(sql + "\nSELECT '#ENDE';\n")
        psql.stdin.flush()
        zeilen = []
        while True:
            z = psql.stdout.readline()
            if not z or z.strip() == "#ENDE":
                return zeilen
            zeilen.append(z.rstrip("\n"))

    with open(os.path.join(aus, "pg_aktivitaet.csv"), "w") as fa, open(os.path.join(aus, "pg_wal.csv"), "w") as fw:
        fa.write("zeit_utc\tdatenbank\trolle\tzustand\twarteart\twartegrund\tanweisung\tanzahl\n")
        fw.write("zeit_utc,wal_records,wal_bytes,wal_write,wal_sync\n")
        letzte_wal = 0.0
        while not STOP.is_set():
            beginn = time.monotonic()
            z = jetzt()
            for zeile in frage(AKTIVITAET):
                fa.write(f"{z}\t{zeile}\n")
            if beginn - letzte_wal >= 1.0:
                for zeile in frage(WAL):
                    fw.write(z + "," + zeile.replace("\t", ",") + "\n")
                letzte_wal = beginn
                fa.flush()
                fw.flush()
            STOP.wait(max(0.0, takt - (time.monotonic() - beginn)))
    try:
        psql.stdin.close()
        psql.wait(5)
    except Exception:
        psql.kill()


def counters_schleife(aus):
    """Startet dotnet-counters für jeden neuen Host- und Treiber-Prozess (endet von selbst mit dem Prozess)."""
    werkzeug = shutil.which("dotnet-counters") or os.path.expanduser("~/.dotnet/tools/dotnet-counters")
    if not os.path.exists(werkzeug):
        open(os.path.join(aus, "counters-fehlt.txt"), "w").write("dotnet-counters nicht gefunden\n")
        return
    gestartet, kinder = set(), []
    while not STOP.wait(1.0):
        for pid, rolle in host_pids().items():
            if pid in gestartet:
                continue
            gestartet.add(pid)
            zaehler = "System.Runtime" if rolle == "treiber" else "System.Runtime,Npgsql"
            kinder.append(subprocess.Popen([werkzeug, "collect", "--process-id", str(pid), "--counters", zaehler, "--refresh-interval", "1",
                                            "--format", "csv", "--output", os.path.join(aus, f"counters-{rolle}-{pid}.csv")],
                                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL))
    for k in kinder:
        if k.poll() is None:
            k.send_signal(signal.SIGINT)
    for k in kinder:
        try:
            k.wait(10)
        except subprocess.TimeoutExpired:
            k.kill()


HORIZONT_ABFRAGEN = [
    # (Name, SQL mit {kopf}; jede als EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON))
    ("exists_nah_eigen", "select exists(select 1 from es.mt_events where seq_id > {kopf} - 50)"),
    ("exists_weit_eigen", "select exists(select 1 from es.mt_events where seq_id > {kopf} - 2000)"),
    ("exists_nah_allgemein", "execute h_exists({kopf} - 50)"),
    ("exists_weit_allgemein", "execute h_exists({kopf} - 2000)"),
    ("snapshot_xmin", "select pg_snapshot_xmin(pg_current_snapshot())::text"),
    ("zaehlerstand", "select case when is_called then last_value else 0 end from es.mt_events_sequence"),
]


def horizont_schleife(aus, db):
    """Diagnose PF-17: jede Sekunde die Teile der Horizont-Probe einzeln, mit EXPLAIN ANALYZE, gegen die echte DB unter Last. Der Treiber
    löscht die DB beim Start (die Sitzung endet) — dann verbindet sich die Schleife neu."""
    psql = None

    def verbinde():
        return subprocess.Popen(["docker", "exec", "-i", "cqrs-postgres", "psql", "-U", "postgres", "-d", db, "-At", "-q", "-v", "ON_ERROR_STOP=0"],
                                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)

    def frage(sql):
        nonlocal psql
        try:
            if psql is None or psql.poll() is not None:
                psql = verbinde()
            psql.stdin.write(sql + "\nSELECT '#ENDE';\n")
            psql.stdin.flush()
        except (BrokenPipeError, OSError):
            psql = None
            return ""
        zeilen = []
        while True:
            z = psql.stdout.readline()
            if not z:
                psql = None
                return ""
            if z.strip() == "#ENDE":
                return "\n".join(zeilen)
            zeilen.append(z.rstrip("\n"))

    import json as _json
    with open(os.path.join(aus, "horizont.csv"), "w") as f:
        f.write("zeit_utc,kopf,abfrage,ms,knoten,puffer_hit,puffer_read\n")
        vorbereitet = False
        while not STOP.wait(1.0):
            war = psql
            kopf = frage("select coalesce(max(seq_id), 0) from es.mt_events;").strip().splitlines()
            if psql is not war or not kopf or not kopf[-1].lstrip("-").isdigit():
                vorbereitet = False   # neue Sitzung, DB neu angelegt oder noch ohne Tabelle
                continue
            kopf = int(kopf[-1])
            if not vorbereitet:
                frage("deallocate all; set plan_cache_mode = force_generic_plan; "
                      "prepare h_exists(bigint) as select exists(select 1 from es.mt_events where seq_id > $1);")
                vorbereitet = True
            z = jetzt()
            for name, sql in HORIZONT_ABFRAGEN:
                roh = frage("explain (analyze, buffers, format json) " + sql.format(kopf=kopf) + ";")
                try:
                    plan = _json.loads(roh[roh.index("["):])[0]
                except (ValueError, IndexError):
                    continue
                def knoten(n):
                    return n["Node Type"] + ("(" + "+".join(knoten(k) for k in n.get("Plans", [])) + ")" if n.get("Plans") else "")
                p = plan["Plan"]
                f.write(f"{z},{kopf},{name},{plan.get('Execution Time', 0):.3f},{knoten(p)},{p.get('Shared Hit Blocks', 0)},"
                        f"{p.get('Shared Read Blocks', 0)}\n")
            # eigene Transaktionsnummer (wie die Probe: in einer Transaktion, die damit eine Nummer bekommt)
            t0 = time.monotonic()
            frage("begin; select pg_current_xact_id(); rollback;")
            f.write(f"{z},{kopf},xact_id_rundlauf,{(time.monotonic() - t0) * 1000:.3f},psql-Rundlauf,0,0\n")
            f.flush()
    try:
        if psql is not None:
            psql.stdin.close()
            psql.wait(5)
    except Exception:
        psql.kill()


def profil_schleife(aus, ergebnisse, abschnitt, dauer):
    """Wartet, bis der Treiber den Abschnitt beginnt (neuester Ergebnis-Ordner, host.csv), und zeichnet dann den Host auf."""
    werkzeug = shutil.which("dotnet-trace") or os.path.expanduser("~/.dotnet/tools/dotnet-trace")
    beginn = time.time()
    while not STOP.wait(0.2):
        try:
            ordner = [os.path.join(ergebnisse, d) for d in os.listdir(ergebnisse)]
            ordner = [d for d in ordner if os.path.getmtime(d) >= beginn - 5 and os.path.exists(os.path.join(d, "host.csv"))]
        except OSError:
            continue
        if not ordner:
            continue
        csvdatei = os.path.join(max(ordner, key=os.path.getmtime), "host.csv")
        with open(csvdatei, encoding="utf-8-sig") as f:
            if not any(f",{abschnitt}," in z for z in f):
                continue
        rolle = "host-pruefdomaene" if abschnitt.startswith(("S5", "L5")) else "host-beispiel"
        pid = next((p for p, r in host_pids().items() if r == rolle), None)
        if pid is None:
            continue
        with open(os.path.join(aus, "profil.txt"), "w") as f:
            f.write(f"{abschnitt} {rolle} {pid} ab {jetzt()} für {dauer} s\n")
        subprocess.run([werkzeug, "collect", "--process-id", str(pid), "--profile", "dotnet-common,dotnet-sampled-thread-time",
                        "--duration", f"00:00:00:{int(dauer):02d}", "--format", "Speedscope", "--output", os.path.join(aus, "profil.nettrace")],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL, timeout=dauer + 120)
        return


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--aus", required=True)
    ap.add_argument("--dauer", type=float, default=0, help="Sekunden; 0 = bis SIGTERM")
    ap.add_argument("--profil", default="", help="Abschnitt des Treibers (z. B. S5-8-w1), ab dessen Beginn der Host aufgezeichnet wird")
    ap.add_argument("--profil-dauer", type=int, default=int(os.environ.get("SONDE_PROFIL_DAUER", "15")))
    ap.add_argument("--ergebnisse", default="vermessung/ergebnisse")
    ap.add_argument("--horizont", default="", help="DB, in der die Teile der Horizont-Probe jede Sekunde gemessen werden (PF-17)")
    a = ap.parse_args()
    os.makedirs(a.aus, exist_ok=True)
    # dotnet-counters schreibt Ortszeit ohne Zone — die Auswertung rechnet mit diesem Versatz nach UTC um.
    with open(os.path.join(a.aus, "uhr.txt"), "w") as f:
        f.write(f"{int(datetime.now().astimezone().utcoffset().total_seconds())}\n")
    for s in (signal.SIGTERM, signal.SIGINT):
        signal.signal(s, lambda *_: STOP.set())
    faeden = [threading.Thread(target=cpu_schleife, args=(a.aus, 0.5)), threading.Thread(target=thread_schleife, args=(a.aus,)),
              threading.Thread(target=pg_schleife, args=(a.aus, 0.1)), threading.Thread(target=counters_schleife, args=(a.aus,))]
    if a.horizont:
        faeden.append(threading.Thread(target=horizont_schleife, args=(a.aus, a.horizont)))
    if a.profil:
        faeden.append(threading.Thread(target=profil_schleife, args=(a.aus, a.ergebnisse, a.profil, a.profil_dauer)))
    for f in faeden:
        f.daemon = True
        f.start()
    beginn = time.monotonic()
    while not STOP.wait(0.5):
        if a.dauer and time.monotonic() - beginn >= a.dauer:
            STOP.set()
    for f in faeden:
        f.join(150 if a.profil else 20)


if __name__ == "__main__":
    main()
