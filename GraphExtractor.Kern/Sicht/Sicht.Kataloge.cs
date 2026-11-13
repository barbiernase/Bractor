using System.Text.Json.Nodes;
using DomainEditor;

namespace GraphExtractor;

// Die KATALOGE (board.sicht.kataloge, docs/10 §10.9): je Katalog — eine geschlossene, flache Einheit, alles im Namespace <Name>.Katalog — seine
//   Nachrichten mit wer sie ausgibt und wer sie nimmt, was die Quellen des Frameworks melden, seine Funktionen und Graphen, und je Funktion, welche
//   Nachrichten als Eingang bzw. je Auf als Ausgang passen. Was passt, sagt die Grammatik (Konsum Sorte × Funktion, Erzeugung Funktion × Sorte,
//   DomainEditor.Grammatik); dazu kein Kreis an einem Knoten (eine Funktion gibt nicht aus, was sie nimmt, und nimmt nicht, was sie ausgibt). Alles aus
//   dem Board (Code-Fakten); das Katalog-Modal des Editors zeigt nur an.
public sealed partial class Sicht
{
    /// <summary>Die Kataloge der Anwendung: die Projekt-Wurzeln (Verzeichnisse), die ein Katalog sind — auch ein leerer, eben angelegter.</summary>
    private IEnumerable<string> KatalogNamen() => Eintraege(_board["rahmen"]?["verzeichnisse"] as JsonObject).Select(e => e.Key)
        .Where(ns => Abstractions.Projektstruktur.KatalogVon(ns) == ns).OrderBy(x => x, StringComparer.Ordinal);

    /// <summary>Die Nachrichtensorte eines Typs: aus der Art seines Records; was eine Quelle meldet, ist ein Command (GR-QUELLE).</summary>
    private string? KatalogSorte(string typ, ISet<string> gemeldet) =>
        RecByName(typ) is { } r ? Grammatik.SorteVonRecordArt(S(r, "kind")) : gemeldet.Contains(typ) ? Grammatik.Command : null;

    /// <summary>
    /// Die Arten von Nachrichten, die ein Katalog hält: je Record-Art, deren Primitive im Katalog liegen darf (Projektstruktur, wie ein Ergebnis einer
    /// Funktion gelesen) und deren Sorte eine Funktion nehmen oder ausgeben kann (Grammatik) — heute Command ohne Ziel und Hinweis. Für die Gruppen
    /// und „＋ neu“ im Katalog-Modal.
    /// </summary>
    private static JsonArray KatalogArten() => new(RecordArt.Alle
        .Where(k => Strukturort.Primitive(new Record { Name = "_", Kind = k, Namespace = "" }, new HashSet<string> { "_" }, new HashSet<string>()) is { } p
                    && Abstractions.Projektstruktur.RollenVon(p).Contains(Abstractions.Projektstruktur.Rolle.Katalog))
        .Select(k => (Art: k, Sorte: Grammatik.SorteVonRecordArt(k)))
        .Where(x => x.Sorte is { } so && (Grammatik.KonsumVon(so, Grammatik.Funktion) != null || Grammatik.ErzeugungVon(Grammatik.Funktion, so) != null))
        .Select(x => (JsonNode)new JsonObject
        {
            ["art"] = x.Art, ["sorte"] = x.Sorte, ["name"] = Grammatik.Sorten.First(so => so.Id == x.Sorte).Name,
            ["eingang"] = Grammatik.KonsumVon(x.Sorte!, Grammatik.Funktion) != null,
        }).ToArray());

