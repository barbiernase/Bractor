using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Abstractions;

namespace DomainEditor;

/// <summary>
/// Board ⇄ Modell für die LESESEITE und den BETRIEB. Das Board zeigt sie in eigenen Sammlungen (<c>stores</c> mit Fn-Karten,
/// <c>projektionen</c>/<c>reaktionen</c>/<c>reader</c>/<c>pipelines</c> mit Handles und „darf Store.Fn ▶"-Kanten als Fn-Ids,
/// <c>readModels</c>, <c>triggers</c>, <c>selbstNachrichten</c>). Was davon Code-Fakt ist, reist in den Board-Objekten mit
/// (<c>sig</c> je Fn/Handle, <c>code</c> je Klasse/Trigger, <c>impl</c>/<c>datei</c> je Store, <c>bindung</c> je Trigger, jeweils
/// mit Herkunfts-Stempel). Hier wird daraus wieder das typisierte Modell:
///
///   • Kante Handle → Fn  ⇒ Fähigkeits-Parameter des Handles (Typ = Fähigkeit der Fn; Name aus dem Code, sonst abgeleitet).
///   • Ausgänge eines Handles (Responses / sendet / veröffentlicht / erzeugt Trigger / plant Selbst) ⇒ OneOf der Rückgabe;
///     bei einem bestehenden Handle wird nur das Typ-Argument der Rückgabe getauscht (Form und Wrapper bleiben).
///   • Parameter/Rückgabe einer bestehenden Fn, Pull/Append/TrackDeps/SubscriberId/Projektion ⇒ die jeweilige Deklaration.
///   • Fn ohne Fähigkeit (neu) ⇒ Fähigkeit <c>I</c>+Methode ohne <c>Async</c> (+ Store bei Kollision) — danach Code-Fakt.
///   • Neuer Store ohne Impl ⇒ Impl-Klasse (Name ohne <c>I</c>-Präfix, Namespace = der aller Impls, sonst der des Stores).
///   • Neuer Trigger ⇒ Trigger-Record; mit Modus + Ort ⇒ neue Ingress-Bindung (nach dem Vorbild einer bestehenden).
///   • Neue Selbst-Nachricht (Self-Tick) ⇒ Record <c>: IPipelineSelfMessage</c>.
///
/// Die Defaults gelten nur für NEUES; alles aus dem Code Gelesene kommt unverändert zurück, solange es im Board nicht
/// geändert wurde (Fixpunkt Board ⇄ Modell, geprüft von <c>--check</c>).
/// </summary>
public static class BoardLeseseite
{
    private static readonly string Selbst = typeof(Selbst<>).Name.Split('`')[0];
    private static readonly string Frist = typeof(Frist<>).Name.Split('`')[0];
    private static readonly string FristStorno = typeof(FristStorno<>).Name.Split('`')[0];
    private static readonly string OneOf = typeof(OneOf<>).Name.Split('`')[0];

