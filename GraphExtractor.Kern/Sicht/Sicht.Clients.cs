using System.Text.Json.Nodes;
using Abstractions;

namespace GraphExtractor;

// DIE SPALTE CLIENTS der Fläche (docs/10 §10.9, Plan Clients und Quellen Schritt 4): je Client (record X : IClient, Code) eine Karte, darin je Umgebung
//   seine Eingänge (Konfig, Clients:<Client>:Eingaenge:<eingang> in umgebung.json) und seine App-Verbindung (Token, Adresse, URLs, Worker). Von
//   jedem Eingang eine Kante zu dem Akteur, für den er hineingibt — an den Port der Nachricht, die die Verwirklichung (Übersetzung der Akteur-Seite) aus
//   der Meldung seiner Quellart macht. Dazu die Befunde, die der Start melden würde (dieselben Regeln wie Infrastructure.Quellen.Eingaenge.Aus): Client
//   und Akteur gibt es, der Client verkörpert ihn, die Art ist eine Quellart, je Meldung eine Übersetzung auf ein IDarf des Akteurs, im Cluster ein
//   Rechner (eine Annahme nennt keinen, dafür einen Pfad, je Umgebung eindeutig) — und die Werte gehören zur Konfig der Quellart. Gerechnet aus dem Board und rahmen.umgebungen (roh, Platzhalter bleiben stehen); der
//   Editor wählt nur die Daten der aktiven Umgebung.
public sealed partial class Sicht
{
    /// <summary>Die Umgebungen roh (<c>rahmen.umgebungen</c>): Name → Inhalt der <c>umgebung.json</c>.</summary>
    private IEnumerable<(string Name, JsonObject Inhalt)> UmgebungenRoh() =>
        Eintraege(_board["rahmen"]?["umgebungen"] as JsonObject).Where(e => e.Value is JsonObject).Select(e => (e.Key, (JsonObject)e.Value!));

    /// <summary>Der Client des Codes, dessen Handshake-Name <paramref name="name"/> ist (der Schlüssel unter <c>Clients</c>).</summary>
    private JsonObject? ClientNamens(string name) =>
        _clients.FirstOrDefault(c => Clientvertragsregeln.ClientName(S(c, "name")) == name);

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

    /// <summary>
    /// Ein Eingang geprüft wie beim Start: die Nachrichten, die er dem Akteur gibt (je passender Übersetzung ihr Ziel), und die Befunde. Leer heißt: er
    /// startet so.
    /// </summary>
    private (List<(string Typ, string Uebersetzung)> Gibt, List<string> Befunde) EingangPruefen(string client, JsonObject? c, string name, JsonObject e, bool cluster,
        IReadOnlyDictionary<string, List<string>> pfade)
    {
        var befunde = new List<string>();
        var gibt = new List<(string, string)>();
        var art = S(e, nameof(EingangKonfig.Art));
        var akteur = S(e, nameof(EingangKonfig.Akteur));
        var a = _akteure.FirstOrDefault(x => S(x, "name") == akteur);
        if (c == null) befunde.Add($"einen Client {client} gibt es nicht (record {client} : IClient)");
        if (akteur.Length == 0) befunde.Add("kein Akteur genannt");
        else if (a == null) befunde.Add($"Akteur „{akteur}“ gibt es nicht");
        else if (c != null && !L(c, "verkoerpert").Contains(akteur)) befunde.Add($"{client} verkörpert {akteur} nicht (IVerkoerpert<{akteur}>)");
        var q = art.Length > 0 ? QuellartNamens(art) : null;
        if (art.Length == 0) befunde.Add("keine Art genannt");
        else if (q == null) befunde.Add($"„{art}“ ist keine Quellart (bekannt: {string.Join(", ", _quellarten.Select(x => S(x, "name")).DefaultIfEmpty("keine"))})");
        if (q != null && a != null)
            foreach (var meldung in Meldet(q))
            {
                var passend = Verwirklichungen.Where(u => Basis(S(u, "ein")) == meldung && L(a, "darf").Contains(Basis(S(u, "aus")))).ToList();
                if (passend.Count == 0) befunde.Add($"keine Übersetzung macht aus {meldung} eine Nachricht, die {akteur} darf (＋ Verwirklichung im Modal Akteure und Clients)");
                foreach (var u in passend)
                {
                    var typ = L(a, "darf").First(d => Basis(d) == Basis(S(u, "aus")));
                    if (!gibt.Contains((typ, S(u, "name")))) gibt.Add((typ, S(u, "name")));
                }
            }
        if (e[nameof(EingangKonfig.Rechner)] is JsonValue rv && rv.TryGetValue<string>(out var r)
            && (r.Length == 0 || r.Trim() != r || r.Any(ch => ch is ',' or '*' or '?' || char.IsWhiteSpace(ch))))
            befunde.Add($"Rechner „{r}“ ist kein Name — ein Rechner ist genau ein Name");
        if (q != null && B(q, "annahme"))
        {
            // Eine Annahme (docs/04 §4.7): jeder Host nimmt an — kein Rechner, dafür ein Pfad, je Umgebung eindeutig.
            if (e[nameof(EingangKonfig.Rechner)] != null) befunde.Add($"{art} nimmt an — jeder Host nimmt Aufrufe an, ein Rechner hat hier keine Bedeutung");
            var pfad = Annahmevertrag.Normal(WertText(e[Annahmevertrag.Pfad]));
            if (pfad.Length == 0) befunde.Add($"der Wert „{Annahmevertrag.Pfad}“ fehlt — unter ihm nimmt der Host an (POST {Annahmevertrag.Route}/<{Annahmevertrag.Pfad}>)");
            else if (pfade.TryGetValue(pfad, out var mit) && mit.Count > 1)
                befunde.Add($"Pfad „{pfad}“ nennen mehrere Eingänge ({string.Join(", ", mit)}) — ein Pfad gehört je Umgebung genau einem");
        }
        else if (cluster && e[nameof(EingangKonfig.Rechner)] == null) befunde.Add("im Cluster nennt ein Eingang seinen Rechner — eine Quelle lauscht genau einmal");
        if (q != null)
        {
            var felder = QuellartFelder(q).Select(x => x.Feld).ToHashSet(StringComparer.Ordinal);
            foreach (var (k, _) in e)
                if (!EingangKonfig.Rahmenfelder.Contains(k) && !felder.Contains(k))
                    befunde.Add($"„{k}“ ist kein Wert von {art} (Felder: {string.Join(", ", felder.Order(StringComparer.Ordinal).DefaultIfEmpty("keine"))})");
        }
        return (gibt, befunde);
    }

