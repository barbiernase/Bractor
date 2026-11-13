using System.Text.Json.Nodes;
using DomainEditor;

namespace GraphExtractor;

// AKTEURE UND CLIENTS VON INNEN (board.sicht.akteure, docs/10 §10.9): was das Modal „Akteure und Clients“ in Bändern zeigt.
//   Je Akteur: was er darf und hört, je Nachricht ihr Gegenüber auf der Fläche (wohin sie geht bzw. woher sie kommt — Aggregat, Graph, Übersetzung,
//   Reader), die Clients, die ihn verkörpern, und was an „＋ darf“ bzw. „＋ hört“ passt (Grammatik). Je Client: wen er verkörpert, wen er noch
//   verkörpern kann und je gehörtem Fakt seine Reaktion (die Methode der Implementierung, ihr Stand) samt dem Akteur, in dessen Band sie steht.
//   Je Akteur dazu seine VERWIRKLICHUNGEN (docs/04 §4.7): je Übersetzung der Akteur-Seite die Quellarten, deren Meldung sie in eine Nachricht seines IDarf
//   macht, wie weit ihre Felder zugeordnet sind und welche Quellen von Apps sie nennen; was „＋ Verwirklichung“ anbietet (Quellarten mit ihren
//   Meldungen und deren Feldern, die Commands seines IDarf). Alles aus dem Board und der Fläche (Code-Fakten); das Modal ordnet nur an.
public sealed partial class Sicht
{
    private JsonObject AkteureJson()
    {
        var f = Flaeche;
        var karte = f.Karten.ToDictionary(k => k.Id, StringComparer.Ordinal);
        JsonObject Gegenueber(string flId) => karte.TryGetValue(flId, out var k)
            ? new JsonObject { ["id"] = flId, ["karte"] = k.Karte, ["art"] = k.Art, ["name"] = k.Name }
            : new JsonObject { ["id"] = flId, ["karte"] = "", ["art"] = "", ["name"] = flId };
        bool Erzeugt(string typ) => GrSorte(Basis(typ)) is { } s && Grammatik.ErzeugungVon(Grammatik.Akteur, s) != null;
        bool Hoerbar(string typ) => GrSorte(Basis(typ)) is { } s && Grammatik.KonsumVon(s, Grammatik.Akteur) != null;
        var namen = _records.Select(r => S(r, "name")).Order(StringComparer.Ordinal).ToList();

        // Die Quellen der Apps (App, Quelle, ihre Übersetzungen) — wer eine Verwirklichung braucht.
        var appQuellen = _apps.SelectMany(ap => Alle(ap["quellen"]).Select(q => (App: S(ap, "name"), Quelle: S(q, "name"), Uebersetzungen: L(q, "uebersetzungen").ToList())))
            .ToList();
        // Die Felder einer Meldung: ihr Record (eine Meldung der Anwendung), sonst die Quelle einer vorhandenen Übersetzung (eine des Frameworks).
        JsonArray MeldungsFelder(string m)
        {
            var felder = RecByName(m) is { } r ? Alle(r["felder"]).Select(f => (S(f, "name"), S(f, "typ")))
                : Verwirklichungen.Where(u => Basis(S(u, "ein")) == m).Select(u => Alle(u["quellen"]).Select(f => (S(f, "name"), S(f, "typ")))).FirstOrDefault() ?? [];
            return new JsonArray(felder.Select(f => (JsonNode)new JsonObject { ["name"] = f.Item1, ["typ"] = f.Item2 }).ToArray());
        }
        var quellarten = new JsonArray(_quellarten.OrderBy(q => S(q, "name"), StringComparer.Ordinal).Select(q => (JsonNode)new JsonObject
        {
            ["name"] = S(q, "name"), ["framework"] = B(q, "framework"),
            ["meldet"] = new JsonArray(Meldet(q).Select(m => (JsonNode)new JsonObject { ["typ"] = m, ["felder"] = MeldungsFelder(m) }).ToArray()),
        }).ToArray());

        var akteure = new JsonObject();
        foreach (var a in _akteure)
        {
            var id = "akt:" + S(a, "_id");
            var darf = L(a, "darf").Distinct().ToList();
            var hoert = L(a, "hoert").Distinct().ToList();
            // Seine Verwirklichungen: je Übersetzung der Akteur-Seite auf eine Nachricht, die er darf. Offen = Ziele ohne Regel (dann baut sie nicht).
            var verwirklichungen = new JsonArray(Verwirklichungen.Where(u => darf.Contains(Basis(S(u, "aus")))).Select(u =>
            {
                var meldung = Basis(S(u, "ein"));
                var arten = MeldendeQuellarten(meldung).Select(q => S(q, "name")).ToList();
                // Gebraucht von den Quellen der Apps, die sie nennen ([Uebersetzt<U>]).
                var gebraucht = appQuellen.Where(q => q.Uebersetzungen.Contains(S(u, "name"))).Select(q => q.App + "." + q.Quelle).ToList();
                return (JsonNode)new JsonObject
                {
                    ["uebersetzung"] = S(u, "name"), ["karte"] = "ue:" + S(u, "_id"), ["meldung"] = meldung, ["nachricht"] = Basis(S(u, "aus")),
                    ["quellarten"] = Arr(arten),
                    ["offen"] = Alle(u["ziele"]).Count(z => S(z, "regel") is not ("feld" or "name" or "standard")),
                    ["fehler"] = L(u, "fehler").Count(x => x.Length > 0),
                    ["gebraucht"] = Arr(gebraucht),
                };
            }).ToArray());
            akteure[id] = new JsonObject
            {
                ["name"] = S(a, "name"),
                ["darf"] = new JsonArray(darf.Select(t => (JsonNode)new JsonObject
                {
                    ["typ"] = t, ["sorte"] = Sorte(t),
                    ["ziele"] = new JsonArray(f.Kanten.Where(k => k.Von == id + "|aus" && k.Typ == t).Select(k => k.Nach).Distinct().Select(n => (JsonNode)Gegenueber(n)).ToArray()),
                }).ToArray()),
                ["hoert"] = new JsonArray(hoert.Select(t => (JsonNode)new JsonObject
                {
                    ["typ"] = t, ["sorte"] = Sorte(t),
                    ["herkunft"] = new JsonArray(f.Kanten.Where(k => k.Nach == id + "|ein" && k.Typ == t).Select(k => k.Von).Distinct().Select(n => (JsonNode)Gegenueber(n)).ToArray()),
                }).ToArray()),
                ["clients"] = Arr(_clients.Where(c => L(c, "verkoerpert").Contains(S(a, "name"))).Select(c => "cl:" + S(c, "_id"))),
                ["verwirklichungen"] = verwirklichungen,
                ["kandidaten"] = new JsonObject
                {
                    ["darf"] = Arr(namen.Where(n => Erzeugt(n) && !darf.Contains(n))),
                    ["hoert"] = Arr(namen.Where(n => Hoerbar(n) && !hoert.Contains(n))),
                    // „＋ Verwirklichung“: erst eine Quellart (alle, mit ihren Meldungen), dann ein Command seines IDarf.
                    ["verwirklichung"] = new JsonObject
                    {
                        ["quellarten"] = quellarten.DeepClone(),
                        ["nachrichten"] = Arr(darf.Where(t => Sorte(t) == "command")),
                    },
                },
            };
        }

        var clients = new JsonObject();
        foreach (var c in _clients)
        {
            var verk = L(c, "verkoerpert").ToList();
            // Je gehörtem Fakt die Reaktion: die Methode der Implementierung; sie steht im Band des ersten verkörperten Akteurs, der das Fakt hört.
            var reaktionen = new JsonArray();
            foreach (var (impl, ii) in Alle(c["implementierungen"]).Select((x, i) => (x, i)))
                foreach (var (r, ri) in Alle(impl["rumpfe"]).Select((x, i) => (x, i)))
                {
                    var ev = S(r, "event");
                    var bei = verk.FirstOrDefault(an => _akteure.Any(a => S(a, "name") == an && L(a, "hoert").Contains(ev)));
                    reaktionen.Add(new JsonObject
                    {
                        ["event"] = ev, ["methode"] = S(r, "methode"), ["klasse"] = S(impl, "klasse"), ["impl"] = ii, ["teil"] = ri,
                        ["akteur"] = bei ?? "", ["stand"] = r["rumpf"] is null ? "fehlt" : Stand(S(r, "rumpf")),
                        ["ausgaenge"] = Arr(L(r, "ausgaenge")),
                    });
                }
            clients["cl:" + S(c, "_id")] = new JsonObject
            {
                ["name"] = ClientAnzeige(c),
                ["verkoerpert"] = Arr(verk),
                ["kandidaten"] = Arr(_akteure.Select(a => S(a, "name")).Where(n => !verk.Contains(n)).Order(StringComparer.Ordinal)),
                ["implementierungen"] = new JsonArray(Alle(c["implementierungen"]).Select(i => (JsonNode)new JsonObject
                    { ["klasse"] = S(i, "klasse"), ["datei"] = S(i, "datei"), ["sprache"] = S(i, "sprache") }).ToArray()),
                ["reaktionen"] = reaktionen,
            };
        }
        // Apps (docs/10 E18): je App ihr Client und je Nachricht des Clients, wer sie umsetzt — die App oder ihre Quellen (Quellart,
        //   Übersetzung der Akteur-Seite); was „＋ Quelle“ an einer Nachricht anbietet: je Übersetzung der Akteur-Seite auf diese Nachricht die
        //   Quellarten, die ihre Meldung melden.
        var apps = new JsonObject();
        foreach (var ap in _apps)
        {
            var cl = _clients.FirstOrDefault(c => S(c, "name") == S(ap, "client"));
            var verk = cl == null ? [] : L(cl, "verkoerpert").ToList();
            var sendet = _akteure.Where(a => verk.Contains(S(a, "name"))).SelectMany(a => L(a, "darf")).Where(t => Sorte(t) == "command").Distinct().Order(StringComparer.Ordinal).ToList();
            var quellen = Alle(ap["quellen"]).Select(q => new
            {
                Q = q, Ziele = L(q, "uebersetzungen").Select(u => (U: u, Ziel: Verwirklichungen.FirstOrDefault(v => S(v, "name") == u))).ToList(),
            }).ToList();
            apps["ap:" + S(ap, "_id")] = new JsonObject
            {
                ["name"] = S(ap, "name"), ["client"] = S(ap, "client"), ["clientKarte"] = cl == null ? "" : "cl:" + S(cl, "_id"),
                ["quellen"] = new JsonArray(quellen.Select(q => (JsonNode)new JsonObject
                {
                    ["name"] = S(q.Q, "name"), ["quellart"] = S(q.Q, "quellart"),
                    ["uebersetzungen"] = new JsonArray(q.Ziele.Select(z => (JsonNode)new JsonObject
                    {
                        ["name"] = z.U, ["nachricht"] = z.Ziel == null ? "" : Basis(S(z.Ziel, "aus")), ["meldung"] = z.Ziel == null ? "" : Basis(S(z.Ziel, "ein")),
                        ["karte"] = z.Ziel == null ? "" : "ue:" + S(z.Ziel, "_id"),
                    }).ToArray()),
                }).ToArray()),
                // Je Nachricht, die der Client sendet: App oder Quellen, und was „＋ Quelle“ dort anbietet.
                ["nachrichten"] = new JsonArray(sendet.Select(t => (JsonNode)new JsonObject
                {
                    ["typ"] = t,
                    ["quellen"] = Arr(quellen.Where(q => q.Ziele.Any(z => z.Ziel != null && Basis(S(z.Ziel, "aus")) == t)).Select(q => S(q.Q, "name"))),
                    ["angebot"] = new JsonArray(Verwirklichungen.Where(v => Basis(S(v, "aus")) == t).SelectMany(v =>
                        MeldendeQuellarten(Basis(S(v, "ein"))).Select(q => (JsonNode)new JsonObject
                        {
                            ["uebersetzung"] = S(v, "name"), ["meldung"] = Basis(S(v, "ein")), ["quellart"] = S(q, "name"),
                        })).ToArray()),
                }).ToArray()),
            };
        }
        foreach (var c in _clients)
            if (clients["cl:" + S(c, "_id")] is JsonObject co)
                co["apps"] = Arr(_apps.Where(a => S(a, "client") == S(c, "name")).Select(a => "ap:" + S(a, "_id")));
        return new JsonObject { ["akteure"] = akteure, ["clients"] = clients, ["apps"] = apps };
    }
}
