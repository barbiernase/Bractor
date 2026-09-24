using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abstractions;
using Cqrs.Testing;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SimHost;

// ── Ergebnis-DTOs für den Editor ──
public record CompileFehler(string Schweregrad, string Code, string Meldung, string? Datei);
public record CompileErgebnis(bool Ok, List<CompileFehler> Fehler, int Aggregate, int Sagas);

/// <summary>Ein Event im Simulations-Frame: Typ, persistent/Ablehnung, gebundener Guard („weil …"), Werte.</summary>
public record SimEvent(string Typ, bool Persistent, string? Warum, string Werte);
/// <summary>Ein Command-Schritt der Kaskade — genau das, was der Editor Knoten für Knoten animiert.</summary>
public record SimFrame(string Command, string Werte, string Herkunft, string? Saga, int? Regel,
    string Aggregat, string AggregatId, string Label, List<SimEvent> Events, bool Unrouted);
public record SimFeld(string Name, string Wert, bool Geaendert);
public record SimInstanz(string Aggregat, string Id, string Label, List<SimFeld> Felder);
public record SimWartend(List<string> Bedingung, List<string> Fehlt);
public record SimSaga(string Prozess, string Korrelation, List<string> Angekommen, List<SimWartend> Wartend);
public record SimErgebnis(bool Ok, List<CompileFehler> Fehler, List<SimFrame> Frames, List<SimInstanz> Instanzen,
    List<SimSaga> Sagas, List<string> Abdeckung, int Nachgespielt, string? Hinweis = null);

/// <summary>
/// DIE EINE Simulation des Editors (ersetzt die alte, fest an <c>Domain.dll</c> gebundene SimEngine):
/// das Editor-Modell wird mit dem <see cref="Scaffolder"/> zu C#, IN-MEMORY mit den ECHTEN Domain-Generatoren
/// übersetzt und über den geteilten store-freien Kern (<see cref="SagaLaufwerk"/>) gefahren — dieselbe
/// Kaskade wie die Test-DSL. Weil der Round-trip ein Fixpunkt ist (GraphExtractor <c>--check</c>), ist das
/// Kompilat des Modells verhaltensgleich zum echten Code — und simuliert zusätzlich ungeschriebene Entwürfe.
///
/// Sessions sind zustandsbehaftet (Instanzen, Saga-Markings, Abdeckung). <b>Hot-Reload:</b> ändert sich das
/// Modell zwischen zwei Schritten, wird neu übersetzt und die bisherigen Wurzel-Commands werden nachgespielt —
/// man ändert eine Regel und sieht sofort, wie dieselbe Geschichte jetzt ausgeht.
///
/// EHRLICHE GRENZEN: nur die Schreibseite + Sagas (keine Projektionen/Reader/Pipelines, kein Marten/Wire).
/// Kompilate werden nach Modell-Hash gecacht; alte Assemblies bleiben im Prozess (kein Unload).
/// </summary>
public sealed class ModellSimulation
{
    private sealed class Kompilat
    {
        public required CompileErgebnis Ergebnis { get; init; }
        public Assembly? Assembly { get; init; }
    }

    private sealed class Session
    {
        public required string Hash;
        public required SagaLaufwerk Lauf;
        public readonly List<(string Command, string Werte)> Wurzeln = new();   // zum Nachspielen (Hot-Reload)
        public ICommand? LetzteWurzel;
        public List<Ereignis> LetzterAusgang = new();
        public readonly Dictionary<(string, Guid), string> Labels = new();
        public readonly Dictionary<string, int> Zähler = new();
        public readonly HashSet<string> Abdeckung = new(StringComparer.Ordinal);
        public Dictionary<(string, Guid), IReadOnlyDictionary<string, object?>> Vorher = new();
    }

    private static readonly JsonSerializerOptions CmdJson = new() { PropertyNameCaseInsensitive = true };
    private readonly ConcurrentDictionary<string, Kompilat> _cache = new();
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    /// <summary>Nur übersetzen und die Diagnosen melden — der Self-Repair-/Guardrail-Signalgeber.</summary>
    public CompileErgebnis Kompiliere(EditorModell modell) => Hole(modell).Ergebnis;