    private JsonObject KatalogeJson()
    {
        var funktionen = _bausteinFunktionen.Where(f => !IstGerufen(f)).ToList();
        var gemeldet = Alle(_board["katalog"]).SelectMany(k => L(k, "lauscht")).ToHashSet(StringComparer.Ordinal);
        List<string> Gibt(string nm) => funktionen.Where(f => Alle(f["eingaenge"]).Any(e => L(e, "ausgaben").Contains(nm)) || L(f, "lauscht").Contains(nm))
            .Select(f => S(f, "name")).Distinct().ToList();
        List<string> Nimmt(string nm) => funktionen.Where(f => Alle(f["eingaenge"]).Any(e => S(e, "eingang") == nm)).Select(f => S(f, "name")).Distinct().ToList();
        bool AlsEingang(string typ) => KatalogSorte(typ, gemeldet) is { } s && Grammatik.KonsumVon(s, Grammatik.Funktion) != null;
        bool AlsAusgang(string typ) => KatalogSorte(typ, gemeldet) is { } s && Grammatik.ErzeugungVon(Grammatik.Funktion, s) != null;

        var je = new JsonObject();
        foreach (var kat in KatalogNamen())
        {
            // Die Nachrichten des Katalogs: was eine Funktion nehmen oder ausgeben kann (laut Grammatik), im Namespace des Katalogs.
            var eigene = _records.Select(r => S(r, "name")).Where(n => S(RecByName(n), "namespace") == kat && (AlsEingang(n) || AlsAusgang(n)))
                .Order(StringComparer.Ordinal).ToList();
            var quellen = gemeldet.Where(AlsEingang).Order(StringComparer.Ordinal).ToList();
            JsonObject Nachricht(string n) => new()
            {
                ["typ"] = n, ["art"] = S(RecByName(n), "kind"), ["sorte"] = KatalogSorte(n, gemeldet), ["gibt"] = Arr(Gibt(n)), ["nimmt"] = Arr(Nimmt(n)),
            };
            var fns = new JsonArray();
            foreach (var f in funktionen.Where(f => S(f, "namespace") == kat).OrderBy(f => S(f, "name"), StringComparer.Ordinal))
            {
                var aufs = Alle(f["eingaenge"]).ToList();
                var nimmt = aufs.Select(e => S(e, "eingang")).ToHashSet(StringComparer.Ordinal);
                var gibtAus = aufs.SelectMany(e => L(e, "ausgaben")).ToHashSet(StringComparer.Ordinal);
                var ausgang = new JsonObject();
                foreach (var e in aufs)
                {
                    var schon = L(e, "ausgaben").ToHashSet(StringComparer.Ordinal);
                    ausgang[S(e, "eingang")] = Arr(eigene.Where(n => AlsAusgang(n) && !schon.Contains(n) && !nimmt.Contains(n)));
                }
                fns.Add(new JsonObject
                {
                    ["name"] = S(f, "name"), ["karte"] = "fb:" + S(f, "_id"),
                    // Was sie braucht (Konstruktor, je Typ ein Chip mit seiner Art) und in welchen Graphen sie Knoten ist.
                    ["konstruktor"] = new JsonArray(BausteinKonstruktor(f).GroupBy(p => S(p, "typ")).Select(x => (JsonNode)KonstruktorChip(f, x.First())).ToArray()),
                    ["verwendet"] = Arr(GraphenMitKnoten(S(f, "name")).OfType<string>()),
                    ["eingang"] = Arr(eigene.Concat(quellen).Where(n => AlsEingang(n) && !nimmt.Contains(n) && !gibtAus.Contains(n))),
                    ["ausgang"] = ausgang,
                });
            }
            je[kat] = new JsonObject
            {
                ["nachrichten"] = new JsonArray(eigene.Select(n => (JsonNode)Nachricht(n)).ToArray()),
                ["quellen"] = new JsonArray(quellen.Select(n => (JsonNode)Nachricht(n)).ToArray()),
                ["funktionen"] = fns,
                ["graphen"] = Arr(_graphen.Where(g => S(g, "namespace") == kat).Select(g => "gr:" + S(g, "_id"))),
            };
        }
        return new JsonObject { ["arten"] = KatalogArten(), ["je"] = je };
    }
}
