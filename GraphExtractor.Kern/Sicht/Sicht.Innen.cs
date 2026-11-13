using System.Text.Json.Nodes;
using DomainEditor;

namespace GraphExtractor;

// AGGREGAT UND STORE VON INNEN (board.sicht.innen, docs/10 §10.9): was die Modale früher selbst aus dem Modell rechneten — wo ein Typ verwendet wird
//   (für Löschpläne, „hat schon einen Erzeuger“, „unverbunden“), was an einen „＋“ angeschlossen werden kann (je Decide, Frage, Auf bzw. Frage eines
//   Readers: die Nachrichten bzw. Fähigkeiten, die die Grammatik dort erlaubt und die noch nicht dranhängen) und welche Read-Models eine Fähigkeit in
//   ihrer Signatur nennt. Alles aus dem Board (Code-Fakten) und DomainEditor.Grammatik; die Modale schlagen nur nach und legen an, was im Bild steht.
public sealed partial class Sicht
{
    private static IEnumerable<string> BezeichnerIn(string typ) => Bezeichner.Matches(typ ?? "").Select(m => m.Value);

    /// <summary>Die Sorte eines Records nach der Grammatik (null = keine Nachricht).</summary>
    private string? GrSorte(string name) => RecByName(name) is { } r ? Grammatik.SorteVonRecordArt(S(r, "kind")) : null;