    public void Reset(string sessionId) => _sessions.TryRemove(sessionId, out _);

    /// <summary>
    /// Einen Command mit Werten schicken → die ganze wertabhängige Kaskade (inkl. Sagas). Ist das Modell seit
    /// dem letzten Schritt geändert, wird neu übersetzt und die Session-Geschichte nachgespielt.
    /// </summary>
    public SimErgebnis Schritt(EditorModell modell, string sessionId, string commandName, JsonElement werte)
    {
        var (session, fehler, nachgespielt, hinweis) = SessionFür(modell, sessionId);
        if (session is null) return new SimErgebnis(false, fehler, [], [], [], [], 0);

        var asm = Hole(modell).Assembly!;
        var cmdType = asm.GetTypes().FirstOrDefault(t => typeof(ICommand).IsAssignableFrom(t) && t.Name == commandName);
        if (cmdType is null)
            return new SimErgebnis(false, [new("error", "SIM-CMD", $"Command '{commandName}' nicht im Kompilat.", null)], [], Instanzen(session), [], [], nachgespielt);

        ICommand cmd;
        try { cmd = (ICommand)JsonSerializer.Deserialize(werte.GetRawText(), cmdType, CmdJson)!; }
        catch (Exception ex)
        {
            return new SimErgebnis(false, [new("error", "SIM-WERTE", $"Werte passen nicht zu {commandName}: {ex.Message}", null)],
                [], Instanzen(session), [], [], nachgespielt);
        }

        session.Vorher = session.Lauf.AlleZustände().ToDictionary(z => (z.Typ, z.Id), z => z.Felder);
        session.Wurzeln.Add((commandName, werte.GetRawText()));
        var (frames, trace) = Fahre(session, modell, cmd);
        return new SimErgebnis(true, [], frames, Instanzen(session), Sagas(trace), session.Abdeckung.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            nachgespielt, hinweis);
    }

    /// <summary>Aktueller Stand einer Session (Instanzen + Abdeckung) ohne neuen Schritt.</summary>
    public SimErgebnis Stand(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var s)
            ? new SimErgebnis(true, [], [], Instanzen(s), [], s.Abdeckung.OrderBy(x => x, StringComparer.Ordinal).ToList(), 0)
            : new SimErgebnis(true, [], [], [], [], [], 0);

