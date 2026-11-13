using System.Text.Json.Nodes;
using DomainEditor;

namespace GraphExtractor;

// AKTEURE UND CLIENTS VON INNEN (board.sicht.akteure, docs/10 §10.9): was das Modal „Akteure und Clients“ in Bändern zeigt.
//   Je Akteur: was er darf und hört, je Nachricht ihr Gegenüber auf der Fläche (wohin sie geht bzw. woher sie kommt — Aggregat, Graph, Übersetzung,
//   Reader), die Clients, die ihn verkörpern, und was an „＋ darf“ bzw. „＋ hört“ passt (Grammatik). Je Client: wen er verkörpert, wen er noch
//   verkörpern kann und je gehörtem Fakt seine Reaktion (die Methode der Implementierung, ihr Stand) samt dem Akteur, in dessen Band sie steht.
//   Je Akteur dazu seine VERWIRKLICHUNGEN (docs/04 §4.7): je Übersetzung der Akteur-Seite die Quellarten, deren Meldung sie in eine Nachricht seines IDarf
//   macht, wie weit ihre Felder zugeordnet sind und welche Eingänge der Umgebungen sie brauchen; was „＋ Verwirklichung“ anbietet (Quellarten mit ihren
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

        // Die Eingänge aller Umgebungen (Umgebung, Client, Eingang, Art, Akteur) — wer eine Verwirklichung braucht.
        var eingaenge = UmgebungenRoh().SelectMany(u => Eintraege(u.Inhalt[Abstractions.UmgebungsAbschnitte.Clients] as JsonObject)
            .SelectMany(c => Eintraege(c.Value?[nameof(Abstractions.ClientKonfig.Eingaenge)] as JsonObject)
                .Select(e => (Umgebung: u.Name, Client: c.Key, Eingang: e.Key, Art: S(e.Value, "Art"), Akteur: S(e.Value, "Akteur"))))).ToList();
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
                // Gebraucht von den Eingängen dieses Akteurs mit einer Quellart, die die Meldung meldet — wenn keine andere seiner Verwirklichungen sie nimmt.
                var andere = Verwirklichungen.Any(x => !ReferenceEquals(x, u) && Basis(S(x, "ein")) == meldung && darf.Contains(Basis(S(x, "aus"))));
                var gebraucht = andere ? [] : eingaenge.Where(e => e.Akteur == S(a, "name") && arten.Contains(e.Art))
                    .Select(e => e.Umgebung + ": " + e.Client + "/" + e.Eingang).ToList();
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
        return new JsonObject { ["akteure"] = akteure, ["clients"] = clients };
    }
}
