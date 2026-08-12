# Prompt für den nächsten Agenten — Schreibpfad-Performance (Fortsetzung)

> Kopiere den Block unten als Startprompt für eine neue Session. Voller Kontext des bisherigen
> Vorgehens: `docs/backend-perf-untersuchung-bericht.md`.

---

## Auftrag

Wir optimieren den **Command-Schreibpfad** eines selbstgebauten CQRS/Event-Sourcing-Frameworks
(Proto.Actor + Marten/Postgres + Redis). Der bisherige Durchbruch: der Schreibpfad ist
**commit-gebunden**, und ein **paralleler Commit-Drain** (`AppendDrainParallelism`, Default 4) hat den
Durchsatz **+48 %** gebracht (3249 → 4805 msg/s, Exactly-once verifiziert). Alles ist auf **`main`**.

**Dein nächster Schritt (empfohlen, billig, hoher Erkenntniswert):** Der parallele Drain skaliert
**sub-linear** (2 Streams → 1,57×, 4 → 1,5×). Ein geteilter Serialisierungspunkt im Postgres-Schreibpfad
kappt ihn. **Löse auf, WOGEGEN die parallelen Drains warten** — dann wissen wir, ob mehr Parallelität auf
Produktions-Hardware weiter trägt, ob ein Postgres-Tuning den Punkt löst, oder ob erst COPY hilft.

### Konkret
1. Nachhaltige Last fahren (`--mode aggregate --accounts 2500 --credits 40 --concurrency 128`, `DrainPar` = 4).
2. Während der aktiven Phase wiederholt samplen:
   `SELECT wait_event_type, wait_event, count(*) FROM pg_stat_activity WHERE state='active' AND backend_type='client backend' GROUP BY 1,2 ORDER BY 3 DESC;`
   (jede ~0,5–1 s, ~15–20 Samples aggregieren).
3. Interpretieren:
   - **`LWLock` / `WALWrite` / `WALInsert`** dominant → WAL-Insert-Serialisierung ist die Wand.
     Probiere Postgres-Tuning (`wal_buffers`, `commit_siblings`/`commit_delay`, `max_wal_size`) und miss.
   - **`Lock` (row/relation)** → unerwartete Contention (Sequence? mt_streams?) — untersuchen.
   - **`Client`/`IO` gering, viel idle** → noch Headroom: `DrainPar`-Sweep höher treiben (8/12/16) auf
     dieser oder größerer Hardware messen.
4. Entscheiden & dokumentieren: skaliert K weiter (→ Default anheben/tunen), oder ist der DB-Write selbst
   die Wand (→ **COPY-Event-Store**, Weg 2, wird relevant; der reflection-freie JSON-Baustein
   `GeneratedEventJson` steht schon).

### Danach (Rangfolge, nur wenn (1) es rechtfertigt)
- `DrainPar` pro Deployment tunen (mehr Kerne / remote-Postgres → höheres K).
- **COPY-Event-Store** (eigene Tabelle + `UNIQUE(stream,version)`): senkt die ~0,11 ms/Event-Insert-Kosten;
  ~2 Wochen + Ownership. Analyse-Grundlage im Bericht + früheren Handoffs.
- Remote-Redis-Detach — **nur** falls produktiv remote-Redis (localhost: 0 Gewinn, gemessen).

---

## Was schon erledigt ist (nicht wiederholen)

Vermessen **bevor** gebaut wurde — drei Kandidaten aussortiert, einer geliefert:

| Kandidat | Befund | Status |
|---|---|---|
| STJ-Source-Gen-Serializer | Serialisierung = 0,04 % der Kosten → 0 % end-to-end (obwohl roh 2,48× schneller) | verworfen; Baustein `GeneratedEventJson` bleibt (für COPY) |
| Redis-sync im Turn detachen | ~0 % Durchsatz **und** Latenz (localhost); nur remote-Redis relevant | nicht gebaut |
| **Paralleler Commit-Drain** | **+48 %**, Exactly-once verifiziert | **geliefert, Default 4** |
| DB-Profil | 97 % Wall = serielle `SaveChangesAsync`; NICHT fsync (sync_commit=off egal), NICHT CPU (~80 % idle) → **commit-WAIT-gebunden** | Grundlage des nächsten Schritts |