    /// <summary>Die Session als Regressionstest in der Test-DSL (Szenario.Für…Vorab…Wenn…Dann).</summary>
    public string Dsl(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s) || s.LetzteWurzel is null)
            return "// Noch kein Command geschickt — erst in der Simulation ein Command absenden.";
        var ziel = s.LetzteWurzel;
        var stateTyp = ZielAggregat(s, ziel);
        if (stateTyp is null) return "// Das letzte Command wird von keinem Aggregat behandelt.";
        var vorab = s.Wurzeln.Take(s.Wurzeln.Count - 1)
            .Select(w => Deserialisiere(s, w.Command, w.Werte))
            .Where(c => c is not null && c.AggregateId == ziel.AggregateId && ZielAggregat(s, c) == stateTyp)
            .Select(c => c!).ToList();
        return DslSchreiber.Aus(stateTyp, Array.Empty<IEvent>(), vorab, ziel, s.LetzterAusgang);
    }

    // ── Session / Hot-Reload ────────────────────────────────────────────────────────────────
    private (Session? S, List<CompileFehler> Fehler, int Nachgespielt, string? Hinweis) SessionFür(EditorModell modell, string sessionId)
    {
        var k = Hole(modell);
        if (!k.Ergebnis.Ok || k.Assembly is null) return (null, k.Ergebnis.Fehler, 0, null);
        var hash = Hash(modell.AlsJson());

        if (_sessions.TryGetValue(sessionId, out var alt) && alt.Hash == hash) return (alt, [], 0, null);

        var neu = new Session { Hash = hash, Lauf = BaueLauf(k.Assembly) };
        var nachgespielt = 0;
        string? hinweis = null;
        if (alt is not null)
        {
            // Modell geändert → dieselbe Geschichte gegen die neue Logik nachspielen (still, ohne Frames).
            foreach (var (name, werte) in alt.Wurzeln)
            {
                var c = Deserialisiere(neu, name, werte);
                if (c is null) continue;
                neu.Wurzeln.Add((name, werte));
                Fahre(neu, modell, c);
                nachgespielt++;
            }
            foreach (var kv in alt.Labels) neu.Labels.TryAdd(kv.Key, kv.Value);
            foreach (var kv in alt.Zähler) neu.Zähler[kv.Key] = Math.Max(neu.Zähler.GetValueOrDefault(kv.Key), kv.Value);
            hinweis = nachgespielt == alt.Wurzeln.Count
                ? $"Modell geändert — {nachgespielt} bisherige Command(s) gegen die neue Logik nachgespielt."
                : $"Modell geändert — {nachgespielt}/{alt.Wurzeln.Count} Command(s) nachgespielt (übrige passen nicht mehr zum Modell).";
        }
        _sessions[sessionId] = neu;
        return (neu, [], nachgespielt, hinweis);
    }

    private static ICommand? Deserialisiere(Session s, string name, string werte)
    {
        var t = FabrikAssembly(s)?.GetTypes().FirstOrDefault(x => typeof(ICommand).IsAssignableFrom(x) && x.Name == name);
        if (t is null) return null;
        try { return (ICommand?)JsonSerializer.Deserialize(werte, t, CmdJson); } catch { return null; }
    }

    private static readonly ConditionalWeakTable<SagaLaufwerk, Assembly> LaufAssembly = new();
    private static Assembly? FabrikAssembly(Session s) => LaufAssembly.TryGetValue(s.Lauf, out var a) ? a : null;

    /// <summary>Das Aggregat, dessen innerer Decider (Vertrag <c>IDecider&lt;&gt;</c>) eine Methode mit diesem Command-Typ hat.</summary>
    private static string? ZielAggregat(Session s, ICommand c) =>
        FabrikAssembly(s)?.GetTypes()
            .Where(t => t.IsClass && typeof(IState).IsAssignableFrom(t))
            .FirstOrDefault(t => t.GetNestedTypes()
                .Where(n => n.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDecider<>)))
                .SelectMany(n => n.GetMethods())
                .Any(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == c.GetType()))?.Name;

    // ── Fahren + Frames ─────────────────────────────────────────────────────────────────────
    private (List<SimFrame>, SagaTrace) Fahre(Session s, EditorModell modell, ICommand wurzel)
    {
        var trace = s.Lauf.Fahre(wurzel);
        s.LetzteWurzel = wurzel;
        s.LetzterAusgang = trace.Schritte.FirstOrDefault(x => ReferenceEquals(x.Command, wurzel))?.Ausgang.ToList() ?? new();

        var guards = modell.Decider
            .SelectMany(d => d.Ergibt.Where(a => a.Guard is not null).Select(a => (Key: d.Command + "|" + a.Event, a.Guard!)))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.Ordinal);

        var frames = new List<SimFrame>();
        foreach (var st in trace.Schritte)
        {
            var cmdName = st.Command.GetType().Name;
            if (!st.Unrouted)
            {
                s.Abdeckung.Add($"dec:{st.AggregatTyp}|{cmdName}");
                foreach (var e in st.Ausgang)
                {
                    s.Abdeckung.Add($"out:{cmdName}>{e.Typ}");
                    if (e.Persistent) s.Abdeckung.Add($"app:{st.AggregatTyp}|{e.Typ}");
                }
            }
            if (st.SagaName is not null && st.RegelIndex is int ri) s.Abdeckung.Add($"regel:{st.SagaName}#{ri}");

            var events = st.Ausgang.Select(e => new SimEvent(
                e.Typ, e.Persistent,
                guards.TryGetValue(cmdName + "|" + e.Typ, out var g) ? GuardBinder.Binde(g, st.ZustandVorher, st.Command) : null,
                Werte(e.Event))).ToList();
            frames.Add(new SimFrame(cmdName, Werte(st.Command), st.Ursprung == Ursprung.Saga ? "saga" : "wurzel",
                st.SagaName, st.RegelIndex, st.AggregatTyp, st.AggregatId.ToString(),
                st.Unrouted ? "—" : Label(s, st.AggregatTyp, st.AggregatId), events, st.Unrouted));
        }
        return (frames, trace);
    }

    private List<SimInstanz> Instanzen(Session s)
    {
        var alle = s.Lauf.AlleZustände();
        foreach (var z in alle) Label(s, z.Typ, z.Id);
        return alle.Select(z =>
            {
                s.Vorher.TryGetValue((z.Typ, z.Id), out var vorher);
                var felder = z.Felder.Select(kv => new SimFeld(kv.Key, Anzeige(kv.Value),
                    vorher is null || !vorher.TryGetValue(kv.Key, out var alt) || Anzeige(alt) != Anzeige(kv.Value))).ToList();
                return new SimInstanz(z.Typ, z.Id.ToString(), Label(s, z.Typ, z.Id), felder);
            })
            .OrderBy(x => x.Label, StringComparer.Ordinal).ToList();
    }

    private static List<SimSaga> Sagas(SagaTrace t) =>
        t.Markierungen.Select(m => new SimSaga(m.Prozess, m.Korrelation.ToString(), m.Angekommen.ToList(),
            m.Wartend.Select(w => new SimWartend(w.Bedingung.ToList(), w.Fehlt.ToList())).ToList())).ToList();

    // Stabiles, lesbares Label je (Typ, Id) in Anlege-Reihenfolge: „Datensatz #1", „Datensatz #2", …
    private static string Label(Session s, string typ, Guid id)
    {
        if (s.Labels.TryGetValue((typ, id), out var l)) return l;
        s.Zähler.TryGetValue(typ, out var n);
        l = $"{typ} #{n + 1}";
        s.Zähler[typ] = n + 1;
        s.Labels[(typ, id)] = l;
        return l;
    }

    /// <summary>Werte einer Nachricht kompakt (ohne AggregateId): <c>Name=Test, Anzahl=3</c>.</summary>
    private static string Werte(object msg) =>
        string.Join(", ", msg.GetType().GetProperties()
            .Where(p => p.Name is not ("AggregateId" or "EqualityContract") && p.GetIndexParameters().Length == 0)
            .Select(p => { try { return $"{p.Name}={Anzeige(p.GetValue(msg))}"; } catch { return p.Name; } }));

    /// <summary>Anzeige-Text eines Feldwerts: Collections als <c>[n: a, b, …]</c>, Records über ihr ToString.</summary>
    private static string Anzeige(object? o) => o switch
    {
        null => "null",
        string str => str,
        IEnumerable e when o is not string => ListenText(e),
        _ => o.ToString() ?? "",
    };

    private static string ListenText(IEnumerable e)
    {
        var xs = e.Cast<object?>().ToList();
        return $"[{xs.Count}" + (xs.Count == 0 ? "]" : ": " + string.Join(", ", xs.Take(3).Select(Anzeige)) + (xs.Count > 3 ? ", …]" : "]"));
    }

    // ── In-Memory-Übersetzung ───────────────────────────────────────────────────────────────
    private Kompilat Hole(EditorModell modell) => _cache.GetOrAdd(Hash(modell.AlsJson()), _ => Übersetze(modell));

    /// <summary>
    /// Nur die SCHREIBSEITE übersetzen: Records/Enums aus den Namespaces der Aggregate und Sagas plus alles,
    /// was deren Felder transitiv referenzieren. Leseseiten-/Pipeline-Typen (eigene Assemblies mit eigenen
    /// Paket-Referenzen, z. B. OpenCvSharp) gehören nicht in den store-freien Kern.
    /// </summary>
    internal static EditorModell NurSchreibseite(EditorModell modell)
    {
        var bezeichner = new System.Text.RegularExpressions.Regex(@"[A-Za-z_][A-Za-z0-9_]*");
        var nsVon = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in modell.Records) nsVon.TryAdd(r.Name, r.Namespace);
        foreach (var e in modell.Enums) nsVon.TryAdd(e.Name, e.Namespace);

        var ns = new HashSet<string>(modell.Aggregate.Select(a => a.Namespace).Concat(modell.Sagas.Select(g => g.Namespace)), StringComparer.Ordinal);
        for (var geändert = true; geändert;)
        {
            geändert = false;
            var typen = modell.Records.Where(r => ns.Contains(r.Namespace)).SelectMany(r => r.Felder.Select(f => f.Typ))
                .Concat(modell.Aggregate.SelectMany(a => a.State.Select(f => f.Typ)));
            foreach (var typ in typen)
                foreach (System.Text.RegularExpressions.Match m in bezeichner.Matches(typ))
                    if (nsVon.TryGetValue(m.Value, out var n) && ns.Add(n)) geändert = true;
        }
        return modell with
        {
            Records = modell.Records.Where(r => ns.Contains(r.Namespace)).ToList(),
            Enums = modell.Enums.Where(e => ns.Contains(e.Namespace)).ToList(),
        };
    }

    private static Kompilat Übersetze(EditorModell modell)
    {
        modell = NurSchreibseite(modell);
        var trees = Scaffolder.Generiere(modell)
            .Select(d => CSharpSyntaxTree.ParseText(d.Inhalt, path: d.Pfad)).ToList();
        // Die globalen usings der Domänen-Projekte, wie der Extractor sie im Code gefunden hat (Rahmen).
        trees.Add(CSharpSyntaxTree.ParseText(
            string.Concat(modell.Rahmen.GlobaleUsings.Select(u => $"global using {u};\n")), path: "GlobalUsings.g.cs"));

        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var refs = tpa.Where(p => p.EndsWith(".dll")).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

        var comp = CSharpCompilation.Create("EditorLive_" + Guid.NewGuid().ToString("N"),
            trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // Die ECHTEN Domain-Generatoren (State/Handler/Factory/Signal/Prozess…) laufen lassen.
        var genAsm = typeof(Domain.SourceGeneration.Generator).Assembly;
        var gens = genAsm.GetTypes().Where(t => !t.IsAbstract &&
                (typeof(IIncrementalGenerator).IsAssignableFrom(t) || typeof(ISourceGenerator).IsAssignableFrom(t)))
            .Select(t => Activator.CreateInstance(t)!)
            .Select(o => o is IIncrementalGenerator ig ? ig.AsSourceGenerator() : (ISourceGenerator)o)
            .ToArray();

        CSharpGeneratorDriver.Create(gens).RunGeneratorsAndUpdateCompilation(comp, out var outComp, out var genDiag);

        using var ms = new MemoryStream();
        var emit = outComp.Emit(ms);

        var fehler = emit.Diagnostics.Concat(genDiag)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => new CompileFehler("error", d.Id, d.GetMessage(), d.Location.SourceTree?.FilePath))
            .DistinctBy(f => (f.Code, f.Meldung, f.Datei))
            .ToList();

        if (!emit.Success)
            return new Kompilat { Ergebnis = new CompileErgebnis(false, fehler, modell.Aggregate.Count, modell.Sagas.Count) };

        ms.Position = 0;
        var asm = Assembly.Load(ms.ToArray());
        return new Kompilat
        {
            Assembly = asm,
            Ergebnis = new CompileErgebnis(true, fehler, modell.Aggregate.Count, modell.Sagas.Count),
        };
    }

    private static SagaLaufwerk BaueLauf(Assembly asm)
    {
        // Die generierte Handler-Fabrik = der Typ, der den Vertrag IAggregateHandlerFactory implementiert.
        var factoryType = asm.GetTypes().FirstOrDefault(t => t.IsClass && !t.IsAbstract && typeof(IAggregateHandlerFactory).IsAssignableFrom(t))
            ?? throw new InvalidOperationException("Keine Handler-Fabrik generiert (kein Aggregat?).");
        var factory = (IAggregateHandlerFactory)Activator.CreateInstance(factoryType)!;

        // Prozess-Regeln direkt aus den kompilierten IProzessDefinition-Typen (wie SagaSzenario.Mit).
        var prozesse = asm.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IProzessDefinition).IsAssignableFrom(t))
            .Select(t => (IProzessDefinition)Activator.CreateInstance(t)!)
            .Select(p => (p.GetType().Name, p.Regeln))
            .ToList();

        var lauf = new SagaLaufwerk(factory, prozesse);
        LaufAssembly.AddOrUpdate(lauf, asm);
        return lauf;
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