    /// <summary>Das volle Board-JSON (Browser-MODEL) → <see cref="EditorModell"/> inklusive Leseseite, Betriebs-Records und Ingress.</summary>
    public static EditorModell AusBoard(string boardJson)
    {
        var basis = EditorModell.AusJson(boardJson);
        var b = JsonNode.Parse(boardJson)?.AsObject() ?? new JsonObject();
        var rahmen = basis.Rahmen;

        // ── ReadModels (Board-Sammlung readModels) → Records der Art readmodel ──
        var readModels = A(b, "readModels").Select(rm => new Record
        {
            Name = S(rm, "name"), Kind = RecordArt.ReadModel, Namespace = S(rm, "namespace"),
            Felder = Felder(rm),
            Doku = N(rm, "doku"), Zusatz = N(rm, "zusatz"), Usings = Strings(rm, "usings"), Datei = N(rm, "datei"),
            Typart = N(rm, "typart") is { } ta && ta != "public record" ? ta : null,
            OhneParameterliste = B(rm, "ohneParameterliste") ?? false,
            Basen = rm["basen"] is JsonArray ? Strings(rm, "basen") : null, Attribute = N(rm, "attribute"), Herkunft = N(rm, "herkunft"),
        }).Where(r => r.Name.Length > 0 && r.Namespace.Length > 0).ToList();

        // ── Trigger-Nachrichten (Board-Sammlung triggers) → Records der Art trigger; Felder im Board editierbar ──
        var trigger = A(b, "triggers").Select(t =>
        {
            var name = N(t, "msgName") ?? S(t, "name");
            var code = t["code"] is JsonObject co ? EditorModell.AusJsonRecord(co.ToJsonString()) : null;
            var felder = t["felder"] is JsonArray ? Felder(t) : code?.Felder ?? [];
            return code != null ? code with { Felder = felder }
                : new Record { Name = name, Kind = RecordArt.Trigger, Namespace = S(t, "namespace"), Felder = felder };
        }).Where(r => r.Name.Length > 0 && r.Namespace.Length > 0).GroupBy(r => r.Name).Select(g => g.First()).ToList();

        // ── Selbst-Nachrichten: aus dem Code (selbstNachrichten) + neu im Editor geplante (Self-Tick) ──
        var selbst = A(b, "selbstNachrichten").Select(x => EditorModell.AusJsonRecord(x.ToJsonString())).ToList();

        // ── Stores: Fn-Karten → Fähigkeiten; Fn-Id → Fähigkeit (für die Handle-Kanten) ──
        var belegt = new HashSet<string>(StringComparer.Ordinal);
        foreach (var st in A(b, "stores"))
            foreach (var f in A(st, "writeFns").Concat(A(st, "readFns")))
                if (N(f, "faehigkeit") is { Length: > 0 } fa) belegt.Add(fa);
        var faehigkeitVonFn = new Dictionary<string, Faehigkeit>(StringComparer.Ordinal);
        var stores = new List<Store>();
        foreach (var st in A(b, "stores"))
        {
            var name = S(st, "name");
            var ns = S(st, "namespace");
            if (name.Length == 0 || ns.Length == 0) continue;
            var fns = new List<Faehigkeit>();
            foreach (var (f, lesen) in A(st, "writeFns").Select(f => (f, false)).Concat(A(st, "readFns").Select(f => (f, true))))
            {
                var methode = S(f, "name");
                if (methode.Length == 0) continue;
                var faeh = AlsFaehigkeit(f, lesen, name, belegt);
                fns.Add(faeh);
                if (N(f, "_id") is { } id) faehigkeitVonFn[id] = faeh;
            }
            var datei = N(st, "datei");
            var impl = st?["impl"] is JsonObject io
                ? new StoreImpl { Name = S(io, "name"), Namespace = S(io, "namespace"), Datei = N(io, "datei"), SchreibDatei = N(io, "schreibDatei"), LeseDatei = N(io, "leseDatei") }
                : datei == null ? NeueImpl(name, ns, rahmen) : null;   // bestehender Store ohne (eindeutige) Impl: nichts raten
            stores.Add(new Store
            {
                Name = name, Namespace = ns, Doku = N(st, "doku"), Datei = datei, IstBuendel = B(st, "istBuendel") ?? true,
                Fns = fns, Impl = impl,
            });
        }

        List<Parameter> Faehigkeiten(JsonNode? hd)
        {
            var alt = A(hd?["sig"], "faehigkeitParameter").Select(p => new Parameter { Typ = S(p, "typ"), Name = S(p, "name") }).ToList();
            var aus = new List<Parameter>();
            foreach (var id in Strings(hd, "fns"))
            {
                if (!faehigkeitVonFn.TryGetValue(id, out var fa) || aus.Any(p => Basisname(p.Typ) == fa.Name)) continue;
                aus.Add(alt.FirstOrDefault(p => Basisname(p.Typ) == fa.Name) ?? new Parameter { Typ = fa.Name, Name = ParameterName(fa.Name) });
            }
            // Im Auftrag eines Akteur-Dienstes (Board-Feld „akteur"): ein Parameter wie eine Fähigkeit; Reihenfolge wie im Code.
            if (N(hd, "akteur") is { Length: > 0 } akteur && aus.All(p => Basisname(p.Typ) != akteur))
                aus.Add(alt.FirstOrDefault(p => Basisname(p.Typ) == akteur) ?? new Parameter { Typ = akteur, Name = ParameterName(akteur) });
            return aus.OrderBy(p => alt.FindIndex(x => Basisname(x.Typ) == Basisname(p.Typ)) is var i && i < 0 ? int.MaxValue : i).ToList();
        }
        // Ein Handle: aus dem Code (sig) unverändert, außer Fähigkeiten und Ausgänge, die das Board trägt.
        Handle AlsHandle(JsonNode? hd, string eingang, string standardParameter, IReadOnlyList<string> boardAusgaenge, Wrapper art)
        {
            var sig = hd?["sig"];
            var sigAus = sig?["ausgangsTypen"] is JsonArray ? Strings(sig, "ausgangsTypen") : null;
            // Fristen editiert das Board, sobald der Handle sie trägt (fristen[]); ältere Boards ohne das Feld lassen sie, wie der Code sie hat.
            var aus = AusgaengeAbgleich(sigAus, boardAusgaenge, art, fristenImBoard: hd?["fristen"] is JsonArray);
            var rueck = N(sig, "rueckgabe");
            if (rueck != null && sigAus != null && !aus.SequenceEqual(sigAus)) rueck = Umhuellen(rueck, aus, art);
            return new Handle
            {
                Eingang = eingang,
                Parameter = N(sig, "parameter") ?? standardParameter,
                Kontext = sig?["kontext"] is JsonArray ? Strings(sig, "kontext") : null,
                Faehigkeiten = Faehigkeiten(hd),
                Ausgaenge = aus,
                Rueckgabe = rueck, Modifikatoren = N(sig, "modifikatoren"),
                // Neu: der Code-/LLM-Entwurf (Board-Feld „entwurf") wird der Rumpf; aus dem Code: der Rumpf verbatim.
                Rumpf = sig == null ? N(hd, "entwurf") : N(sig, "rumpfCode"), Ausdruck = N(sig, "ausdruck"), Datei = N(sig, "datei"),
                Herkunft = N(sig, "herkunft"),
            };
        }

        // ── Projektionen + Reaktionen (dieselbe Form) ──
        var konsumenten = A(b, "projektionen").Concat(A(b, "reaktionen")).Select(p =>
        {
            var code = p?["code"];
            var pull = B(p, "pull") ?? true;
            var append = B(p, "append") ?? false;
            return new Konsument
            {
                Name = S(p, "name"), Namespace = S(p, "namespace"),
                SubscriberId = N(p, "subscriberId") ?? (code == null ? S(p, "name") : null),
                Pull = pull, Append = append,
                Handles = A(p, "handles").Where(hd => S(hd, "event").Length > 0)
                    .Select(hd => AlsHandle(hd, S(hd, "event"), "evt", Strings(hd, "sends").Concat(Strings(hd, "publishes")).Distinct().ToList(), Wrapper.Konsument)).ToList(),
                Doku = N(code, "doku"), Typart = N(code, "typart"),
                // Basisliste aus dem Code, die Marker nach den Flags (Pull/Append) nachgezogen.
                Basen = code?["basen"] is JsonArray ? Marker(Marker(Strings(code, "basen"), nameof(IPullSubscriber), pull), nameof(IAppendProjektion), append) : null,
                Attribute = N(code, "attribute"), Zusatz = N(code, "zusatz"), Usings = Strings(code, "usings"), Datei = N(code, "datei"),
                Herkunft = N(code, "herkunft"), Zustand = code?["zustand"] is JsonArray ? Strings(code, "zustand") : null,
            };
        }).Where(k => k.Name.Length > 0 && k.Namespace.Length > 0).OrderBy(k => k.Name, StringComparer.Ordinal).ToList();

        // ── Reader (ohne angedockte Projektion kein IReader<T> — nicht platzierbar, übergangen) ──
        var reader = A(b, "reader").Where(r => S(r, "projektion").Length > 0).Select(r =>
        {
            var code = r?["code"];
            var projektion = S(r, "projektion");
            var ireader = typeof(IReader<>).Name.Split('`')[0];
            return new Leser
            {
                Name = S(r, "name"), Namespace = S(r, "namespace"), Projektion = projektion, TrackDeps = B(r, "trackDeps") ?? true,
                Handles = A(r, "handles").Where(hd => S(hd, "query").Length > 0)
                    .Select(hd => AlsHandle(hd, S(hd, "query"), "query", Strings(hd, "responses"), Wrapper.Reader)).ToList(),
                Doku = N(code, "doku"), Typart = N(code, "typart"),
                // IReader<P>: das Typ-Argument folgt der Projektion im Board.
                Basen = code?["basen"] is JsonArray ? Strings(code, "basen").Select(x => Basisname(x) == ireader ? $"{ireader}<{projektion}>" : x).ToList() : null,
                Attribute = N(code, "attribute"), Zusatz = N(code, "zusatz"), Usings = Strings(code, "usings"), Datei = N(code, "datei"),
                Herkunft = N(code, "herkunft"),
            };
        }).Where(r => r.Name.Length > 0 && r.Namespace.Length > 0).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

        // ── Pipelines: Eingang (Trigger/Event/Selbst) → Commands, Trigger, Selbst<T> (+ Frist<T> aus dem Code) ──
        var triggerName = A(b, "triggers").Where(t => N(t, "_id") != null).ToDictionary(t => N(t, "_id")!, t => N(t, "msgName") ?? S(t, "name"));
        string Eingang(JsonNode hd) => N(hd, "input") ?? (S(hd, "inputKind") switch
        {
            "trigger" => N(hd, "trigId") is { } tid && triggerName.TryGetValue(tid, out var tn) ? tn : "",
            "self" => S(hd, "selfName"),
            _ => S(hd, "event"),
        });
        var pipelines = A(b, "pipelines").Select(p =>
        {
            var code = p?["code"];
            return new PipelineKarte
            {
                Name = S(p, "name"), Namespace = S(p, "namespace"),
                PipelineId = N(p, "pipelineId") is { Length: > 0 } pid ? pid : code == null ? S(p, "name") : null,
                Konfigs = Strings(p, "konfigs"),
                // Ausgänge nach Typ: Command (sofort), Trigger, veröffentlichtes Event, Selbst<T>, Frist<T>/FristStorno<T> (Command per Frist).
                Handles = A(p, "handles").Where(hd => Eingang(hd).Length > 0).Select(hd => AlsHandle(hd, Eingang(hd), "ctx",
                    Strings(hd, "sends").Concat(Strings(hd, "emits")).Concat(Strings(hd, "publishes"))
                        .Concat(A(hd, "schedules").Select(sc => S(sc, "name")).Where(x => x.Length > 0).Select(x => $"{Selbst}<{x}>"))
                        .Concat(A(hd, "fristen").Where(f => S(f, "command").Length > 0)
                            .Select(f => $"{(S(f, "art") == "storno" ? FristStorno : Frist)}<{S(f, "command")}>")).Distinct().ToList(),
                    Wrapper.Pipeline) with { Parameter = hd["sig"] != null ? N(hd["sig"], "parameter") ?? "ein" : ParameterName(Eingang(hd)) }).ToList(),
                Doku = N(code, "doku"), Typart = N(code, "typart"),
                Basen = code?["basen"] is JsonArray ? Strings(code, "basen") : null,
                Attribute = N(code, "attribute"), Zusatz = N(code, "zusatz"), Usings = Strings(code, "usings"),
                Datei = N(code, "datei") ?? N(p, "datei"), Herkunft = N(p, "herkunft") ?? N(code, "herkunft"),
                Zustand = code?["zustand"] is JsonArray ? Strings(code, "zustand") : null,
            };
        }).Where(p => p.Name.Length > 0 && p.Namespace.Length > 0).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

        // Neue Selbst-Nachrichten: jede geplante/empfangene Selbst-Nachricht, die weder Record noch Code-Eingang ist.
        var bekannt = basis.Records.Select(r => r.Name).Concat(readModels.Select(r => r.Name)).Concat(trigger.Select(r => r.Name))
            .Concat(selbst.Select(r => r.Name)).ToHashSet(StringComparer.Ordinal);
        var codeEingaenge = A(b, "pipelines").SelectMany(p => A(p, "handles")).Where(hd => hd["sig"] != null).Select(Eingang).ToHashSet(StringComparer.Ordinal);
        foreach (var p in pipelines)
            foreach (var name in p.Handles.SelectMany(h => h.Ausgaenge).Where(a => a.StartsWith(Selbst + "<", StringComparison.Ordinal)).Select(a => a[(Selbst.Length + 1)..^1])
                         .Concat(A(b, "pipelines").Where(x => S(x, "name") == p.Name).SelectMany(x => A(x, "handles"))
                             .Where(hd => S(hd, "inputKind") == "self" && hd["sig"] == null).Select(hd => S(hd, "selfName"))))
                if (name.Length > 0 && !bekannt.Contains(name) && !codeEingaenge.Contains(name))
                {
                    selbst.Add(new Record { Name = name, Kind = RecordArt.Selbst, Namespace = p.Namespace, OhneParameterliste = true });
                    bekannt.Add(name);
                }

        // ── Ingress: Bindungen aus dem Code + neue (Trigger mit Modus und Ort, ohne Bindung) ──
        var ingress = new List<IngressBindung>();
        foreach (var t in A(b, "triggers"))
        {
            var msg = N(t, "msgName") ?? S(t, "name");
            var modus = N(t, "modus");
            if (t["bindung"] is JsonObject bd)
                ingress.Add(new IngressBindung
                {
                    Trigger = msg, Modus = N(bd, "modus") ?? modus ?? "", Ort = N(bd, "ort"),
                    Datei = N(bd, "datei"), Anweisung = N(bd, "anweisung"), TypArgument = N(bd, "typArgument"), OrtArgument = N(bd, "ortArgument"),
                });
            else if (modus is { Length: > 0 } && (N(t, "route") ?? N(t, "intervall") ?? N(t, "pfad")) is { Length: > 0 } ort)
                ingress.Add(new IngressBindung { Trigger = msg, Modus = modus, Ort = ort });
        }

        var records = basis.Records.Where(r => r.Kind is not (RecordArt.ReadModel or RecordArt.Trigger or RecordArt.Selbst))
            .Select(Bereinigt).Concat(readModels).Concat(trigger).Concat(selbst).ToList();
        return basis with
        {
            Records = records,
            Lesen = new Leseseite { Stores = stores, Konsumenten = konsumenten, Reader = reader, Pipelines = pipelines },
            Ingress = ingress,
        };
    }

