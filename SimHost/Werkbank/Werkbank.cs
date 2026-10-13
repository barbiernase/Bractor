using System.Security.Cryptography;
using System.Text;
using Abstractions;

namespace SimHost.Werkbank;

/// <summary>Ein Schritt der ⟳-Kette mit Ergebnis.</summary>
public sealed record Schritt(string Name, bool Ok, long DauerMs, IReadOnlyList<string> Meldungen);

/// <summary>Das Ergebnis von ⟳: Schritte, Compiler-/Analyzer-Befunde (an Karten), Boot-Fehler, ggf. eine Rückfrage (Sim-DB zurücksetzen).</summary>
public sealed record KreislaufErgebnis(bool Ok, string Stand, IReadOnlyList<Schritt> Schritte, IReadOnlyList<Diagnose> Diagnosen,
    IReadOnlyList<BootMeldung> Boot, IReadOnlyList<string>? Rueckfrage, DateTimeOffset Zeit);

/// <summary>
/// Der Werkbank-Server (docs/konzept-werkbank.md §5, Werkbank P3): ⟳ = Bauen (Codegen-Prepass, Proto, Generatoren, Analyzer) → Python-Basen
/// → Board neu lesen → Daten prüfen (Sim-DB) → Infrastruktur → Host ersetzen. Je ein Weg; ein Fehler steht an der Karte, die ihn hat.
/// </summary>
public sealed class Werkbank
{
    private readonly string _root;
    private readonly SemaphoreSlim _einLauf = new(1, 1);
    public Umgebungen Umgebungen { get; }
    public HostProzess Host { get; }
    public Infra Infra { get; }
    public SimDatenbank Db { get; } = new();
    public Fortschritt Fortschritt { get; } = new();
    /// <summary>Der warme Workspace (P7.1): das gelesene Board, die Karten der Befunde, „Neu lesen“.</summary>
    public Arbeitsbereich Arbeitsbereich { get; }
    /// <summary>Geste → Patch (P7.2): jede Bearbeitung im Editor ist eine Änderung am Code, mit ↶.</summary>
    public GestenDienst Gesten { get; }
    public KreislaufErgebnis? Letzter { get; private set; }
    /// <summary>Der Stand, mit dem der laufende Host gebaut wurde („● Entwurf ≠ Lauf", §5).</summary>
    public string? LaufStand { get; private set; }
    /// <summary>Inhalt der Umgebungs-Datei beim Start des laufenden Hosts — geändert heißt: ⟳ übernimmt die neue Konfig.</summary>
    private string? _laufUmgebung;
    private string? DateiInhalt(string name) => File.Exists(Umgebungen.Datei(name)) ? File.ReadAllText(Umgebungen.Datei(name)) : null;
    /// <summary>Läuft gerade ⟳ (bzw. eine Sim-DB-Aktion) — und welcher Schritt.</summary>
    public string? Laeuft { get; private set; }

    public Werkbank(string slnRoot)
    {
        _root = slnRoot;
        Umgebungen = new Umgebungen(slnRoot);
        Host = new HostProzess(slnRoot);
        Infra = new Infra(slnRoot);
        Arbeitsbereich = new Arbeitsbereich(slnRoot, Fortschritt.Melde);
        Gesten = new GestenDienst(slnRoot, Arbeitsbereich, new Verlauf(slnRoot, Umgebungen.Branch), Umgebungen.Branch, Fortschritt.Melde);
    }

    private string AktivDatei => Path.Combine(_root, ".werkbank", "aktiv");

