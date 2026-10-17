#!/usr/bin/env python3
"""Wertet ein CPU-Profil der Engpass-Sonde aus (dotnet-trace, Profil dotnet-sampled-thread-time, als Speedscope-JSON).

  scripts/sonde-profil.py <profil.speedscope.json[.gz]>   → Markdown auf stdout

dotnet-trace tastet alle .NET-Threads ab (~100 Hz). Ein Stack, dessen Spitze .NET-Code ist, rechnet (CPU_TIME); steht der Thread in nativem
Code, steht dort UNMANAGED_CODE_TIME — das sind vor allem Warten (Monitor, Sockets, Threadpool im Leerlauf), aber auch GC und Systemaufrufe.
Gezählt wird deshalb getrennt; die Tabellen der Methoden zählen nur rechnende Stichproben.

Je Stichprobe:
  - Bibliothek: das Modul des innersten Rahmens, der nicht zur Laufzeit (System.*, Microsoft.*, CoreLib) gehört — wer die Arbeit verursacht;
  - Methode selbst: der innerste Rahmen (exklusiv), Methode gesamt: jeder Rahmen auf dem Stack (inklusiv).
"""
import gzip
import json
import sys
from collections import Counter

LAUFZEIT = ("System.", "Microsoft.", "System!", "Microsoft!")


def lade(pfad):
    oeffne = gzip.open if pfad.endswith(".gz") else open
    with oeffne(pfad, "rt", encoding="utf-8") as f:
        return json.load(f)


def stichproben(d):
    """→ Liste (stack als Liste von Rahmen-Namen, Wurzel zuerst; Gewicht in ms) über alle Threads."""
    namen = [f["name"] for f in d["shared"]["frames"]]
    for p in d["profiles"]:
        if p["type"] == "sampled":
            for s, w in zip(p["samples"], p["weights"]):
                yield [namen[i] for i in s], w
        else:   # evented: Öffnen/Schließen von Rahmen; die Zeit zwischen zwei Ereignissen gehört dem aktuellen Stack
            stack, zuletzt = [], None
            for e in p["events"]:
                if zuletzt is not None and stack and e["at"] > zuletzt:
                    yield list(stack), e["at"] - zuletzt
                zuletzt = e["at"]
                if e["type"] == "O":
                    stack.append(namen[e["frame"]])
                elif stack:
                    stack.pop()


def modul(rahmen):
    return rahmen.split("!", 1)[0] if "!" in rahmen else "?"


def kurz(rahmen, n=110):
    r = rahmen.split("!", 1)[-1]
    r = r.split("(", 1)[0]
    return r if len(r) <= n else "…" + r[-n:]


def main():
    d = lade(sys.argv[1])
    gesamt, leaf = 0.0, Counter()
    cpu = []
    for stack, w in stichproben(d):
        gesamt += w
        spitze = stack[-1] if stack else "?"
        leaf[spitze.split("!")[-1] if spitze in ("CPU_TIME", "UNMANAGED_CODE_TIME") else "managed"] += w
        if spitze == "CPU_TIME" or (spitze not in ("UNMANAGED_CODE_TIME",) and "CPU_TIME" not in spitze):
            rahmen = [r for r in stack if r not in ("CPU_TIME", "UNMANAGED_CODE_TIME") and not r.startswith("Thread (")]
            if rahmen:
                cpu.append((rahmen, w))
    cpu_ms = sum(w for _, w in cpu)
    bibl, selbst, inkl = Counter(), Counter(), Counter()
    for rahmen, w in cpu:
        selbst[kurz(rahmen[-1])] += w
        for r in set(kurz(x) for x in rahmen):
            inkl[r] += w
        verursacher = next((modul(r) for r in reversed(rahmen) if not modul(r).startswith(LAUFZEIT) and modul(r) not in ("?", "System")),
                           "Laufzeit allein")
        bibl[verursacher] += w

    z = ["# CPU-Profil des Hosts", "",
         f"Abgetastete Thread-Zeit {gesamt / 1000:.1f} s, davon rechnend (.NET-Code an der Spitze) {cpu_ms / 1000:.1f} s; "
         + ", ".join(f"{k} {v / 1000:.1f} s" for k, v in leaf.most_common()), "",
         "## Rechnende Zeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)", "", "| Bibliothek | Anteil |", "|---|---:|"]
    z += [f"| {k} | {v / cpu_ms * 100:.1f} % |" for k, v in bibl.most_common(20)]
    z += ["", "## Methoden selbst (exklusiv)", "", "| Methode | Anteil |", "|---|---:|"]
    z += [f"| `{k}` | {v / cpu_ms * 100:.1f} % |" for k, v in selbst.most_common(30)]
    z += ["", "## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)", "", "| Methode | Anteil |", "|---|---:|"]
    eigene = [(k, v) for k, v in inkl.most_common() if not k.startswith(("System.", "Microsoft."))]
    z += [f"| `{k}` | {v / cpu_ms * 100:.1f} % |" for k, v in eigene[:45]]
    print("\n".join(z))


if __name__ == "__main__":
    main()