    // ── Fähigkeit: aus dem Code (sig) — Parameter/Rückgabe folgen dem Board, wenn dort geändert ──
    private static Faehigkeit AlsFaehigkeit(JsonNode f, bool lesen, string store, HashSet<string> belegt)
    {
        var methode = S(f, "name");
        var sig = f["sig"];
        var fa = N(f, "faehigkeit") is { Length: > 0 } x ? x : NeueFaehigkeit(methode, store, belegt);
        var rueck = N(f, "rueckgabe");
        var boardParams = A(f, "params").Select(p => new Parameter { Typ = S(p, "typ"), Name = S(p, "name") }).Where(p => p.Typ.Length > 0 && p.Name.Length > 0).ToList();
        if (sig == null)
            return new Faehigkeit
            {
                Name = fa, Methode = methode, Lesen = lesen, ImplRumpf = N(f, "entwurf"),
                Rueckgabe = lesen && !string.IsNullOrWhiteSpace(rueck) ? $"Task<{rueck}>" : "Task", Parameter = boardParams,
            };
        var sigParams = A(sig, "parameter").Select(p => new Parameter { Typ = S(p, "typ"), Name = S(p, "name"), Standard = N(p, "standard") }).ToList();
        var sigRueck = N(sig, "rueckgabe") ?? "Task";
        // Geändert = Namen/Typen (qualifizierungs-unabhängig) weichen ab. Unverändertes bleibt wörtlich (inkl. Default).
        var paramsGleich = f["params"] is not JsonArray
            || boardParams.Select(p => (p.Name, Norm(p.Typ))).SequenceEqual(sigParams.Select(p => (p.Name, Norm(p.Typ))));
        var rueckGleich = !lesen || string.IsNullOrWhiteSpace(rueck) || Norm(Innen(sigRueck) ?? sigRueck) == Norm(rueck);
        return new Faehigkeit
        {
            Name = fa, Methode = methode, Lesen = lesen,
            Namespace = N(sig, "namespace"), Doku = N(sig, "doku"), Datei = N(sig, "datei"), Herkunft = N(sig, "herkunft"),
            Parameter = paramsGleich ? sigParams
                : boardParams.Select(p => sigParams.FirstOrDefault(s => s.Name == p.Name && Norm(s.Typ) == Norm(p.Typ)) ?? p).ToList(),
            Rueckgabe = rueckGleich ? sigRueck : Wrapper0(sigRueck) + "<" + rueck + ">",
        };
    }

