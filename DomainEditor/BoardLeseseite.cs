using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Abstractions;

namespace DomainEditor;

/// <summary>
/// Board ⇄ Modell für die LESESEITE. Das Board zeigt sie in eigenen Sammlungen (<c>stores</c> mit Fn-Karten,
/// <c>projektionen</c>/<c>reader</c> mit ihren Eingängen (<c>Auf</c>/<c>Frage</c>) und „darf Store.Fn ▶"-Kanten als Fn-Ids, <c>readModels</c>).
/// Was davon Code-Fakt ist, reist in den Board-Objekten mit (<c>sig</c> je Fn/Eingang, <c>code</c> je Klasse, <c>impl</c>/<c>datei</c> je Store,
/// jeweils mit Herkunfts-Stempel). Hier wird daraus wieder das typisierte Modell:
///
///   • Kante Eingang → Fn  ⇒ Fähigkeits-Parameter des Eingangs (Typ = Fähigkeit der Fn; Name aus dem Code, sonst abgeleitet).
///   • Antworten einer Frage ⇒ OneOf der Rückgabe; bei einer bestehenden Frage wird nur das Typ-Argument getauscht (Hülle bleibt).
///     Ein Auf einer Projektion gibt nichts aus (Rückgabe <c>Task</c>).
///   • Parameter/Rückgabe einer bestehenden Fn, <c>[MindestensEinmal]</c> einer Projektion ⇒ die jeweilige Deklaration.
///   • Fn ohne Fähigkeit (neu) ⇒ Fähigkeit <c>I</c>+Methode ohne <c>Async</c> (+ Store bei Kollision) — danach Code-Fakt.
///   • Neuer Store ohne Impl ⇒ Impl-Klasse (Name ohne <c>I</c>-Präfix, Namespace = der aller Impls, sonst der des Stores).
///
/// Die Defaults gelten nur für NEUES; alles aus dem Code Gelesene kommt unverändert zurück, solange es im Board nicht
/// geändert wurde (Fixpunkt Board ⇄ Modell, geprüft von <c>--check</c>).
/// </summary>
public static class BoardLeseseite
{
    private static readonly string OneOf = typeof(OneOf<>).Name.Split('`')[0];

