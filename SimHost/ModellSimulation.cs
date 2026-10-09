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

/// <summary>Ein Event im Simulations-Frame: Typ, persistent/Ablehnung, Werte.</summary>
public record SimEvent(string Typ, bool Persistent, string Werte);
/// <summary>Ein Command-Schritt der Kaskade — genau das, was der Editor Knoten für Knoten animiert.</summary>
public record SimFrame(string Command, string Werte, string Herkunft, string? Saga, int? Regel,
    string Aggregat, string AggregatId, string Label, List<SimEvent> Events, bool Unrouted);
public record SimFeld(string Name, string Wert, bool Geaendert);
public record SimInstanz(string Aggregat, string Id, string Label, List<SimFeld> Felder);
public record SimWartend(List<string> Bedingung, List<string> Fehlt);
public record SimSaga(string Prozess, string Korrelation, List<string> Angekommen, List<SimWartend> Wartend);
public record SimErgebnis(bool Ok, List<CompileFehler> Fehler, List<SimFrame> Frames, List<SimInstanz> Instanzen,
    List<SimSaga> Sagas, List<string> Abdeckung, int Nachgespielt, string? Hinweis = null,
    List<SimFlussSchritt>? Fluss = null, List<SimVorgang>? Vorgaenge = null);

/// <summary>Ein Draht, über den ein Fluss-Knoten bedient wurde: Herkunfts-Knoten (Name), Fall bzw. Port (⏳/✕).</summary>
public record SimDraht(string Von, string Fall, string Port);
/// <summary>
/// Ein Schritt des Dirigenten im Durchspielen (docs/konzept-editor-pipelines.md §14): Knoten (Name wie im Editor), was er rief, welcher
/// Ausgang feuerte. <see cref="Frames"/> = die Indizes der Aggregat-Frames, die ein Command-Knoten ausgelöst hat.
/// </summary>
public record SimFlussSchritt(string Pipeline, int Vorgang, string Knoten, int Index, string Art, string Typ, string Werte,
    List<SimDraht> Ein, string Ausgang, string? Fall, string? ErgebnisWerte, string? Grund, int? Element, List<int> Frames);
public record SimVorgang(string Pipeline, int Nummer, bool Erfolg, string? Grund, List<string> Wartend);

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
/// Pipelines als Fluss (§14) laufen mit: eine Quell-Nachricht einspeisen (<see cref="StarteFluss"/>), je Funktionsknoten wählt der
/// Editor den Ausgangsfall (Ergebnis / ⏳ / ✕) — ohne Implementierung; Commands laufen in dieselbe Aggregat-Kaskade, deren Events
/// Flüsse mit <c>p.Auf&lt;E&gt;()</c> starten. Der Dirigent ist <see cref="FlussLaufwerk"/> (derselbe Belegungs-Kern wie live).
///
/// EHRLICHE GRENZEN: Schreibseite + Sagas + Flüsse (keine Projektionen/Reader/Handle-Pipelines, kein Marten/Wire).
/// Kompilate werden nach Modell-Hash gecacht; alte Assemblies bleiben im Prozess (kein Unload).
/// </summary>
public sealed class ModellSimulation
{
    private sealed class Kompilat
    {
        public required CompileErgebnis Ergebnis { get; init; }
        public Assembly? Assembly { get; init; }
    }

    /// <summary>Eine Wurzel der Geschichte (zum Nachspielen): ein Command von außen oder eine eingespeiste Quell-Nachricht.</summary>
    private sealed record Wurzel(bool IstFluss, string Name, string Werte, string? Wahl, Guid? Strom = null);