    /// <summary>Welche Form ein Handle hat — bestimmt Wrapper und welche Ausgänge das Board editiert.</summary>
    private enum Wrapper { Konsument, Reader, Pipeline }

    /// <summary>
    /// Die Ausgänge nach dem Board: aus dem Code die Reihenfolge der Signatur (was bleibt), neue hinten dran. Fristen
    /// (<c>Frist&lt;T&gt;</c>/<c>FristStorno&lt;T&gt;</c>) editiert das Board, wenn der Handle sie trägt (<paramref name="fristenImBoard"/>);
    /// sonst bleiben sie, wie der Code sie hat.
    /// </summary>
    private static List<string> AusgaengeAbgleich(IReadOnlyList<string>? sig, IReadOnlyList<string> board, Wrapper art, bool fristenImBoard = false)
    {
        if (sig == null) return board.ToList();
        bool Editierbar(string t) => art != Wrapper.Pipeline || fristenImBoard
            || !(t.StartsWith(Frist + "<", StringComparison.Ordinal) || t.StartsWith(FristStorno + "<", StringComparison.Ordinal));
        return sig.Where(t => !Editierbar(t) || board.Contains(t)).Concat(board.Where(t => !sig.Contains(t))).ToList();
    }

    /// <summary>Neue Ausgänge in die bestehende Rückgabe setzen: der Wrapper bleibt (Task/IAsyncEnumerable/IEnumerable/ValueTask), nur das Typ-Argument wechselt.</summary>
    private static string Umhuellen(string alt, IReadOnlyList<string> aus, Wrapper art)
    {
        // Pipelines: der Dispatch erkennt nur OneOf<…> — auch bei einem Ausgang.
        var inner = aus.Count == 1 && art != Wrapper.Pipeline ? aus[0] : $"{OneOf}<{string.Join(", ", aus)}>";
        // Keine Ausgabe ⇒ Task (Projektion/Reaktion/Pipeline: nur Effekte); ein Reader braucht immer eine Antwort ⇒ bleibt.
        if (aus.Count == 0) return art == Wrapper.Reader ? alt : "Task";
        var w = Wrapper0(alt);
        if (art != Wrapper.Reader && Innen(alt) == null) w = "IAsyncEnumerable";   // bisher Task (nichts) → Strom
        return $"{w}<{inner}>";
    }

