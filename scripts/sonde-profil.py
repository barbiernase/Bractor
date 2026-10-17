#!/usr/bin/env python3
"""Wertet ein CPU-Profil der Engpass-Sonde aus (dotnet-trace, Profil dotnet-sampled-thread-time, als Speedscope-JSON).

  scripts/sonde-profil.py <profil.speedscope.json[.gz]>   → Markdown auf stdout

dotnet-trace tastet alle .NET-Threads ab (~100 Hz); jede Stichprobe öffnet in der Speedscope-Datei („evented“) einen Spitzen-Rahmen CPU_TIME
oder UNMANAGED_CODE_TIME — gezählt werden diese Öffnungen (je Stichprobe ≈ 10 ms eines Threads). Auf Linux ist fast jede Stichprobe als
UNMANAGED_CODE_TIME markiert, auch mitten in Marten; Rechnen und Warten trennt deshalb der innerste .NET-Rahmen: steht er in einer
Warte-Methode (Semaphore, Monitor, Ereignis, Schlaf, Socket-Ereignisschleife), wartet der Thread, sonst arbeitet er (rechnet oder steht in
einem Systemaufruf bzw. einer Sperre der Laufzeit — „arbeitend“ ist also eine Obergrenze der CPU).

Je Stichprobe:
  - Bibliothek: das Modul des innersten Rahmens, der nicht zur Laufzeit (System.*, Microsoft.*, CoreLib) gehört — wer die Arbeit verursacht;
  - Methode selbst: der innerste Rahmen (exklusiv), Methode gesamt: jeder Rahmen auf dem Stack (inklusiv).
"""
import gzip
import json
import sys
from collections import Counter

LAUFZEIT = ("System.", "Microsoft.", "System!", "Microsoft!")
WARTEN = ("LowLevelLifoSemaphore.Wait", "Monitor.Wait", "WaitHandle.WaitOne", "ManualResetEventSlim.Wait", "SemaphoreSlim.Wait", "Thread.Sleep",
          "WaitForSocketEvents", "SocketAsyncEngine.EventLoop", "DedicatedThreadPoolPipeScheduler.RunWorkLoop", "TimerQueue.TimerThread",
          "ThreadPoolWorkerThreadWait", "Heartbeat.TimerLoop", "CounterGroup.PollForValues", "GC.RunFinalizers", "Interop+Sys.Read",
          "WorkerThread.WorkerThreadStart", "Task.SpinThenBlockingWait", "Task.InternalWait")


def lade(pfad):
    oeffne = gzip.open if pfad.endswith(".gz") else open
    with oeffne(pfad, "rt", encoding="utf-8") as f:
        return json.load(f)


def stichproben(d):
    """→ (stack als Liste von Rahmen-Namen, Wurzel zuerst, Spitze CPU_TIME bzw. UNMANAGED_CODE_TIME; Gewicht 1 = eine Stichprobe)."""
    namen = [f["name"] for f in d["shared"]["frames"]]
    for p in d["profiles"]:
        if p["type"] == "sampled":
            for s in p["samples"]:
                yield [namen[i] for i in s], 1
        else:   # evented: jede Stichprobe öffnet ihren Spitzen-Rahmen (CPU_TIME / UNMANAGED_CODE_TIME) neu
            stack = []
            for e in p["events"]:
                if e["type"] == "O":
                    stack.append(namen[e["frame"]])
                    if stack[-1] in ("CPU_TIME", "UNMANAGED_CODE_TIME"):
                        yield list(stack), 1
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
        leaf[spitze] += w
        # ohne die Wurzel-Rahmen der Aufzeichnung (Prozess, „(Non-Activities)“, „Threads“, „Thread (…)“)
        rahmen = [r for r in stack[:-1] if "!" in r]
        if rahmen and not any(m in rahmen[-1] for m in WARTEN):
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
         f"Stichproben {gesamt:.0f} (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) {cpu_ms:.0f} "
         f"≈ {cpu_ms / 100:.1f} Thread-Sekunden; Markierung: " + ", ".join(f"{k} {v:.0f}" for k, v in leaf.most_common(3)), "",
         "Die Anteile beziehen sich auf die arbeitenden Stichproben.", "",
         "## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)", "", "| Bibliothek | Anteil |", "|---|---:|"]
    z += [f"| {k} | {v / cpu_ms * 100:.1f} % |" for k, v in bibl.most_common(20)]
    z += ["", "## Methoden selbst (exklusiv)", "", "| Methode | Anteil |", "|---|---:|"]
    z += [f"| `{k}` | {v / cpu_ms * 100:.1f} % |" for k, v in selbst.most_common(30)]
    z += ["", "## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)", "", "| Methode | Anteil |", "|---|---:|"]
    eigene = [(k, v) for k, v in inkl.most_common() if not k.startswith(("System.", "Microsoft."))]
    z += [f"| `{k}` | {v / cpu_ms * 100:.1f} % |" for k, v in eigene[:45]]
    print("\n".join(z))


if __name__ == "__main__":
    main()