---

## Reproduktion / Werkzeuge

**Infra hochfahren** (nativ, kein Docker; Consul stirbt gern → vor jedem Lauf prüfen):
```bash
pg_ctlcluster 16 main start; redis-server --daemonize yes --bind 127.0.0.1 --port 6379
pgrep -x consul || (nohup consul agent -dev -client=0.0.0.0 >/tmp/consul.log 2>&1 & disown); sleep 8
curl -sf http://localhost:8500/v1/status/leader   # muss antworten
export DOTNET_ROLL_FORWARD=LatestMajor             # .NET 10 SDK baut/läuft net9.0
```
DB-Reset zwischen Läufen: `DROP SCHEMA IF EXISTS es CASCADE; rm CASCADE; dlq CASCADE;` (Marten legt neu an).

**LoadHarness** (druckt Durchsatz, Latenz, Group-Commit-Profil, Exactly-once-Check):
```bash
dotnet run -c Release --project LoadHarness --no-build -- --mode aggregate --accounts 500 --credits 40 --concurrency 128 --log warning
```
**Env-Toggles** (A/B ohne Rebuild): `BRACTOR_DRAIN_PAR=k`, `BRACTOR_BATCH_MAX=n`, `BRACTOR_BATCH_LINGER=ms`,
`BRACTOR_SOURCEGEN_JSON=1`, `BRACTOR_VERSION_TRACKING=0`. Serializer-Mikrobench: `--mode serbench --iters 1000000`.

**Tests:** Prüfstand `dotnet test Infrastructure.Pruefstand.Tests/...` (in-memory, schnell, muss grün).
Integration `dotnet test Infrastructure.Integration.Tests/...` (braucht Infra, **sequenziell**,
~1–2 min; bekannter SnapshotLive-Cold-Boot-Flake existiert — nicht an Timeouts drehen).

**Schlüsseldateien:** `Infrastructure/Persistence/BatchingEventAppender.cs` (paralleler Drain),
`MartenEventBatchWriter.cs` (+ `BatchWriterStats`), `Infrastructure/Extensions/CqrsServiceExtension.cs`
(`AppendDrainParallelism` u. a. Flags), `LoadHarness/Program.cs` (Bench + Toggles + Profil-Ausgabe).

---

## Leitplanken (nicht verletzen)

- **Messen VOR Bauen.** Jeder Perf-Kandidat wird isoliert vermessen (A/B-Toggle), bevor Aufwand fließt.
  Verteilte/Timing-Effekte in-memory bzw. mit Fake-Cluster beweisen, nicht im langsamen Integrationstest raten.
- **Sechs Invarianten** (siehe `CLAUDE.md`): Wahrheit ist der Log; Routing über Typen; keine Runtime-Reflection
  (alles generiert); Fachcode bleibt rein; Exactly-once ist nie Default. Der parallele Drain ist sicher, weil
  **Single-Activation** garantiert, dass ein Stream nie gleichzeitig in zwei Batches liegt — diese Eigenschaft
  bei jeder Änderung wahren.
- **Bench-Pfad:** `--mode aggregate` nutzt `Modus.Emittiert` → 2 Events/Command (Domain + Inbox-Marke).
  Client-OCC-Commands schreiben 1 Event → real höher. Beim Interpretieren beachten.
- **Git:** Entwicklung auf `main` ist freigegeben (der Nutzer pusht am Ende bewusst dorthin). Erst committen/
  pushen, wenn Prüfstand + Integration grün sind und der Nutzer es will. Commit-Messages mit Co-Authored-By-Footer.
- **Ehrlich bleiben:** negative Ergebnisse (kein Effekt) sind genauso wertvoll wie positive — berichten, nicht verstecken.