    /// <summary>
    /// Wo ein Typ verwendet wird: je Stelle Art, Text, das Aggregat bzw. der Besitzer (Projektion/Reader) und die Karte der Stelle (Decide, Apply,
    /// Handhabung); <c>erzeugt</c> = die Stelle gibt ihn aus (Ergebnis eines Decide, Antwort einer Frage, Ausgabe einer Funktion bzw. Übersetzung,
    /// was eine Quelle meldet).
    /// </summary>
    private JsonObject VerwendungenJson()
    {
        var v = new Dictionary<string, JsonArray>(StringComparer.Ordinal);
        void P(string nm, string art, string wo, string? agg = null, string? own = null, string? karte = null, bool erzeugt = false)
        {
            if (nm.Length == 0) return;
            if (!v.TryGetValue(nm, out var l)) v[nm] = l = [];
            var o = new JsonObject { ["art"] = art, ["wo"] = wo };
            if (agg != null) o["agg"] = agg;
            if (own != null) o["own"] = own;
            if (karte != null) o["karte"] = karte;
            if (erzeugt) o["erzeugt"] = true;
            l.Add(o);
        }
        foreach (var d in _decider)
        {
            var (a, id, c) = (S(d, "aggregat"), "dec:" + S(d, "_id"), S(d, "command"));
            P(c, "decide", "Decide(" + c + ") in ▣ " + a, a, null, id);
            foreach (var o in Alle(d["ergibt"])) P(S(o, "event"), "ergebnis", "Ergebnis von Decide(" + c + ") in ▣ " + a, a, null, id, true);
        }
        foreach (var x in _applier) P(S(x, "event"), "apply", "Apply(" + S(x, "event") + ") in ▣ " + S(x, "aggregat"), S(x, "aggregat"), null, "app:" + S(x, "_id"));
        foreach (var a in _aggregate)
            foreach (var f in Alle(a["fragen"]))
            {
                P(S(f, "query"), "frage", "Frage(" + S(f, "query") + ") in ▣ " + S(a, "name"), S(a, "name"), null, "agg:" + S(a, "name"));
                foreach (var x in L(f, "antworten")) P(x, "antwort", "Antwort der Frage(" + S(f, "query") + ") in ▣ " + S(a, "name"), S(a, "name"), null, "agg:" + S(a, "name"), true);
            }
        foreach (var p in _projektionen)
        {
            var own = "prj:" + S(p, "_id");
            var ids = HandleIds(p, own);
            foreach (var (hd, i) in Alle(p["handles"]).Select((h, i) => (h, i)))
            {
                var ev = HandleEvents(hd);
                var text = (hd["buendel"] is JsonObject ? "Bündel " + string.Join(" ⋈ ", ev) : "Auf(" + S(hd, "event") + ")") + " in Projektion " + S(p, "name");
                foreach (var e in ev) P(e, "auf", text, null, own, ids[i]);
            }
        }
        foreach (var r in _reader)
        {
            var own = "rdr:" + S(r, "_id");
            var ids = HandleIds(r, own);
            foreach (var (hd, i) in Alle(r["handles"]).Select((h, i) => (h, i)))
            {
                P(S(hd, "query"), "rfrage", "Frage(" + S(hd, "query") + ") in Reader " + S(r, "name"), null, own, ids[i]);
                foreach (var x in L(hd, "responses")) P(x, "rantwort", "Antwort der Frage(" + S(hd, "query") + ") in Reader " + S(r, "name"), null, own, ids[i], true);
            }
        }
        foreach (var f in _bausteinFunktionen)
        {
            var fid = "fb:" + S(f, "_id");
            foreach (var e in Alle(f["eingaenge"]))
            {
                P(S(e, "eingang"), "funktion", "Auf(" + S(e, "eingang") + ") in Funktion " + S(f, "name"), null, null, fid);
                foreach (var x in L(e, "ausgaben")) P(Basis(x), "funktion", "Ausgabe von " + S(f, "name") + ".Auf(" + S(e, "eingang") + ")", null, null, fid, true);
            }
            foreach (var x in L(f, "lauscht")) P(x, "funktion", "Quelle " + S(f, "name"), null, null, fid, true);
        }
        foreach (var u in _uebersetzungen)
        {
            var uid = "ue:" + S(u, "_id");
            P(Basis(S(u, "ein")), "uebersetzung", "Übersetzung " + S(u, "name") + " nimmt es", null, null, uid);
            P(Basis(S(u, "aus")), "uebersetzung", "Übersetzung " + S(u, "name") + " erzeugt es", null, null, uid, true);
        }
        foreach (var a in _akteure)
        {
            foreach (var x in L(a, "darf")) P(x, "darf", "IDarf<" + x + "> am Akteur " + S(a, "name"), null, null, "akt:" + S(a, "_id"));
            foreach (var x in L(a, "hoert")) P(x, "hoert", "IHoert<" + x + "> am Akteur " + S(a, "name"), null, null, "akt:" + S(a, "_id"));
        }
        foreach (var r in _records)
            foreach (var t in Alle(r["felder"]).SelectMany(f => BezeichnerIn(S(f, "typ"))).Distinct().Where(t => t != S(r, "name")))
                P(t, "feld", "Feld in " + S(r, "name"));
        foreach (var rm in _readModels)
            foreach (var t in Alle(rm["felder"]).SelectMany(f => BezeichnerIn(S(f, "typ"))).Distinct())
                P(t, "feld", "Feld in Read-Model " + S(rm, "name"));
        var aus = new JsonObject();
        foreach (var (k, l) in v.Where(x => IstRec(x.Key) || _readModels.Any(rm => S(rm, "name") == x.Key))) aus[k] = l;
        return aus;
    }

