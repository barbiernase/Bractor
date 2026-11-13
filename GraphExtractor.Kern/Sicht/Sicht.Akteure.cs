using System.Text.Json.Nodes;
using DomainEditor;

namespace GraphExtractor;

// AKTEURE UND CLIENTS VON INNEN (board.sicht.akteure, docs/10 §10.9): was das Modal „Akteure und Clients“ in Bändern zeigt.
//   Je Akteur: was er darf und hört, je Nachricht ihr Gegenüber auf der Fläche (wohin sie geht bzw. woher sie kommt — Aggregat, Graph, Übersetzung,
//   Reader), die Clients, die ihn verkörpern, und was an „＋ darf“ bzw. „＋ hört“ passt (Grammatik). Je Client: wen er verkörpert, wen er noch
//   verkörpern kann und je gehörtem Fakt seine Reaktion (die Methode der Implementierung, ihr Stand) samt dem Akteur, in dessen Band sie steht.
//   Alles aus dem Board und der Fläche (Code-Fakten); das Modal ordnet nur an.
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

        var akteure = new JsonObject();
        foreach (var a in _akteure)
        {
            var id = "akt:" + S(a, "_id");
            var darf = L(a, "darf").Distinct().ToList();
            var hoert = L(a, "hoert").Distinct().ToList();
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
                ["kandidaten"] = new JsonObject
                {
                    ["darf"] = Arr(namen.Where(n => Erzeugt(n) && !darf.Contains(n))),
                    ["hoert"] = Arr(namen.Where(n => Hoerbar(n) && !hoert.Contains(n))),
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