    private sealed class Session
    {
        public required string Hash;
        public required FlussLaufwerk Fluss;
        public SagaLaufwerk Lauf => Fluss.Aggregate;
        public readonly List<Wurzel> Wurzeln = new();   // zum Nachspielen (Hot-Reload)
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
    public SimErgebnis Schritt(EditorModell modell, string sessionId, string commandName, JsonElement werte, JsonElement? wahl = null)
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
        var wahlText = wahl is { ValueKind: JsonValueKind.Object } w ? w.GetRawText() : null;
        session.Wurzeln.Add(new Wurzel(false, commandName, werte.GetRawText(), wahlText));
        try
        {
            var lauf = session.Fluss.Fahre(cmd, Wahl(session, modell, wahlText));
            session.LetzteWurzel = cmd;
            return Ergebnis(session, modell, lauf, nachgespielt, hinweis);
        }
        catch (InvalidOperationException ex)
        {
            session.Wurzeln.RemoveAt(session.Wurzeln.Count - 1);
            return new SimErgebnis(false, [new("error", "SIM-FLUSS", ex.Message, null)], [], Instanzen(session), [], [], nachgespielt);
        }
    }

    /// <summary>
    /// Eine Quell-Nachricht (bzw. das Auslöse-Event) in die Pipeline <paramref name="pipeline"/> einspeisen und den Fluss durchspielen.
    /// <paramref name="wahl"/>: <c>{ Pipeline: { Knoten: { fall: "Ergebnis" | "zeitlimit" | "abgelehnt", werte: {…} } } }</c> — fehlt ein
    /// Knoten, liefert er seinen ersten Ergebnis-Fall mit Musterwerten.
    /// </summary>
    public SimErgebnis StarteFluss(EditorModell modell, string sessionId, string pipeline, JsonElement werte, JsonElement? wahl = null,
        Guid? strom = null)
    {
        var (session, fehler, nachgespielt, hinweis) = SessionFür(modell, sessionId);
        if (session is null) return new SimErgebnis(false, fehler, [], [], [], [], 0);
        var fluss = session.Fluss.Fluesse.FirstOrDefault(f => f.Name == pipeline).Fluss;
        if (fluss is null)
            return new SimErgebnis(false, [new("error", "SIM-FLUSS", $"Pipeline '{pipeline}' nicht im Kompilat.", null)], [], Instanzen(session), [], [], nachgespielt);

        IEvent quelle;
        try { quelle = (IEvent)JsonSerializer.Deserialize(werte.GetRawText(), fluss.Regeln.AuslöserTyp, CmdJson)!; }
        catch (Exception ex)
        {
            return new SimErgebnis(false, [new("error", "SIM-WERTE", $"Werte passen nicht zu {fluss.Regeln.AuslöserTyp.Name}: {ex.Message}", null)],
                [], Instanzen(session), [], [], nachgespielt);
        }

        session.Vorher = session.Lauf.AlleZustände().ToDictionary(z => (z.Typ, z.Id), z => z.Felder);
        var wahlText = wahl is { ValueKind: JsonValueKind.Object } w ? w.GetRawText() : null;
        // Ohne Angabe ein stabiler Strom je Eingabe (Hot-Reload spielt dieselbe Geschichte mit derselben Id nach).
        var stromId = strom ?? new Guid(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes($"{pipeline}|{session.Wurzeln.Count}|{werte.GetRawText()}")));
        session.Wurzeln.Add(new Wurzel(true, pipeline, werte.GetRawText(), wahlText, stromId));
        try
        {
            return Ergebnis(session, modell, session.Fluss.Starte(pipeline, quelle, Wahl(session, modell, wahlText), stromId), nachgespielt, hinweis);
        }
        catch (InvalidOperationException ex)
        {
            session.Wurzeln.RemoveAt(session.Wurzeln.Count - 1);
            return new SimErgebnis(false, [new("error", "SIM-FLUSS", ex.Message, null)], [], Instanzen(session), [], [], nachgespielt);
        }
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
        var vorab = s.Wurzeln.Take(s.Wurzeln.Count - 1).Where(w => !w.IstFluss)
            .Select(w => Deserialisiere(s, w.Name, w.Werte))
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