    private static string Wrapper0(string typ) => typ.Contains('<') ? typ[..typ.IndexOf('<')].Trim() : "Task";
    private static string? Innen(string typ) => typ.Contains('<') && typ.EndsWith('>') ? typ[(typ.IndexOf('<') + 1)..^1] : null;
    /// <summary>Typ-Text ohne Leerzeichen und ohne Namespace-Qualifizierer — Vergleich „derselbe Typ?".</summary>
    public static string Norm(string typ) => Regex.Replace(Regex.Replace(typ ?? "", @"\s+", ""), @"\b(?:[A-Za-z_]\w*\.)+(?=[A-Za-z_])", "");

    /// <summary>Marker in der Basisliste setzen/entfernen (die übrigen Basen bleiben wörtlich).</summary>
    private static List<string> Marker(List<string> basen, string marker, bool an)
    {
        var hat = basen.Any(x => Basisname(x) == marker);
        if (an && !hat) return basen.Append(marker).ToList();
        if (!an && hat) return basen.Where(x => Basisname(x) != marker).ToList();
        return basen;
    }

    /// <summary>Leere Strings aus dem Browser (Eingabefelder) sind „nicht gesetzt".</summary>
    private static Record Bereinigt(Record r) => r with
    {
        Felder = r.Felder.Select(f => f with
        {
            Standard = string.IsNullOrEmpty(f.Standard) ? null : f.Standard, Zugriff = string.IsNullOrEmpty(f.Zugriff) ? null : f.Zugriff,
            Ausdruck = string.IsNullOrEmpty(f.Ausdruck) ? null : f.Ausdruck, ElementTyp = string.IsNullOrEmpty(f.ElementTyp) ? null : f.ElementTyp,
        }).ToList(),
    };

