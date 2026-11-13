using System.Text.Json.Nodes;
using Abstractions;

namespace GraphExtractor;

// DIE APP-INSTANZEN der Fläche (docs/10 §10.9, docs/10 E18): je Umgebung und Rechner die Instanzen der Apps, die dort laufen
//   (apps/<instanz>.json: App, Rechner, Port, Token, Start, je Quelle ihre Werte), geprüft wie beim Start (dieselben Regeln wie
//   Infrastructure.Quellen.Eingaenge.Aus): die App gibt es, die Werte nennen nur ihre Quellen, das Token gibt es und nennt einen Akteur ihres Clients; je
//   Quelle ein Akteur des Clients, der darf, was ihre Übersetzungen geben, eine Annahme mit Pfad (je Umgebung eindeutig), die Werte gehören zur Konfig der
//   Quellart. Dazu die Kanten der Fläche: Rechner → Client (die App setzt ihn um) und Quelle → Port des Akteurs (was sie gibt). Gerechnet aus dem Board
//   und rahmen.umgebungen (roh, Platzhalter bleiben stehen); der Editor wählt nur die aktive Umgebung.
public sealed partial class Sicht
{
    /// <summary>Die Umgebungen roh (<c>rahmen.umgebungen</c>): Name → Inhalt der <c>umgebung.json</c> (mit <c>Graphen</c> und <c>Apps</c>).</summary>
    private IEnumerable<(string Name, JsonObject Inhalt)> UmgebungenRoh() =>
        Eintraege(_board["rahmen"]?["umgebungen"] as JsonObject).Where(e => e.Value is JsonObject).Select(e => (e.Key, (JsonObject)e.Value!));

    /// <summary>Die App des Codes namens <paramref name="name"/>.</summary>
    private JsonObject? AppNamens(string name) => _apps.FirstOrDefault(a => S(a, "name") == name);

    /// <summary>Die Felder der Konfig-Records einer Quellart in ihrer Reihenfolge: Name → Typ.</summary>
    private static List<(string Feld, string Typ)> QuellartFelder(JsonObject q) =>
        Alle(q["konfigs"]).SelectMany(c => Alle(c["felder"])).Select(f => (S(f, "feld"), S(f, "typ"))).ToList();

    /// <summary>Ein Wert aus der rohen Datei als Text (eine Liste kommagetrennt).</summary>
    private static string WertText(JsonNode? n) => n switch
    {
        null => "",
        JsonArray a => string.Join(", ", a.Select(x => x?.ToString() ?? "")),
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => n.ToJsonString(),
    };

    /// <summary>Die Tokens einer Umgebung (<c>Identitaeten:Tokens</c>): Token → Akteure; null ohne Abschnitt (ohne Tor).</summary>
    private static Dictionary<string, List<string>>? TokensRoh(JsonObject inhalt) =>
        inhalt[UmgebungsAbschnitte.Identitaeten]?["Tokens"] is JsonObject t
            ? Eintraege(t).ToDictionary(x => x.Key, x => x.Value is JsonArray a ? a.Select(v => WertText(v)).ToList() : AkteurTokens.Akteure(WertText(x.Value)).ToList(),
                StringComparer.Ordinal)
            : null;

    /// <summary>Die App-Instanzen einer Umgebung roh: Name → Inhalt (nur Objekte).</summary>
    private static IEnumerable<(string Name, JsonObject Inhalt)> AppsRoh(JsonObject inhalt) =>
        Eintraege(inhalt[UmgebungsAbschnitte.Apps] as JsonObject).Where(e => e.Value is JsonObject).Select(e => (e.Key, (JsonObject)e.Value!));

    /// <summary>Der Rechner einer App-Instanz (ohne Angabe <c>localhost</c>, wie im Lader).</summary>
    private static string AppRechner(JsonObject i) => WertText(i[nameof(AppInstanzKonfig.Rechner)]) is { Length: > 0 } r ? r : Rechnerregeln.Standard;