        var neu = new Session { Hash = hash, Fluss = BaueLauf(k.Assembly) };
        var nachgespielt = 0;
        string? hinweis = null;
        if (alt is not null)
        {
            // Modell geändert → dieselbe Geschichte gegen die neue Logik nachspielen (still, ohne Frames).
            foreach (var w in alt.Wurzeln)
            {
                try
                {
                    if (w.IstFluss)
                    {
                        var typ = neu.Fluss.Fluesse.FirstOrDefault(f => f.Name == w.Name).Fluss?.Regeln.AuslöserTyp;
                        if (typ is null) continue;
                        var quelle = (IEvent)JsonSerializer.Deserialize(w.Werte, typ, CmdJson)!;
                        neu.Fluss.Starte(w.Name, quelle, Wahl(neu, modell, w.Wahl), w.Strom);
                    }
                    else
                    {
                        var c = Deserialisiere(neu, w.Name, w.Werte);
                        if (c is null) continue;
                        neu.Fluss.Fahre(c, Wahl(neu, modell, w.Wahl));
                        neu.LetzteWurzel = c;
                    }
                }
                catch (Exception) { continue; }   // passt nicht mehr zum Modell
                neu.Wurzeln.Add(w);
                nachgespielt++;
            }
            foreach (var kv in alt.Labels) neu.Labels.TryAdd(kv.Key, kv.Value);
            foreach (var kv in alt.Zähler) neu.Zähler[kv.Key] = Math.Max(neu.Zähler.GetValueOrDefault(kv.Key), kv.Value);
            hinweis = nachgespielt == alt.Wurzeln.Count
                ? $"Modell geändert — {nachgespielt} bisherige Eingabe(n) (Commands/Quell-Nachrichten) gegen die neue Logik nachgespielt."
                : $"Modell geändert — {nachgespielt}/{alt.Wurzeln.Count} Eingabe(n) nachgespielt (übrige passen nicht mehr zum Modell).";
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
    private SimErgebnis Ergebnis(Session s, EditorModell modell, FlussTrace lauf, int nachgespielt, string? hinweis)
    {
        var frames = new List<SimFrame>();
        SagaTrace? letzte = null;
        if (lauf.Wurzel is { } w)
        {
            s.LetzterAusgang = w.Schritte.FirstOrDefault(x => ReferenceEquals(x.Command, s.LetzteWurzel))?.Ausgang.ToList() ?? new();
            frames.AddRange(Frames(s, w, null));
            letzte = w;
        }

        var schritte = new List<SimFlussSchritt>();
        foreach (var st in lauf.Schritte)
        {
            var name = KnotenName(modell, st.Pipeline, st.Knoten);
            var fall = st.Ergebnis?.GetType().Name;
            s.Abdeckung.Add($"fl:{st.Pipeline}|{name}>{(st.Ausgang == FlussAusgang.Fall ? fall : st.Ausgang)}");
            var indizes = new List<int>();
            if (st.Kaskade is { } k)
            {
                foreach (var f in Frames(s, k, st.Pipeline)) { indizes.Add(frames.Count); frames.Add(f); }
                letzte = k;
            }
            schritte.Add(new SimFlussSchritt(st.Pipeline, st.Vorgang, name, st.Knoten, st.Art.ToString().ToLowerInvariant(), st.Typ.Name,
                Werte(st.Nachricht), st.Ein.Select(d => new SimDraht(KnotenName(modell, st.Pipeline, d.Von), d.Fall, d.Port)).ToList(),
                st.Ausgang, fall, st.Ergebnis is null || st.Art is PipelineKnotenArt.Quelle or PipelineKnotenArt.Auf ? null : Werte(st.Ergebnis),
                st.Grund, st.Element, indizes));
        }
        var vorgänge = lauf.Vorgänge.Select(v => new SimVorgang(v.Pipeline, v.Nummer, v.Erfolg, v.Grund, v.Wartend.ToList())).ToList();
        return new SimErgebnis(true, [], frames, Instanzen(s), letzte is null ? [] : Sagas(letzte),
            s.Abdeckung.OrderBy(x => x, StringComparer.Ordinal).ToList(), nachgespielt, hinweis, schritte, vorgänge);
    }

    /// <summary>Der Knoten-Name wie im Editor: Index = Deklarations-Reihenfolge = Reihenfolge der Knoten im Modell.</summary>
    private static string KnotenName(EditorModell m, string pipeline, int index)
    {
        var f = m.Fluesse.FirstOrDefault(x => x.Name == pipeline);
        return f is not null && index >= 0 && index < f.Knoten.Count ? f.Knoten[index].Name : $"#{index}";
    }

    /// <summary>
    /// Die Wahl des Editors als Antwort-Funktion: je Knoten der Fall (Ergebnis-Typ, <c>zeitlimit</c>, <c>abgelehnt</c>) und optionale
    /// Werte; ohne Wahl der erste Ergebnis-Fall der Funktion. Das Ergebnis bekommt Musterwerte (<see cref="Musterwerte"/>).
    /// </summary>
    private static Func<FlussAufruf, FlussAntwort> Wahl(Session s, EditorModell modell, string? wahlJson)
    {
        using var doc = wahlJson is null ? null : JsonDocument.Parse(wahlJson);
        var wahl = doc?.RootElement.Clone();
        return aufruf =>
        {
            var name = KnotenName(modell, aufruf.Pipeline, aufruf.Knoten);
            JsonElement? knoten = wahl is { } w && w.TryGetProperty(aufruf.Pipeline, out var p) && p.TryGetProperty(name, out var k) ? k : null;
            var fall = knoten is { } kk && kk.TryGetProperty("fall", out var fa) ? fa.GetString() : null;
            JsonElement? werte = knoten is { } kw && kw.TryGetProperty("werte", out var we) ? we : null;
            if (fall == FlussAusgang.Zeitlimit) return new FlussAntwort.Zeitlimit();
            if (fall == FlussAusgang.Abgelehnt) return new FlussAntwort.Abgelehnt($"{name} abgelehnt (gewählt)");

            // Ein Warten (strom.Warte<A, B>()) liefert eines der erwarteten Events; eine Funktion einen Fall ihres OneOf.
            List<Type> fälle;
            if (aufruf.WartetAuf.Count > 0) fälle = aufruf.WartetAuf.ToList();
            else
            {
                var funktion = s.Fluss.Fluesse.First(f => f.Name == aufruf.Pipeline).Fluss.Knoten.First(x => x.Index == aufruf.Knoten).Typ;
                fälle = ErgebnisFälle(funktion);
                if (fälle.Count == 0) throw new InvalidOperationException($"{funktion.Name}: keine Ergebnis-Fälle in der Signatur.");
            }
            var typ = fälle.FirstOrDefault(t => t.Name == fall) ?? fälle[0];
            var ort = $"{aufruf.Pipeline}|{aufruf.Vorgang}|{aufruf.Knoten}|{aufruf.Aufruf}";
            var quellen = aufruf.Eingang.Cast<object>().Concat(aufruf.Auftrag is null ? [] : [aufruf.Auftrag]).ToList();
            return new FlussAntwort.Ergebnis((IEvent)Musterwerte.Baue(typ, ort, quellen, werte));
        };
    }

    /// <summary>Die OneOf-Fälle aus <c>Task&lt;OneOf&lt;…&gt;&gt; RufeAsync(…)</c> der Funktion (Signatur, wie der Generator sie liest).</summary>
    private static List<Type> ErgebnisFälle(Type funktion)
    {
        var m = funktion.GetMethod(Funktionsvertrag.Methode);
        var oneOf = m?.ReturnType.IsGenericType == true ? m.ReturnType.GetGenericArguments()[0] : null;
        return oneOf?.IsGenericType == true ? oneOf.GetGenericArguments().ToList() : [];
    }

    private List<SimFrame> Frames(Session s, SagaTrace trace, string? pipeline)
    {
        var frames = new List<SimFrame>();
        foreach (var (st, i) in trace.Schritte.Select((x, i) => (x, i)))
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

            // Der erste Schritt einer Fluss-Kaskade ist der Command des Fluss-Knotens (Herkunft „fluss“, Saga = Pipeline).
            var ausFluss = pipeline is not null && i == 0;
            var events = st.Ausgang.Select(e => new SimEvent(e.Typ, e.Persistent, Werte(e.Event))).ToList();
            frames.Add(new SimFrame(cmdName, Werte(st.Command), ausFluss ? "fluss" : st.Ursprung == Ursprung.Saga ? "saga" : "wurzel",
                ausFluss ? pipeline : st.SagaName, st.RegelIndex, st.AggregatTyp, st.AggregatId.ToString(),
                st.Unrouted ? "—" : Label(s, st.AggregatTyp, st.AggregatId), events, st.Unrouted));
        }
        return frames;
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
        // Flüsse (§14) laufen mit: ihre Namespaces, die ihrer Funktionen und die der Knoten-Typen (Quell-Nachricht, Auftrag, Ergebnisse).
        var fnVon = modell.Funktionen.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var g in modell.Sagas)
            foreach (var st in g.Schritte)
                if (st.Rufe is { } rufe && fnVon.TryGetValue(rufe, out var gf)) ns.Add(gf.Namespace);
        foreach (var f in modell.Fluesse)
        {
            ns.Add(f.Namespace);
            foreach (var k in f.Knoten)
            {
                if (fnVon.TryGetValue(k.Typ, out var fn))
                {
                    ns.Add(fn.Namespace);
                    foreach (var r in fn.Ergebnisse.Append(fn.Auftrag)) if (nsVon.TryGetValue(r, out var rn)) ns.Add(rn);
                }
                else if (nsVon.TryGetValue(k.Typ, out var tn)) ns.Add(tn);
            }
        }
        for (var geändert = true; geändert;)
        {
            geändert = false;
            var typen = modell.Records.Where(r => ns.Contains(r.Namespace)).SelectMany(r => r.Felder.Select(f => f.Typ))
                .Concat(modell.Aggregate.SelectMany(a => a.State.Select(f => f.Typ)));
            foreach (var typ in typen)
                foreach (System.Text.RegularExpressions.Match m in bezeichner.Matches(typ))
                    if (nsVon.TryGetValue(m.Value, out var n) && ns.Add(n)) geändert = true;
        }
        // usings nur auf Namespaces, die im Kompilat vorkommen (die Leseseite fehlt hier) — sonst CS0234 an einer Datei, die z. B.
        //   eine Funktion mit Lese-Fähigkeit trägt.
        bool Bleibt(string u) => ns.Contains(u) || u == modell.Rahmen.VertragsNamespace
            || u.StartsWith("System", StringComparison.Ordinal) || u.StartsWith("Microsoft", StringComparison.Ordinal);
        return modell with
        {
            Lesen = null,   // Leseseite läuft nicht in der Simulation (eigene Assemblies, Stores)
            // Akteure/Clients sind Rechte und Leitungen nach draußen (sie nennen auch Queries der Leseseite) — die Simulation
            //   fährt die Wirkung, nicht den Handshake.
            Akteure = [],
            Clients = [],
            Records = modell.Records.Where(r => ns.Contains(r.Namespace)).Select(r => r with { Usings = r.Usings.Where(Bleibt).ToList() }).ToList(),
            Enums = modell.Enums.Where(e => ns.Contains(e.Namespace)).ToList(),
            // Funktionen laufen in der Simulation nicht (die Wahl liefert ihr Ergebnis) — ihre Lese-Fähigkeiten (Leseseite) entfallen,
            //   die Signatur (Auftrag → OneOf) bleibt.
            Funktionen = modell.Funktionen.Where(f => ns.Contains(f.Namespace)).Select(f => f with { Faehigkeiten = [] }).ToList(),
            Fluesse = modell.Fluesse.Select(f => f with
            {
                ExtraUsings = f.ExtraUsings.Where(Bleibt).ToList(),
            }).ToList(),
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

    private static FlussLaufwerk BaueLauf(Assembly asm)
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

        // Pipelines als Fluss (§14): die kompilierten IPipeline-Typen, wie der Host sie registriert.
        var fluesse = asm.GetTypes()
            .Where(t => !t.IsAbstract && t.IsClass && typeof(IPipeline).IsAssignableFrom(t))
            .Select(t => (t.Name, ((IPipeline)Activator.CreateInstance(t)!).Fluss))
            .ToList();
        return new FlussLaufwerk(lauf, fluesse);
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