    private static List<Feld> Felder(JsonNode? n) => A(n, "felder").Select(f => new Feld
    {
        Name = S(f, "name"), Typ = S(f, "typ"), Standard = Leer(N(f, "standard")), Zugriff = Leer(N(f, "zugriff")),
        Pflicht = B(f, "pflicht") ?? false, ElementTyp = Leer(N(f, "elementTyp")),
    }).Where(f => f.Name.Length > 0 && f.Typ.Length > 0).ToList();

    private static string? Leer(string? s) => string.IsNullOrEmpty(s) ? null : s;

    // ── Defaults für NEUES (im Editor gezeichnet) — danach Code-Fakt, der Extractor rät sie nie ──────────────────

    /// <summary>Fähigkeits-Name einer neuen Fn: <c>I</c> + Methode ohne <c>Async</c>; belegt ⇒ + Store-Name (ohne <c>I</c>).</summary>
    public static string NeueFaehigkeit(string methode, string store, ISet<string> belegt)
    {
        var kern = methode.EndsWith("Async", StringComparison.Ordinal) && methode.Length > 5 ? methode[..^5] : methode;
        var name = "I" + kern;
        if (belegt.Contains(name)) name = "I" + kern + OhneI(store);
        for (var n = 2; belegt.Contains(name); n++) name = "I" + kern + OhneI(store) + n;
        belegt.Add(name);
        return name;
    }