    /// <summary>Das volle Board-JSON (Browser-MODEL) → <see cref="EditorModell"/> inklusive Leseseite.</summary>
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
            Geteilt = B(rm, "geteilt") ?? false,
            Basen = rm["basen"] is JsonArray ? Strings(rm, "basen") : null, Attribute = N(rm, "attribute"), Herkunft = N(rm, "herkunft"),
        }).Where(r => r.Name.Length > 0 && r.Namespace.Length > 0).ToList();

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
                Klassen = A(st, "klassen").Select(x => x is JsonValue v && v.TryGetValue<int>(out var k) ? k : 0).Where(k => k > 0).ToList(),
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
            return aus.OrderBy(p => alt.FindIndex(x => Basisname(x.Typ) == Basisname(p.Typ)) is var i && i < 0 ? int.MaxValue : i).ToList();
        }
        // Ein Eingang (Auf/Frage): aus dem Code (sig) unverändert, außer Fähigkeiten und Antworten, die das Board trägt. Neu: Auf mit Herkunft.
        Handle AlsHandle(JsonNode? hd, string eingang, string standardParameter, IReadOnlyList<string> boardAusgaenge, bool projektion)
        {
            var sig = hd?["sig"];
            var sigAus = sig?["ausgangsTypen"] is JsonArray ? Strings(sig, "ausgangsTypen") : null;
            var aus = projektion ? [] : AusgaengeAbgleich(sigAus, boardAusgaenge);
            var rueck = N(sig, "rueckgabe");
            if (rueck != null && sigAus != null && !projektion && !aus.SequenceEqual(sigAus)) rueck = Umhuellen(rueck, aus);
            // input = der Eingang beim Einlesen aus dem Code; weicht er vom bearbeiteten ab, steht im Code noch der alte.
            var imCode = sig != null && N(hd, "input") is { Length: > 0 } ein && ein != eingang ? ein : null;
            return new Handle
            {
                Eingang = eingang,
                EingangImCode = imCode,
                Parameter = N(sig, "parameter") ?? standardParameter,
                HerkunftParameter = sig != null ? N(sig, "herkunftParameter") : projektion ? "h" : null,
                AbbruchParameter = N(sig, "abbruchParameter"),
                Faehigkeiten = Faehigkeiten(hd),
                Ausgaenge = aus,
                Rueckgabe = rueck, Modifikatoren = N(sig, "modifikatoren"),
                // Neu: der Code-/LLM-Entwurf (Board-Feld „entwurf") wird der Rumpf; aus dem Code: der Rumpf verbatim.
                Rumpf = sig == null ? N(hd, "entwurf") : N(sig, "rumpfCode"), Ausdruck = N(sig, "ausdruck"), Datei = N(sig, "datei"),
                Herkunft = N(sig, "herkunft"),
            };
        }

        // ── Projektionen ──
        var konsumenten = A(b, "projektionen").Select(p =>
        {
            var code = p?["code"];
            return new Konsument
            {
                Name = S(p, "name"), Namespace = S(p, "namespace"), MindestensEinmal = B(p, "mindestensEinmal") ?? false,
                Handles = A(p, "handles").Where(hd => S(hd, "event").Length > 0)
                    .Select(hd => AlsHandle(hd, S(hd, "event"), "e", [], projektion: true)).ToList(),
                Doku = N(code, "doku"), Typart = N(code, "typart"),
                Basen = code?["basen"] is JsonArray ? Strings(code, "basen") : null,
                Attribute = N(code, "attribute"), Zusatz = N(code, "zusatz"), Usings = Strings(code, "usings"), Datei = N(code, "datei"),
                Herkunft = N(code, "herkunft"), Zustand = code?["zustand"] is JsonArray ? Strings(code, "zustand") : null,
            };
        }).Where(k => k.Name.Length > 0 && k.Namespace.Length > 0).OrderBy(k => k.Name, StringComparer.Ordinal).ToList();

        // ── Reader (welche Projektionen dahinter stehen, folgt aus den Lese-Fähigkeiten — kein Board-Feld wird zurückgeschrieben) ──
        var reader = A(b, "reader").Select(r =>
        {
            var code = r?["code"];
            return new Leser
            {
                Name = S(r, "name"), Namespace = S(r, "namespace"),
                Handles = A(r, "handles").Where(hd => S(hd, "query").Length > 0)
                    .Select(hd => AlsHandle(hd, S(hd, "query"), "q", Strings(hd, "responses"), projektion: false)).ToList(),
                Doku = N(code, "doku"), Typart = N(code, "typart"),
                Basen = code?["basen"] is JsonArray ? Strings(code, "basen") : null,
                Attribute = N(code, "attribute"), Zusatz = N(code, "zusatz"), Usings = Strings(code, "usings"), Datei = N(code, "datei"),
                Herkunft = N(code, "herkunft"),
            };
        }).Where(r => r.Name.Length > 0 && r.Namespace.Length > 0).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

        var records = basis.Records.Where(r => r.Kind != RecordArt.ReadModel).Select(Bereinigt).Concat(readModels).ToList();
        return basis with
        {
            Records = records,
            Lesen = new Leseseite { Stores = stores, Konsumenten = konsumenten, Reader = reader },
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

    /// <summary>Die Antworten nach dem Board: aus dem Code die Reihenfolge der Signatur (was bleibt), neue hinten dran.</summary>
    private static List<string> AusgaengeAbgleich(IReadOnlyList<string>? sig, IReadOnlyList<string> board) =>
        sig == null ? board.ToList() : sig.Where(board.Contains).Concat(board.Where(t => !sig.Contains(t))).ToList();

    /// <summary>Neue Antworten in die bestehende Rückgabe einer Frage setzen: die Hülle bleibt (Task/ValueTask oder keine), nur der Antwort-Typ wechselt.</summary>
    private static string Umhuellen(string alt, IReadOnlyList<string> aus)
    {
        // Keine Antwort: eine Frage braucht immer eine — bleibt.
        if (aus.Count == 0) return alt;
        var inner = aus.Count == 1 ? aus[0] : $"{OneOf}<{string.Join(", ", aus)}>";
        var w = Wrapper0(alt);
        return w is "Task" or "ValueTask" && Innen(alt) != null ? $"{w}<{inner}>" : inner;
    }

    private static string Wrapper0(string typ) => typ.Contains('<') ? typ[..typ.IndexOf('<')].Trim() : "Task";
    private static string? Innen(string typ) => typ.Contains('<') && typ.EndsWith('>') ? typ[(typ.IndexOf('<') + 1)..^1] : null;
    /// <summary>Typ-Text ohne Leerzeichen und ohne Namespace-Qualifizierer — Vergleich „derselbe Typ?".</summary>
    public static string Norm(string typ) => Regex.Replace(Regex.Replace(typ ?? "", @"\s+", ""), @"\b(?:[A-Za-z_]\w*\.)+(?=[A-Za-z_])", "");

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