    /// <summary>
    /// Was an einen „＋“ im Aggregat bzw. Store passt — die Nachrichten bzw. Fähigkeiten, die die Grammatik dort erlaubt und die noch nicht dranhängen:
    /// je Decide seine möglichen Ergebnisse (Event, Hinweis) und Fristen (auf einen Command seines Aggregats, die Uhr schickt ihn), je Frage eines
    /// Aggregats seine möglichen Antworten; je Auf bzw. Frage eines Readers die Fähigkeiten derselben Richtung und (Reader) die Antworten; je
    /// Fähigkeit die Read-Models ihrer Signatur. Die Gegenrichtung („wer nimmt dieses Event als Ergebnis“) ist dieselbe Liste, umgekehrt gelesen.
    /// </summary>
    private JsonObject InnenJson()
    {
        List<string> RecordsDer(Func<string, bool> sorte) =>
            _records.Select(r => S(r, "name")).Where(n => GrSorte(n) is { } s && sorte(s)).Order(StringComparer.Ordinal).ToList();
        var ergebnisse = RecordsDer(s => s != Grammatik.Response && s != Grammatik.Frist && Grammatik.ErzeugungVon(Grammatik.Aggregat, s) != null);
        var aggAntworten = RecordsDer(s => s == Grammatik.Response && Grammatik.ErzeugungVon(Grammatik.Aggregat, s) != null);
        var readerAntworten = RecordsDer(s => s == Grammatik.Response && Grammatik.ErzeugungVon(Grammatik.Reader, s) != null);
        var fristen = Grammatik.ErzeugungVon(Grammatik.Aggregat, Grammatik.Frist) != null && Grammatik.KonsumVon(Grammatik.Frist, Grammatik.Uhr) != null;

        var aggregate = new JsonObject();
        foreach (var a in _aggregate)
        {
            var name = S(a, "name");
            var decs = _decider.Where(d => S(d, "aggregat") == name).ToList();
            var ergebnis = new JsonObject();
            var frist = new JsonObject();
            foreach (var d in decs)
            {
                var schon = Alle(d["ergibt"]).Select(o => S(o, "event")).ToHashSet(StringComparer.Ordinal);
                ergebnis["dec:" + S(d, "_id")] = Arr(ergebnisse.Where(n => !schon.Contains(n)));
                if (fristen)
                    frist["dec:" + S(d, "_id")] = Arr(decs.Select(x => S(x, "command")).Where(c => c.Length > 0).Select(c => "Frist<" + c + ">").Where(f => !schon.Contains(f)));
            }
            var antwort = new JsonObject();
            foreach (var f in Alle(a["fragen"]))
            {
                var schon = L(f, "antworten").ToHashSet(StringComparer.Ordinal);
                antwort[S(f, "query")] = Arr(aggAntworten.Where(n => !schon.Contains(n)));
            }
            aggregate[name] = new JsonObject { ["ergebnis"] = ergebnis, ["fristen"] = frist, ["antwort"] = antwort };
        }

        // Je Auf bzw. Frage: die Fähigkeiten, die die Grammatik dort nimmt (Projektion: Schreib-, Reader: Lese-Fähigkeiten), noch nicht genommen.
        var handles = new JsonObject();
        foreach (var (art, sammlung, pre) in HandleArten)
        {
            var lesen = art == "reader";
            var baustein = lesen ? Grammatik.Reader : Grammatik.Projektion;
            var fns = Grammatik.KonsumVon(Grammatik.Faehigkeit, baustein) == null ? []
                : _stores.SelectMany(st => Alle(st[lesen ? "readFns" : "writeFns"])).Select(f => S(f, "_id")).ToList();
            foreach (var o in SammlungVon(sammlung))
            {
                var ids = HandleIds(o, pre + ":" + S(o, "_id"));
                foreach (var (hd, i) in Alle(o["handles"]).Select((h, i) => (h, i)))
                {
                    var hat = L(hd, "fns").ToHashSet(StringComparer.Ordinal);
                    var h = new JsonObject { ["fns"] = Arr(fns.Where(f => !hat.Contains(f))) };
                    if (lesen)
                    {
                        var schon = L(hd, "responses").ToHashSet(StringComparer.Ordinal);
                        h["antworten"] = Arr(readerAntworten.Where(n => !schon.Contains(n)));
                    }
                    handles[ids[i]] = h;
                }
            }
        }

        // Je Fähigkeit die Read-Models, die ihre Signatur nennt (Parameter und Rückgabe).
        var readModels = new JsonObject();
        foreach (var st in _stores)
            foreach (var fn in Alle(st["writeFns"]).Concat(Alle(st["readFns"])))
            {
                var typen = new[] { S(fn, "rueckgabe"), S(fn["sig"], "rueckgabe") }.Concat(Alle(fn["params"]).Select(x => S(x, "typ"))).SelectMany(BezeichnerIn).ToHashSet(StringComparer.Ordinal);
                readModels["fn:" + S(fn, "_id")] = Arr(_readModels.Where(rm => typen.Contains(S(rm, "name"))).Select(rm => "rm:" + S(rm, "_id")));
            }

        return new JsonObject
        {
            ["verwendungen"] = VerwendungenJson(), ["aggregate"] = aggregate, ["handles"] = handles, ["readModels"] = readModels,
        };
    }
}