    /// <summary>Impl-Klasse eines neuen Stores: Name ohne <c>I</c>-Präfix (sonst + <c>Impl</c>), Namespace aller Impls oder des Stores.</summary>
    public static StoreImpl NeueImpl(string store, string ns, Rahmen rahmen) => new()
    {
        Name = HatIPraefix(store) ? store[1..] : store + "Impl",
        Namespace = rahmen.StoreImplNamespace ?? ns,
    };

    /// <summary>Parametername einer Fähigkeit/eines Typs: ohne <c>I</c>-Präfix, erster Buchstabe klein (<c>IUpsertModell</c> → <c>upsertModell</c>).</summary>
    public static string ParameterName(string faehigkeit)
    {
        var s = OhneI(Basisname(faehigkeit));
        return s.Length == 0 ? "faehigkeit" : char.ToLowerInvariant(s[0]) + s[1..];
    }

    private static bool HatIPraefix(string s) => s.Length > 1 && s[0] == 'I' && char.IsUpper(s[1]);
    private static string OhneI(string s) => HatIPraefix(s) ? s[1..] : s;
    /// <summary>Letztes Segment eines (evtl. qualifizierten/generischen) Typnamens.</summary>
    public static string Basisname(string typ) => typ.Split('<')[0].Split('.').Last().TrimEnd('?').Trim();

    // ── JSON-Helfer ──
    private static IEnumerable<JsonNode> A(JsonNode? n, string k) => (n?[k] as JsonArray)?.Where(x => x != null).Select(x => x!) ?? [];
    private static string S(JsonNode? n, string k) => N(n, k) ?? "";
    private static string? N(JsonNode? n, string k) => n?[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    private static bool? B(JsonNode? n, string k) => n?[k] is JsonValue v && v.TryGetValue<bool>(out var x) ? x : null;
    private static List<string> Strings(JsonNode? n, string k) => A(n, k).Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)
        .Where(x => x != null).Select(x => x!).ToList();
}