    /// <summary>
    /// <c>board.sicht.flaeche.eingaenge</c>: je Umgebung je Client-Karte seine Eingänge (Name, Art, Akteur, Rechner, Offen, die wichtigsten Werte,
    /// was er gibt, Befunde) und seine App-Verbindung, die Kanten Eingang → Akteur und Befunde der Umgebung (Clients, die es im Code nicht gibt).
    /// </summary>
    private JsonObject EingaengeJson()
    {
        var aus = new JsonObject();
        foreach (var (umgebung, inhalt) in UmgebungenRoh())
        {
            var cluster = S(inhalt[UmgebungsAbschnitte.Cluster], nameof(ClusterKonfig.Art)) is { Length: > 0 } ca && ca != nameof(ClusterArt.Einzelknoten);
            var karten = new JsonObject();
            var kanten = new JsonArray();
            var befunde = new JsonArray();
            var clients = inhalt[UmgebungsAbschnitte.Clients] as JsonObject ?? new JsonObject();
            // Die Pfade der Annahmen dieser Umgebung (je Pfad die Eingänge, die ihn nennen) — ein Pfad gehört genau einem.
            var pfade = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (client, cw) in clients)
                foreach (var (name, ew) in (cw?[nameof(ClientKonfig.Eingaenge)] as JsonObject ?? new JsonObject()))
                    if (ew is JsonObject e && QuellartNamens(S(e, nameof(EingangKonfig.Art))) is { } q && B(q, "annahme")
                        && Annahmevertrag.Normal(WertText(e[Annahmevertrag.Pfad])) is { Length: > 0 } p)
                        (pfade.TryGetValue(p, out var l) ? l : pfade[p] = []).Add(client + "/" + name);
            foreach (var (client, cw) in clients.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (cw is not JsonObject ck) continue;
                var c = ClientNamens(client);
                if (c == null)
                {
                    befunde.Add((JsonNode)$"Clients:{client}: einen Client {client} gibt es nicht (record {client} : IClient)");
                    continue;
                }
                var kid = "cl:" + S(c, "_id");
                var eingaenge = new JsonArray();
                foreach (var (name, ew) in (ck[nameof(ClientKonfig.Eingaenge)] as JsonObject ?? new JsonObject()).OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (ew is not JsonObject e) continue;
                    var (gibt, bef) = EingangPruefen(client, c, name, e, cluster, pfade);
                    var q = QuellartNamens(S(e, nameof(EingangKonfig.Art)));
                    // Die wichtigsten Werte: die gesetzten Felder der Konfig der Quellart in ihrer Reihenfolge, höchstens zwei — ein Schlüssel nur verdeckt.
                    var kurz = (q == null ? [] : QuellartFelder(q)).Where(f => e[f.Feld] != null).Take(2)
                        .Select(f => (JsonNode)(f.Feld + ": " + (f.Feld == Annahmevertrag.Schluessel ? "•••" : WertText(e[f.Feld])))).ToArray();
                    var akteur = S(e, nameof(EingangKonfig.Akteur));
                    var a = _akteure.FirstOrDefault(x => S(x, "name") == akteur);
                    var eo = new JsonObject
                    {
                        ["name"] = name, ["art"] = S(e, nameof(EingangKonfig.Art)), ["akteur"] = akteur,
                        ["kurz"] = new JsonArray(kurz),
                        ["gibt"] = new JsonArray(gibt.Select(g => (JsonNode)new JsonObject { ["typ"] = g.Typ, ["uebersetzung"] = g.Uebersetzung }).ToArray()),
                        ["befunde"] = Arr(bef),
                    };
                    if (e[nameof(EingangKonfig.Rechner)] is JsonValue rv && rv.TryGetValue<string>(out var r)) eo["rechner"] = r;
                    if (q != null && QuellartKarte(q) is { } qk) eo["quellart"] = qk;
                    if (q != null && B(q, "annahme")) eo["annahme"] = true;
                    eingaenge.Add(eo);
                    if (a != null)
                        foreach (var typ in gibt.Select(g => g.Typ).Distinct())
                            kanten.Add(new JsonObject { ["von"] = kid, ["nach"] = "akt:" + S(a, "_id") + "|aus", ["typ"] = typ, ["art"] = "eingang", ["eingang"] = name });
                }
                // Die App-Verbindung: was der Client an Leitung hat (Token, Adresse, URLs, ein Worker der Werkbank) — nur, wenn etwas davon steht.
                JsonObject? app = null;
                foreach (var f in new[] { nameof(ClientKonfig.Token), nameof(ClientKonfig.Adresse), nameof(ClientKonfig.Urls) })
                    if (ck[f] is { } w && WertText(w) is { Length: > 0 } t) (app ??= new JsonObject())[f.ToLowerInvariant()] = t;
                if (ck[nameof(ClientKonfig.Worker)] is JsonObject wk)
                    (app ??= new JsonObject())["worker"] = WertText(wk["Skript"]) is { Length: > 0 } sk ? sk : WertText(wk["Projekt"]);
                var ko = new JsonObject { ["eingaenge"] = eingaenge };
                if (app != null) ko["app"] = app;
                karten[kid] = ko;
            }
            aus[umgebung] = new JsonObject { ["cluster"] = cluster, ["karten"] = karten, ["kanten"] = kanten, ["befunde"] = befunde };
        }
        return aus;
    }

    /// <summary>
    /// Was „＋ Eingang“ an der Karte eines Clients anbietet: je Quellart die verkörperten Akteure, die sie verwirklicht (eine Übersetzung der
    /// Akteur-Seite von ihrer Meldung auf ein IDarf des Akteurs), und die verkörperten Akteure, die noch keine Quellart verwirklicht.
    /// </summary>
    private JsonObject ClientKandidaten(JsonObject c)
    {
        var verk = L(c, "verkoerpert").ToHashSet(StringComparer.Ordinal);
        var arten = new JsonArray();
        var verwirklicht = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in _quellarten.OrderBy(q => S(q, "name"), StringComparer.Ordinal))
        {
            var akteure = Verwirklicht(q).Select(x => S(x.Akteur, "name")).Where(verk.Contains).Distinct().Order(StringComparer.Ordinal).ToList();
            if (akteure.Count == 0) continue;
            verwirklicht.UnionWith(akteure);
            arten.Add(new JsonObject { ["art"] = S(q, "name"), ["framework"] = B(q, "framework"), ["annahme"] = B(q, "annahme"), ["akteure"] = Arr(akteure) });
        }
        return new JsonObject
        {
            ["arten"] = arten,
            ["ohne"] = Arr(verk.Where(a => !verwirklicht.Contains(a)).Order(StringComparer.Ordinal)),
        };
    }

    /// <summary>Die Befunde der Kanten aus Eingängen für die Paritätsprüfung: jede beginnt an einer Client-Karte und endet an einem Port des Akteurs.</summary>
    private IEnumerable<(string Schweregrad, string Meldung)> EingangsKantenBefunde(FlaechenBild f)
    {
        var karte = f.Karten.ToDictionary(k => k.Id, StringComparer.Ordinal);
        foreach (var (umgebung, u) in Eintraege(EingaengeJson()))
            foreach (var k in Alle(u?["kanten"]))
            {
                if (!karte.TryGetValue(S(k, "von"), out var von) || von.Art != "client")
                    yield return ("error", $"Fläche ({umgebung}): Eingang {S(k, "eingang")} an {S(k, "von")} ohne Client-Karte");
                if (!karte.TryGetValue(S(k, "nach"), out var nach) || !nach.Ports.Any(p => p.Richtung == "aus" && p.Typ == S(k, "typ")))
                    yield return ("error", $"Fläche ({umgebung}): Eingang {S(k, "eingang")} endet an {S(k, "nach")} ohne Port {S(k, "typ")}");
            }
    }
}
