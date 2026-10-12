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
    public KreislaufErgebnis? Letzter { get; private set; }

    public Werkbank(string slnRoot)
    {
        _root = slnRoot;
        Umgebungen = new Umgebungen(slnRoot);
        Host = new HostProzess(slnRoot);
        Infra = new Infra(slnRoot);
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
                : namen.FirstOrDefault(n => Umgebungen.Aufgeloest(n).Umgebung.Art == UmgebungsArt.Sim) ?? namen.FirstOrDefault() ?? "";
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

    private string ProtoDatei(UmgebungKonfig u) => Path.Combine(_root, ".werkbank", "db", u.EventStore.Datenbank, "domain.proto");

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
            infra = u == null ? null : Infra.Dienste(u), host = Host.Zustand(), stand = Stand(),
            build = Letzter == null ? null : new { Letzter.Ok, Letzter.Stand, Letzter.Zeit, fehler = Letzter.Diagnosen.Count },
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
            KreislaufErgebnis Ende(bool ok, IReadOnlyList<Diagnose>? d = null, IReadOnlyList<BootMeldung>? boot = null, IReadOnlyList<string>? frage = null)
                => Letzter = new KreislaufErgebnis(ok, stand, schritte, d ?? [], boot ?? [], frage, DateTimeOffset.UtcNow);

            // 1 Bauen: dotnet build des Laufzeit-Hosts — der Codegen-Prepass (Proto, STJ-Kontexte, Python-Basen) läuft im Build mit.
            var bau = await Prozesse.LaufeAsync("dotnet", ["build", "Host.Grpc/Host.Grpc.csproj", "-v", "q", "-nologo", "-clp:NoSummary"], _root, ct: ct);
            var diagnosen = Diagnosen.Lies(bau.Zeilen, _root);
            schritte.Add(new Schritt("Bauen", bau.Ok, (long)bau.Dauer.TotalMilliseconds,
                bau.Ok ? ["0 Fehler"] : diagnosen.Count > 0 ? diagnosen.Select(x => $"{x.Datei}:{x.Zeile} {x.Code} {x.Text}").Take(20).ToList() : bau.Zeilen.TakeLast(15).ToList()));
            if (!bau.Ok) return Ende(false, diagnosen);

            // 2 Python-DTOs (betterproto) — sonst verstehen externe Worker die geänderten Nachrichten nicht.
            var python = Environment.GetEnvironmentVariable("CQRS_PYTHON") is { Length: > 0 } py ? Path.GetDirectoryName(py)! : "/opt/miniconda3/envs/gpu_env/bin";
            var skript = Path.Combine(_root, "Domain.Client.Worker.Python.ML", "build_python.sh");
            if (File.Exists(skript))
            {
                var pr = await Prozesse.LaufeAsync("bash", [skript], _root, new Dictionary<string, string> { ["PATH"] = python + ":" + Environment.GetEnvironmentVariable("PATH") }, TimeSpan.FromMinutes(3), ct);
                schritte.Add(new Schritt("Python-Basen", pr.Ok, (long)pr.Dauer.TotalMilliseconds, pr.Zeilen.TakeLast(3).ToList()));
                if (!pr.Ok) return Ende(false, diagnosen);
            }

            // 3 Board neu lesen (Code = Wahrheit).
            var lesen = await Prozesse.LaufeAsync("dotnet", ["run", "--project", "GraphExtractor"], _root, zeitlimit: TimeSpan.FromMinutes(5), ct: ct);
            schritte.Add(new Schritt("Neu lesen", lesen.Ok, (long)lesen.Dauer.TotalMilliseconds, lesen.Zeilen.Where(z => z.StartsWith("✅", StringComparison.Ordinal)).ToList()));
            if (!lesen.Ok) return Ende(false, diagnosen);

            var name = Aktiv;
            var u = Umgebungen.Aufgeloest(name);

            // 4 Daten prüfen (nur Sim): passen die gespeicherten Events noch zum Code?
            var neuProto = Path.Combine(_root, "ProtoRepo", "domain.proto");
            if (u.Umgebung.Art == UmgebungsArt.Sim && File.Exists(ProtoDatei(u)) && File.Exists(neuProto))
            {
                var unvertraeglich = ProtoVergleich.Unvertraeglich(File.ReadAllText(ProtoDatei(u)), File.ReadAllText(neuProto));
                if (unvertraeglich.Count > 0 && !zuruecksetzen)
                {
                    schritte.Add(new Schritt("Daten prüfen", false, 0, unvertraeglich));
                    return Ende(false, diagnosen, frage: unvertraeglich);
                }
                if (unvertraeglich.Count > 0)
                {
                    await Host.StoppeAsync();
                    await Db.ZuruecksetzenAsync(u);
                    schritte.Add(new Schritt("Daten prüfen", true, 0, [$"Sim-DB {u.EventStore.Datenbank} zurückgesetzt", .. unvertraeglich]));
                }
                else schritte.Add(new Schritt("Daten prüfen", true, 0, ["Daten passen zum Code"]));
            }

            // 5 Infrastruktur.
            var infra = await Infra.StarteAsync(name, u, ct);
            schritte.Add(new Schritt("Infrastruktur", infra.Ok, 0, infra.Meldungen));
            if (!infra.Ok) return Ende(false, diagnosen);

            // 6 Host ersetzen.
            var start = await Host.StarteAsync(name, u.Transport.EinblickPort, Umgebungen.Branch(), ct);
            schritte.Add(new Schritt("Starten", start.Ok, 0, start.Ok ? [$"Host läuft · {name} · DB {u.EventStore.Datenbank}"] : start.Boot.Select(b => b.Text).ToList()));
            if (start.Ok)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProtoDatei(u))!);
                if (File.Exists(neuProto)) File.Copy(neuProto, ProtoDatei(u), overwrite: true);
            }
            return Ende(start.Ok, diagnosen, start.Boot);
        }
        finally { _einLauf.Release(); }
    }

    /// <summary>Sim-DB: zurücksetzen bzw. Stand laden — Host anhalten, Aktion, Host wieder starten (wenn er lief).</summary>
    public async Task<object> DatenbankAsync(string aktion, string? stand, CancellationToken ct)
    {
        await _einLauf.WaitAsync(ct);
        try
        {
            var name = Aktiv;
            var u = Umgebungen.Aufgeloest(name);
            var lief = Host.Laeuft;
            await Host.StoppeAsync();
            switch (aktion)
            {
                case "zuruecksetzen": await Db.ZuruecksetzenAsync(u); break;
                case "speichern":
                    await Db.SpeichereAsync(u, stand ?? throw new ArgumentException("Name des Stands fehlt"));
                    if (File.Exists(ProtoDatei(u)))
                    {
                        var ziel = Path.Combine(_root, ".werkbank", "db", SimDatenbank.StandName(u, stand), "domain.proto");
                        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
                        File.Copy(ProtoDatei(u), ziel, true);
                    }
                    break;
                case "laden":
                    await Db.LadeAsync(u, stand ?? throw new ArgumentException("Name des Stands fehlt"));
                    var quelle = Path.Combine(_root, ".werkbank", "db", SimDatenbank.StandName(u, stand), "domain.proto");
                    if (File.Exists(quelle)) { Directory.CreateDirectory(Path.GetDirectoryName(ProtoDatei(u))!); File.Copy(quelle, ProtoDatei(u), true); }
                    break;
                default: throw new ArgumentException($"unbekannte Aktion {aktion}");
            }
            (bool Ok, IReadOnlyList<BootMeldung> Boot) neu = (true, []);
            if (lief) neu = await Host.StarteAsync(name, u.Transport.EinblickPort, Umgebungen.Branch(), ct);
            return new { ok = neu.Ok, aktion, stand, host = neu.Ok ? (lief ? "neu gestartet" : "stand") : "Start fehlgeschlagen", boot = neu.Boot };
        }
        finally { _einLauf.Release(); }
    }
}