    /// <summary>Die gewählte Umgebung (Kopfzeile „Umgebung ▾"); Default die erste Sim-Umgebung.</summary>
    public string Aktiv
    {
        get
        {
            var namen = Umgebungen.Namen();
            var gespeichert = File.Exists(AktivDatei) ? File.ReadAllText(AktivDatei).Trim() : null;
            return gespeichert != null && namen.Contains(gespeichert) ? gespeichert
                : namen.OrderBy(n => Rang(n)).FirstOrDefault() ?? "";
            // Default: die lokal lauffähige Sim-Umgebung (Einzelknoten, verwaltete DB) vor Multi-Node-/Container-Umgebungen und Prod.
            int Rang(string n)
            {
                try
                {
                    var u = Umgebungen.Aufgeloest(n);
                    return (u.Umgebung.Art == UmgebungsArt.Sim ? 0 : 4) + (u.Cluster.Art == ClusterArt.Einzelknoten ? 0 : 2) + (u.EventStore.Betrieb == Betrieb.Verwaltet ? 0 : 1);
                }
                catch { return 9; }
            }
        }
        set
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AktivDatei)!);
            File.WriteAllText(AktivDatei, value);
        }
    }

    /// <summary>Der Stand: Hash über den Code der Editor-Projekte — Board, Build und Lauf tragen denselben (Spur, P4).</summary>
    public string Stand()
    {
        using var sha = SHA256.Create();
        foreach (var projekt in new[] { "Domain", "Domain.Pipeline", "Domain.Projections", "Domain.Infrastructure" })
        {
            var verz = Path.Combine(_root, projekt);
            if (!Directory.Exists(verz)) continue;
            foreach (var d in Directory.EnumerateFiles(verz, "*.cs", SearchOption.AllDirectories)
                         .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                         .OrderBy(d => d, StringComparer.Ordinal))
            {
                var kopf = Encoding.UTF8.GetBytes(Path.GetRelativePath(_root, d) + "\0");
                sha.TransformBlock(kopf, 0, kopf.Length, null, 0);
                var inhalt = File.ReadAllBytes(d);
                sha.TransformBlock(inhalt, 0, inhalt.Length, null, 0);
            }
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!)[..8].ToLowerInvariant();
    }

    private string NeuesProto => Path.Combine(_root, "ProtoRepo", "domain.proto");

    private void Schritt(List<Schritt> schritte, Schritt s)
    {
        schritte.Add(s);
        Fortschritt.Melde("schritt", s);
    }

    private void Beginne(string schritt)
    {
        Laeuft = schritt;
        Fortschritt.Melde("beginnt", new { schritt });
    }

    public object Zustand()
    {
        var aktiv = Aktiv;
        UmgebungKonfig? u = null;
        string? fehler = null;
        try { if (aktiv.Length > 0) u = Umgebungen.Aufgeloest(aktiv); }
        catch (Exception e) { fehler = e.Message; }
        return new
        {
            umgebungen = Umgebungen.Namen(), aktiv, branch = Umgebungen.Branch(), fehler,
            art = u?.Umgebung.Art.ToString(), datenbank = u?.EventStore.Datenbank, cluster = u?.Cluster.Name,
            ports = u == null ? null : new { grpc = u.Transport.GrpcPort, einblick = u.Transport.EinblickPort },
            infra = u == null ? null : Infra.Dienste(u), host = Host.Zustand(), stand = Stand(), laufStand = Host.Laeuft ? LaufStand : null, laeuft = Laeuft,
            umgebungGeaendert = Host.Laeuft && _laufUmgebung != null && _laufUmgebung != DateiInhalt(aktiv),
            // Build-Status = der Schritt „Bauen" (nicht die ganze Kette: ein Boot-Fehler ist kein roter Build).
            build = Letzter == null ? null : new
            {
                ok = Letzter.Schritte.FirstOrDefault(x => x.Name == "Bauen")?.Ok ?? false, Letzter.Stand, Letzter.Zeit,
                fehler = Letzter.Diagnosen.Count(d => d.Schwere == "error"),
            },
        };
    }

    /// <summary>⟳ — die ganze Kette. <paramref name="zuruecksetzen"/>: eine unverträgliche Sim-DB wird ohne Rückfrage zurückgesetzt.</summary>
    public async Task<KreislaufErgebnis> KreislaufAsync(bool zuruecksetzen, CancellationToken ct)
    {
        await _einLauf.WaitAsync(ct);
        try
        {
            var schritte = new List<Schritt>();
            var stand = Stand();
            Fortschritt.Melde("kreislauf", new { stand });
            KreislaufErgebnis Ende(bool ok, IReadOnlyList<Diagnose>? d = null, IReadOnlyList<BootMeldung>? boot = null, IReadOnlyList<string>? frage = null)
            {
                // Zu den Compiler-Befunden die Lese-Befunde (was das Lesen nicht fassen konnte, z. B. ein unaufgelöster Basistyp). Der Bau
                //   ersetzt die Befunde, die Gesten seit dem letzten ⟳ an ihren Dateien gesammelt haben.
                if (schritte.Any(x => x.Name == "Bauen")) Gesten.BefundeLeeren();
                Letzter = new KreislaufErgebnis(ok, stand, schritte, [.. d ?? [], .. Arbeitsbereich.Befunde()], boot ?? [], frage, DateTimeOffset.UtcNow);
                Laeuft = null;
                Fortschritt.Melde("ende", new { ok, stand });
                return Letzter;
            }

            // Der aktuelle Code-Stand im warmen Workspace — Befunde des Bauens finden darüber ihre Karte (auch neu angelegte Typen).
            var vorher = await Arbeitsbereich.LiesAsync(ct);

            // 1 Bauen: dotnet build des Laufzeit-Hosts — der Codegen-Prepass (Proto, STJ-Kontexte, Python-Basen) läuft im Build mit.
            Beginne("Bauen");
            // -p:CqrsStand: der Host stempelt diesen Stand an jedes Event (Spur) — Board, Build und Lauf tragen denselben.
            var bau = await Prozesse.LaufeAsync("dotnet", ["build", "Host.Grpc/Host.Grpc.csproj", "-v", "q", "-nologo", "-clp:NoSummary", $"-p:CqrsStand={stand}"], _root, ct: ct);
            var diagnosen = Karten.Zuordnen(Diagnosen.Lies(bau.Zeilen, _root), vorher.Board);
            Schritt(schritte, new Schritt("Bauen", bau.Ok, (long)bau.Dauer.TotalMilliseconds,
                bau.Ok ? ["0 Fehler"] : diagnosen.Count > 0 ? diagnosen.Select(x => $"{x.Datei}:{x.Zeile} {x.Code} {x.Text}").Take(20).ToList() : bau.Zeilen.TakeLast(15).ToList()));
            if (!bau.Ok) return Ende(false, diagnosen);

            // 2 Python-DTOs (betterproto) — sonst verstehen externe Worker die geänderten Nachrichten nicht.
            var python = Environment.GetEnvironmentVariable("CQRS_PYTHON") is { Length: > 0 } py ? Path.GetDirectoryName(py)! : "/opt/miniconda3/envs/gpu_env/bin";
            var skript = Path.Combine(_root, "Domain.Client.Worker.Python.ML", "build_python.sh");
            if (File.Exists(skript))
            {
                Beginne("Python-Basen");
                var pr = await Prozesse.LaufeAsync("bash", [skript], _root, new Dictionary<string, string> { ["PATH"] = python + ":" + Environment.GetEnvironmentVariable("PATH") }, TimeSpan.FromMinutes(3), ct);
                // Neue Client-Implementierungen (Python) entstehen aus der GENERIERTEN Basis — also erst nach dem Bauen (früher „C# schreiben“).
                var modell = DomainEditor.BoardLeseseite.AusBoard(vorher.BoardJson);
                var (angelegt, _) = SimHost.PythonSync.ImplementierungenAnlegen(modell, _root, trocken: false);
                // … ebenso die Python-Implementierungen der Funktionen, die die aktive Umgebung extern rechnen lässt (Plan E13).
                var (fAngelegt, fNicht) = SimHost.PythonSync.FunktionenAnlegen(modell, Umgebungen.Aufgeloest(Aktiv), _root);
                Schritt(schritte, new Schritt("Python-Basen", pr.Ok, (long)pr.Dauer.TotalMilliseconds,
                    [.. pr.Zeilen.TakeLast(3), .. angelegt.Concat(fAngelegt).Select(a => "＋ " + a), .. fNicht.Select(a => "⚠ " + a)]));
                if (!pr.Ok) return Ende(false, diagnosen);
            }

            // 3 Board neu lesen (Code = Wahrheit) — im warmen Workspace, nur was sich geändert hat (E7).
            Beginne("Neu lesen");
            var leseUhr = System.Diagnostics.Stopwatch.StartNew();
            var gelesen = await Arbeitsbereich.LiesAsync(ct);
            var befunde = gelesen.Befunde.Count();
            // Die eigenen Regeln (Analyzer, CQRS…) über den ganzen Stand — vollständig, auch für Projekte, die das Bauen nicht neu übersetzt hat.
            var regeln = await Arbeitsbereich.RegelBefundeAsync(ct);
            diagnosen = [.. diagnosen, .. regeln.Where(r => !diagnosen.Any(d => d.Code == r.Code && d.Datei == r.Datei && d.Zeile == r.Zeile && d.Text == r.Text))];
            Schritt(schritte, new Schritt("Neu lesen", true, (long)leseUhr.Elapsed.TotalMilliseconds,
                [ReferenceEquals(gelesen, vorher) ? $"Code unverändert seit dem Lesen vor dem Bauen ({gelesen.Routing.Source})"
                    : $"Board aus dem warmen Workspace ({gelesen.Routing.Source}) · Übersetzen {gelesen.Uebersetzen.TotalMilliseconds:0} ms · Lesen {gelesen.Extrahieren.TotalMilliseconds:0} ms",
                 befunde == 0 ? "keine Lese-Befunde" : $"{befunde} Lese-Befund(e) an Karten",
                 $"Regeln (Analyzer): {regeln.Count(r => r.Schwere == "error")} Fehler, {regeln.Count(r => r.Schwere == "warning")} Warnung(en)"]));

            var name = Aktiv;
            var u = Umgebungen.Aufgeloest(name);

            // Prod läuft über das Deployment, nie als Prozess der Werkbank — dort endet ⟳ nach dem Lesen (Code und Konfig sind geprüft).
            if (u.Umgebung.Art != UmgebungsArt.Sim)
            {
                Schritt(schritte, new Schritt("Starten", true, 0, [$"{name} ist {u.Umgebung.Art} — gestartet wird über das Deployment, nicht aus der Werkbank"]));
                return Ende(true, diagnosen);
            }

            // 4 Daten prüfen: passen die gespeicherten Events noch zum Code? (Projektionen und Snapshots prüft der Host selbst beim
            //   Start über die Code-Hashes — ProjektionsStandDienst bzw. Snapshot-Version.)
            Beginne("Daten prüfen");
            var neuProto = File.Exists(NeuesProto) ? await File.ReadAllTextAsync(NeuesProto, ct) : null;
            var altProto = await Db.LiesAsync(u, u.EventStore.Datenbank, SimDatenbank.ProtoSchluessel);
            if (altProto != null && neuProto != null)
            {
                var unvertraeglich = ProtoVergleich.Unvertraeglich(altProto, neuProto);
                if (unvertraeglich.Count > 0 && !zuruecksetzen)
                {
                    Schritt(schritte, new Schritt("Daten prüfen", false, 0, unvertraeglich));
                    return Ende(false, diagnosen, frage: unvertraeglich);
                }
                if (unvertraeglich.Count > 0)
                {
                    await Host.StoppeAsync();
                    await Db.ZuruecksetzenAsync(u);
                    Schritt(schritte, new Schritt("Daten prüfen", true, 0, [$"Sim-DB {u.EventStore.Datenbank} zurückgesetzt", .. unvertraeglich]));
                }
                else Schritt(schritte, new Schritt("Daten prüfen", true, 0, ["Events passen zum Code"]));
            }
            else Schritt(schritte, new Schritt("Daten prüfen", true, 0, [altProto == null ? "kein gespeicherter Stand (neue oder übernommene Sim-DB) — der neue gilt ab dem Start" : "kein domain.proto"]));

            // 5 Infrastruktur.
            Beginne("Infrastruktur");
            var infra = await Infra.StarteAsync(name, u, ct);
            Schritt(schritte, new Schritt("Infrastruktur", infra.Ok, 0, infra.Meldungen));
            if (!infra.Ok) return Ende(false, diagnosen);

            // 6 Host ersetzen. Den Proto-Stand der Daten schreibt der Host beim Start selbst (UmgebungsSiegel, jede Umgebung).
            Beginne("Starten");
            var start = await Host.StarteAsync(name, u.Transport.EinblickPort, Umgebungen.Branch(), ct);
            Schritt(schritte, new Schritt("Starten", start.Ok, 0, start.Ok ? [$"Host läuft · {name} · DB {u.EventStore.Datenbank}"] : start.Boot.Select(b => b.Text).ToList()));
            if (start.Ok)
            {
                LaufStand = stand;
                _laufUmgebung = DateiInhalt(name);
            }
            return Ende(start.Ok, diagnosen, start.Boot);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Laeuft = null;
            Fortschritt.Melde("ende", new { ok = false, fehler = e.Message });
            throw;
        }
        finally { _einLauf.Release(); }
    }

    /// <summary>
    /// Sim-DB-Aktionen (§4.6): zurücksetzen, Stand speichern/laden/löschen, Prod-Stand übernehmen. Der Host hält dafür an (eine Vorlage
    /// verträgt keine Verbindungen) und startet danach wieder, wenn er lief — auch wenn die Aktion scheitert.
    /// </summary>
    public async Task<object> DatenbankAsync(string aktion, string? stand, CancellationToken ct)
    {
        await _einLauf.WaitAsync(ct);
        var name = Aktiv;
        var u = Umgebungen.Aufgeloest(name);
        var lief = Host.Laeuft && aktion != "loeschen";
        (bool Ok, IReadOnlyList<BootMeldung> Boot) neu = (true, []);
        try
        {
            if (lief) await Host.StoppeAsync();
            Beginne("Sim-DB: " + aktion);
            IReadOnlyList<string> meldungen = [];
            string Name() => string.IsNullOrWhiteSpace(stand) ? throw new ArgumentException("Name des Stands fehlt") : stand;
            switch (aktion)
            {
                case "zuruecksetzen": await Db.ZuruecksetzenAsync(u); break;
                case "speichern": await Db.SpeichereAsync(u, Name()); break;
                case "laden": await Db.LadeAsync(u, Name()); break;
                case "loeschen": await Db.LoescheStandAsync(u, Name()); break;
                case "prod":
                    var prodName = string.IsNullOrWhiteSpace(stand)
                        ? Umgebungen.Namen().FirstOrDefault(n => Umgebungen.Aufgeloest(n).Umgebung.Art == UmgebungsArt.Prod)
                          ?? throw new InvalidOperationException("Keine Prod-Umgebung vorhanden.")
                        : stand;
                    meldungen = await Db.UebernehmeProdAsync(u, Umgebungen.Aufgeloest(prodName), _root, ct);
                    break;
                default: throw new ArgumentException($"unbekannte Aktion {aktion}");
            }
            var neuGestartet = lief;
            if (lief) neu = await Host.StarteAsync(name, u.Transport.EinblickPort, Umgebungen.Branch(), ct);
            lief = false;
            var aus = new { ok = neu.Ok, aktion, stand, meldungen, host = !Host.Laeuft ? "steht" : neuGestartet ? "neu gestartet" : "läuft", boot = neu.Boot };
            Fortschritt.Melde("simdb", aus);
            return aus;
        }
        finally
        {
            if (lief) await Host.StarteAsync(name, u.Transport.EinblickPort, Umgebungen.Branch(), CancellationToken.None);   // Aktion gescheitert
            Laeuft = null;
            Fortschritt.Melde("ende", new { aktion });
            _einLauf.Release();
        }
    }
}