    /// <summary>Der Port einer App-Instanz (0 = keiner).</summary>
    private static int AppPort(JsonObject i) => int.TryParse(WertText(i[nameof(AppInstanzKonfig.Port)]), out var p) ? p : 0;

    /// <summary>
    /// <c>flaeche.rechner[Umgebung].apps</c>, <c>.karten</c> und <c>.kanten</c>: je App-Instanz ihre Daten samt Befunden (wie beim Start), je Rechner des
    /// Pools eine Karte der Spalte Rechner, die Kanten Rechner → Client und Quelle → Akteur. <paramref name="rechnerBefunde"/> sind die Befunde der
    /// Rechnerregeln (Pool, Port).
    /// </summary>
    private (JsonObject Apps, JsonArray Karten, JsonArray Kanten) AppInstanzenJson(JsonObject inhalt, IReadOnlyCollection<string> pool, IReadOnlyList<string> backend,
        IReadOnlyList<Rechnerbefund> rechnerBefunde)
    {
        var apps = new JsonObject();
        var kanten = new JsonArray();
        var tokens = TokensRoh(inhalt);
        var instanzen = AppsRoh(inhalt).ToList();
        // Die Pfade der Annahmen dieser Umgebung (je Pfad die Quellen, die ihn nennen) — ein Pfad gehört genau einer.
        var pfade = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (name, i) in instanzen)
            if (AppNamens(S(i, nameof(AppInstanzKonfig.App))) is { } ap)
                foreach (var q in Alle(ap["quellen"]))
                    if (QuellartNamens(S(q, "quellart")) is { } qa && B(qa, "annahme")
                        && Annahmevertrag.Normal(WertText(i[nameof(AppInstanzKonfig.Quellen)]?[S(q, "name")]?[Annahmevertrag.Pfad])) is { Length: > 0 } p)
                        (pfade.TryGetValue(p, out var l) ? l : pfade[p] = []).Add(name + "/" + S(q, "name"));
        foreach (var (name, i) in instanzen.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var befunde = rechnerBefunde.Where(b => b.Karte == Rechnerregeln.AppKarte(name)).Select(b => b.Text).ToList();
            var appName = S(i, nameof(AppInstanzKonfig.App));
            var rechner = AppRechner(i);
            var token = WertText(i[nameof(AppInstanzKonfig.Token)]);
            var ap = AppNamens(appName);
            var cl = ap == null ? null : _clients.FirstOrDefault(c => S(c, "name") == S(ap, "client"));
            foreach (var k in Eintraege(i).Select(e => e.Key).Where(k => !AppInstanzKonfig.Felder.Contains(k)))
                befunde.Add($"„{k}“ ist kein Feld (bekannt: {string.Join(", ", AppInstanzKonfig.Felder)})");
            if (appName.Length == 0) befunde.Add("keine App genannt");
            else if (ap == null) befunde.Add($"eine App „{appName}“ gibt es nicht (record {appName} : IApp<Client>)");
            var verk = cl == null ? [] : L(cl, "verkoerpert").ToList();
            var wirksam = verk;
            if (token.Length > 0 && tokens != null)
            {
                if (!tokens.TryGetValue(token, out var ta)) befunde.Add($"das Token „{token}“ gibt es nicht (Identitaeten:Tokens)");
                else
                {
                    wirksam = verk.Where(ta.Contains).ToList();
                    if (cl != null && wirksam.Count == 0) befunde.Add($"das Token „{token}“ nennt keinen Akteur von {S(cl, "name")} ({string.Join(", ", verk)})");
                }
            }
            var werteQuellen = i[nameof(AppInstanzKonfig.Quellen)] as JsonObject ?? new JsonObject();
            if (ap != null)
                foreach (var (q, _) in werteQuellen)
                    if (Alle(ap["quellen"]).All(x => S(x, "name") != q))
                        befunde.Add($"„{q}“ ist keine Quelle von {appName} (Quellen: {string.Join(", ", Alle(ap["quellen"]).Select(x => S(x, "name")).DefaultIfEmpty("keine"))})");
            var quellen = new JsonArray();
            var gibtAlle = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in ap == null ? [] : Alle(ap["quellen"]))
            {
                var qname = S(q, "name");
                var qbef = new List<string>();
                var art = S(q, "quellart");
                var qa = QuellartNamens(art);
                if (qa == null) qbef.Add($"{art} ist keine Quellart");
                var ziele = L(q, "uebersetzungen").Select(u => Verwirklichungen.FirstOrDefault(v => S(v, "name") == u)).OfType<JsonObject>().ToList();
                var gibt = ziele.Select(u => (Typ: Basis(S(u, "aus")), U: u)).ToList();
                foreach (var g in gibt) gibtAlle.Add(g.Typ);
                var akteur = wirksam.FirstOrDefault(an => _akteure.FirstOrDefault(a => S(a, "name") == an) is { } a
                    && gibt.All(g => L(a, "darf").Any(d => Basis(d) == g.Typ)));
                if (akteur == null && cl != null && gibt.Count > 0)
                    qbef.Add($"kein Akteur von {S(cl, "name")}{(token.Length > 0 ? " unter dem Token " + token : "")} darf, was sie gibt ({string.Join(", ", gibt.Select(g => g.Typ).Distinct())})");
                var w = werteQuellen[qname] as JsonObject ?? new JsonObject();
                if (qa != null)
                {
                    var felder = QuellartFelder(qa).Select(x => x.Feld).ToHashSet(StringComparer.Ordinal);
                    foreach (var (k, _) in w)
                        if (!QuelleKonfig.Rahmenfelder.Contains(k) && !felder.Contains(k))
                            qbef.Add($"„{k}“ ist kein Wert von {art} (Felder: {string.Join(", ", felder.Order(StringComparer.Ordinal).DefaultIfEmpty("keine"))})");
                    if (B(qa, "annahme"))
                    {
                        if (AppPort(i) <= 0) qbef.Add($"{art} nimmt an — die Instanz braucht einen Port, auf dem ihr Rechnerdienst annimmt");
                        var pfad = Annahmevertrag.Normal(WertText(w[Annahmevertrag.Pfad]));
                        if (pfad.Length == 0) qbef.Add($"der Wert „{Annahmevertrag.Pfad}“ fehlt — unter ihm nimmt der Rechnerdienst an (POST {Annahmevertrag.Route}/<{Annahmevertrag.Pfad}>)");
                        else if (pfade.TryGetValue(pfad, out var mit) && mit.Count > 1)
                            qbef.Add($"Pfad „{pfad}“ nennen mehrere Quellen ({string.Join(", ", mit)}) — ein Pfad gehört je Umgebung genau einer");
                    }
                }
                // Die wichtigsten Werte: die gesetzten Felder der Konfig der Quellart in ihrer Reihenfolge, höchstens zwei — ein Schlüssel nur verdeckt.
                var kurz = (qa == null ? [] : QuellartFelder(qa)).Where(f => w[f.Feld] != null).Take(2)
                    .Select(f => (JsonNode)(f.Feld + ": " + (f.Feld == Annahmevertrag.Schluessel ? "•••" : WertText(w[f.Feld])))).ToArray();
                var qo = new JsonObject
                {
                    ["name"] = qname, ["quellart"] = art, ["akteur"] = akteur ?? "", ["annahme"] = qa != null && B(qa, "annahme"),
                    ["gibt"] = new JsonArray(gibt.Select(g => (JsonNode)new JsonObject
                        { ["typ"] = g.Typ, ["uebersetzung"] = S(g.U, "name"), ["karte"] = "ue:" + S(g.U, "_id") }).ToArray()),
                    ["kurz"] = new JsonArray(kurz), ["befunde"] = Arr(qbef),
                };
                if (qa != null && QuellartKarte(qa) is { } qk) qo["quellartKarte"] = qk;
                quellen.Add(qo);
                if (akteur != null && _akteure.FirstOrDefault(a => S(a, "name") == akteur) is { } ak)
                    foreach (var typ in gibt.Select(g => L(ak, "darf").First(d => Basis(d) == g.Typ)).Distinct())
                        kanten.Add(new JsonObject
                        {
                            ["von"] = "re:" + rechner, ["nach"] = "akt:" + S(ak, "_id") + "|aus", ["typ"] = typ, ["art"] = "eingang", ["eingang"] = name + "/" + qname,
                        });
            }
            // Was die App selbst sendet: die Commands der Akteure ihres Clients ohne die, die ihre Quellen geben.
            var selbst = _akteure.Where(a => verk.Contains(S(a, "name"))).SelectMany(a => L(a, "darf")).Where(t => Sorte(t) == "command")
                .Select(Basis).Distinct().Count(t => !gibtAlle.Contains(t));
            var start = i[nameof(AppInstanzKonfig.Start)] as JsonObject;
            var o = new JsonObject
            {
                ["app"] = appName, ["appKarte"] = ap == null ? "" : "ap:" + S(ap, "_id"),
                ["client"] = cl == null ? "" : S(cl, "name"), ["clientKarte"] = cl == null ? "" : "cl:" + S(cl, "_id"),
                ["rechner"] = rechner, ["port"] = AppPort(i), ["token"] = token,
                ["start"] = start == null ? "" : WertText(start["Skript"]) is { Length: > 0 } sk ? sk : WertText(start["Projekt"]),
                ["quellen"] = quellen, ["selbst"] = selbst, ["befunde"] = Arr(befunde),
            };
            apps[name] = o;
            if (cl != null && pool.Contains(rechner))
                kanten.Add(new JsonObject { ["von"] = "re:" + rechner, ["nach"] = "cl:" + S(cl, "_id"), ["typ"] = appName, ["art"] = "app", ["app"] = name });
        }
        var karten = new JsonArray(pool.Select(r => (JsonNode)new JsonObject
        {
            ["id"] = "re:" + r, ["karte"] = "", ["art"] = "rechner", ["name"] = r, ["spalte"] = "rechner", ["ports"] = new JsonArray(),
        }).ToArray());
        return (apps, karten, kanten);
    }

    /// <summary>
    /// Die Befunde der Kanten aus App-Instanzen für die Paritätsprüfung: jede beginnt an einem Rechner des Pools und endet an einer Client-Karte bzw. an
    /// einem Port des Akteurs.
    /// </summary>
    private IEnumerable<(string Schweregrad, string Meldung)> AppKantenBefunde(FlaechenBild f)
    {
        var karte = f.Karten.ToDictionary(k => k.Id, StringComparer.Ordinal);
        foreach (var (umgebung, u) in Eintraege(RechnerJson()))
        {
            var rechner = Alle(u?["karten"]).Select(k => S(k, "id")).ToHashSet(StringComparer.Ordinal);
            foreach (var k in Alle(u?["kanten"]))
            {
                if (!rechner.Contains(S(k, "von"))) yield return ("error", $"Fläche ({umgebung}): {S(k, "art")} von {S(k, "von")} ohne Rechner-Karte");
                var ok = S(k, "art") == "app"
                    ? karte.TryGetValue(S(k, "nach"), out var c) && c.Art == "client"
                    : karte.TryGetValue(S(k, "nach"), out var a) && a.Ports.Any(p => p.Richtung == "aus" && p.Typ == S(k, "typ"));
                if (!ok) yield return ("error", $"Fläche ({umgebung}): {S(k, "art")} {S(k, "app")}{S(k, "eingang")} endet an {S(k, "nach")} ohne Karte bzw. Port {S(k, "typ")}");
            }
        }
    }
}
