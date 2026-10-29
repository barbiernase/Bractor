using System.Text.Json.Nodes;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GraphExtractor;

/// <summary>
/// Was eine Geste an den Karten ändert (Werkbank P7.2, CLAUDE.md W4): je Sammlung die geänderten bzw. neuen Karten (ganz) und die
/// Schlüssel der entfernten. Schlüssel sind natürlich (<see cref="KartenSchluessel"/>) — dieselbe Regel im Editor und hier.
/// </summary>
public sealed record KartenDelta
{
    public Dictionary<string, List<JsonObject>> Karten { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> Entfernt { get; init; } = new(StringComparer.Ordinal);

    public bool Leer => Karten.Values.All(l => l.Count == 0) && Entfernt.Values.All(l => l.Count == 0);

    public static KartenDelta AusJson(JsonObject o)
    {
        var d = new KartenDelta();
        if (o["karten"] is JsonObject karten)
            foreach (var (sammlung, liste) in karten)
                d.Karten[sammlung] = (liste as JsonArray ?? []).OfType<JsonObject>().Select(k => (JsonObject)k.DeepClone()).ToList();
        if (o["entfernt"] is JsonObject entfernt)
            foreach (var (sammlung, liste) in entfernt)
                d.Entfernt[sammlung] = (liste as JsonArray ?? []).Select(x => (string?)x ?? "").Where(x => x.Length > 0).ToList();
        return d;
    }
}

/// <summary>Der natürliche Schlüssel einer Karte je Sammlung — der Editor rechnet ihn genauso (<c>GKEY</c> in editor.js).</summary>
public static class KartenSchluessel
{
    /// <summary>Die Sammlungen, die aus dem Code kommen und von Gesten geändert werden.</summary>
    public static readonly string[] Sammlungen =
    [
        "records", "enums", "aggregate", "decider", "applier", "dienste", "akteure", "clients",
        "readModels", "stores", "projektionen", "reader",
    ];

    public static string Von(string sammlung, JsonObject k)
    {
        static string S(JsonObject o, string f) => (string?)o[f] ?? "";
        return sammlung switch
        {
            "decider" => S(k, "aggregat") + "|" + S(k, "command"),
            "applier" => S(k, "aggregat") + "|" + S(k, "event"),
            "readModels" or "stores" or "projektionen" or "reader" => S(k, "name"),
            _ => S(k, "namespace") + "|" + S(k, "name"),
        };
    }
}

/// <summary>Das Ergebnis einer Geste: der Patch (Datei für Datei) und der Bericht des Schreibers (was nicht ging, mit Grund).</summary>
public sealed record GestenBericht(IReadOnlyList<DateiPatch> Patch, SchreibBericht Schreiber, IReadOnlyList<string> Geloescht, IReadOnlyList<string> NichtGeloescht);

/// <summary>
/// Geste → Patch (Werkbank P7.2): das gelesene Board (Code-Stand) plus die Karten-Änderung der Geste ergeben das Soll; daraus schreibt der
/// bestehende Schreiber (<see cref="DateiSchreiber"/>: Scaffolder für Neues, Abgleich für Geändertes) — Unverändertes bleibt Wort für Wort.
/// Was der Code besitzt, entscheidet der Herkunfts-Stempel: er kommt hier aus dem CODE-STAND (dem Board vor der Geste), nie aus dem Editor.
/// Entfernte Karten werden an ihrem Ort gelöscht (<c>_ort</c> + <c>_teile</c>), entfernte Handles an ihrer Methode.
/// </summary>
public static class Gesten
{
    /// <summary>Das Board nach der Geste: Karten nach Schlüssel ersetzt bzw. angehängt, entfernte weg; alles andere bleibt.</summary>
    public static JsonObject Anwenden(JsonObject board, KartenDelta delta)
    {
        var neu = (JsonObject)board.DeepClone();
        foreach (var sammlung in delta.Karten.Keys.Concat(delta.Entfernt.Keys).Distinct())
        {
            if (!KartenSchluessel.Sammlungen.Contains(sammlung)) continue;
            var liste = neu[sammlung] as JsonArray ?? new JsonArray();
            neu[sammlung] = liste;
            var weg = new HashSet<string>(delta.Entfernt.GetValueOrDefault(sammlung) ?? [], StringComparer.Ordinal);
            foreach (var k in liste.OfType<JsonObject>().Where(k => weg.Contains(KartenSchluessel.Von(sammlung, k))).ToList()) liste.Remove(k);
            foreach (var karte in delta.Karten.GetValueOrDefault(sammlung) ?? [])
            {
                var schluessel = KartenSchluessel.Von(sammlung, karte);
                var alt = liste.OfType<JsonObject>().FirstOrDefault(k => KartenSchluessel.Von(sammlung, k) == schluessel);
                var kopie = (JsonObject)karte.DeepClone();
                if (alt == null) liste.Add(kopie);
                else liste[liste.IndexOf(alt)] = kopie;
            }
        }
        return neu;
    }

    /// <summary>
    /// Die Geste in den Puffer schreiben: (1) Entferntes an seinem Ort löschen, (2) das Soll-Modell mit den Stempeln des Code-Stands durch den
    /// Schreiber. Gespeichert wird nicht — das tut der Aufrufer (und merkt sich den Patch für ↶).
    /// </summary>
    public static GestenBericht Schreibe(JsonObject board, KartenDelta delta, Dateipuffer ws)
    {
        var soll = Anwenden(board, delta);
        var vorher = BoardLeseseite.AusBoard(board.ToJsonString());
        var modell = Stempeln(BoardLeseseite.AusBoard(soll.ToJsonString()), vorher);

        var (geloescht, nicht) = Loeschen(board, soll, delta, ws);
        var bericht = DateiSchreiber.Schreibe(modell, ws);
        return new GestenBericht(ws.Patch(), bericht, geloescht, nicht);
    }

    // ══ Stempel aus dem Code-Stand ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Jedes Element des Solls bekommt den Herkunfts-Stempel seines Gegenstücks im Code-Stand (natürlicher Schlüssel) — oder keinen (neu).
    /// So schreibt der Schreiber genau das, was die Geste geändert hat, egal was der Editor mitschickt.
    /// </summary>
    public static EditorModell Stempeln(EditorModell soll, EditorModell code)
    {
        static Dictionary<string, T> Nach<T>(IEnumerable<T> xs, Func<T, string> k) =>
            xs.GroupBy(k, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var rec = Nach(code.Records, r => r.Namespace + "|" + r.Name);
        var en = Nach(code.Enums, e => e.Namespace + "|" + e.Name);
        var agg = Nach(code.Aggregate, a => a.Namespace + "|" + a.Name);
        var dec = Nach(code.Decider, d => d.Aggregat + "|" + d.Command);
        var dst = Nach(code.Dienste, d => d.Namespace + "|" + d.Name);
        var akt = Nach(code.Akteure, a => a.Namespace + "|" + a.Name);
        var cli = Nach(code.Clients, c => c.Namespace + "|" + c.Name);
        var lesen = code.Lesen ?? new Leseseite();
        var sto = Nach(lesen.Stores, s => s.Name);
        var kon = Nach(lesen.Konsumenten, k => k.Name);
        var les = Nach(lesen.Reader, r => r.Name);
        IReadOnlyList<Handle> Handles(IReadOnlyList<Handle> neu, IReadOnlyList<Handle>? alt)
        {
            var a = Nach(alt ?? [], h => h.Eingang);
            return neu.Select(h => h with { Herkunft = a.GetValueOrDefault(h.Eingang)?.Herkunft }).ToList();
        }

        var l = soll.Lesen;
        return soll with
        {
            Records = soll.Records.Select(r => r with { Herkunft = rec.GetValueOrDefault(r.Namespace + "|" + r.Name)?.Herkunft }).ToList(),
            Enums = soll.Enums.Select(e => e with { Herkunft = en.GetValueOrDefault(e.Namespace + "|" + e.Name)?.Herkunft }).ToList(),
            Aggregate = soll.Aggregate.Select(a => a with { Herkunft = agg.GetValueOrDefault(a.Namespace + "|" + a.Name)?.Herkunft }).ToList(),
            Decider = soll.Decider.Select(d => d with { Herkunft = dec.GetValueOrDefault(d.Aggregat + "|" + d.Command)?.Herkunft }).ToList(),
            Dienste = soll.Dienste.Select(d => d with { Herkunft = dst.GetValueOrDefault(d.Namespace + "|" + d.Name)?.Herkunft }).ToList(),
            Akteure = soll.Akteure.Select(a => a with { Herkunft = akt.GetValueOrDefault(a.Namespace + "|" + a.Name)?.Herkunft }).ToList(),
            Clients = soll.Clients.Select(c => c with { Herkunft = cli.GetValueOrDefault(c.Namespace + "|" + c.Name)?.Herkunft }).ToList(),
            Lesen = l == null ? null : l with
            {
                Stores = l.Stores.Select(s =>
                {
                    var alt = Nach(sto.GetValueOrDefault(s.Name)?.Fns ?? [], f => f.Methode);
                    return s with { Fns = s.Fns.Select(f => f with { Herkunft = alt.GetValueOrDefault(f.Methode)?.Herkunft }).ToList() };
                }).ToList(),
                Konsumenten = l.Konsumenten.Select(k => k with
                {
                    Herkunft = kon.GetValueOrDefault(k.Name)?.Herkunft, Handles = Handles(k.Handles, kon.GetValueOrDefault(k.Name)?.Handles),
                }).ToList(),
                Reader = l.Reader.Select(r => r with
                {
                    Herkunft = les.GetValueOrDefault(r.Name)?.Herkunft, Handles = Handles(r.Handles, les.GetValueOrDefault(r.Name)?.Handles),
                }).ToList(),
            },
        };
    }

    // ══ Löschen am Ort ═══════════════════════════════════════════════════════════════════════════

    private sealed record Stelle(string Datei, int Start, int Ende, string Was);

    /// <summary>
    /// Entfernte Karten und entfernte Handles (Projektion/Reader) an ihrem Ort löschen — samt Doku-Kommentar. Bleibt in einer Datei
    /// kein Typ übrig, entfällt die Datei. Verweise anderswo bleiben stehen: sie sind Compiler-Befunde an ihrer Karte.
    /// </summary>
    private static (List<string> Geloescht, List<string> Nicht) Loeschen(JsonObject board, JsonObject soll, KartenDelta delta, Dateipuffer ws)
    {
        var stellen = new List<Stelle>();
        var nicht = new List<string>();
        static IEnumerable<JsonObject> Alle(JsonNode? n) => (n as JsonArray ?? []).OfType<JsonObject>();
        Stelle? Aus(JsonNode? ort, string was) => ort is JsonObject o && (string?)o["datei"] is { } d && (int?)o["start"] is { } s && (int?)o["ende"] is { } e
            ? new Stelle(d, s, e, was) : null;

        foreach (var (sammlung, schluessel) in delta.Entfernt)
            foreach (var k in Alle(board[sammlung]).Where(k => schluessel.Contains(KartenSchluessel.Von(sammlung, k))))
            {
                var was = $"{sammlung} {KartenSchluessel.Von(sammlung, k)}";
                var orte = new[] { Aus(k["_ort"], was) }.Concat(Alle(k["_teile"]).Select(t => Aus(t, was))).Where(x => x != null).ToList();
                if (orte.Count == 0) { nicht.Add($"{was}: kein Ort im Code bekannt — nicht gelöscht"); continue; }
                stellen.AddRange(orte!);
            }

        // Eingänge (Auf/Frage), die an einer weiter bestehenden Klasse wegfallen: ihre Methode.
        foreach (var (sammlung, feld, methode) in new[] { ("projektionen", "event", Abstractions.Projektionsvertrag.Auf), ("reader", "query", Abstractions.Projektionsvertrag.Frage) })
            foreach (var neu in Alle(soll[sammlung]))
            {
                var alt = Alle(board[sammlung]).FirstOrDefault(k => KartenSchluessel.Von(sammlung, k) == KartenSchluessel.Von(sammlung, neu));
                if (alt == null) continue;
                var bleibt = Alle(neu["handles"]).Select(h => (string?)h[feld]).ToHashSet(StringComparer.Ordinal);
                foreach (var h in Alle(alt["handles"]).Where(h => !bleibt.Contains((string?)h[feld])))
                    if (Aus(h["_ort"], $"handle {(string?)alt["name"]}.{methode}({(string?)h[feld]})") is { } st) stellen.Add(st);
            }

        var geloescht = new List<string>();
        foreach (var datei in stellen.GroupBy(s => s.Datei))
        {
            if (ws.Lies(datei.Key) is not { } text) { nicht.AddRange(datei.Select(s => $"{s.Was}: Datei {datei.Key} fehlt")); continue; }
            var baum = CSharpSyntaxTree.ParseText(text);
            var wurzel = baum.GetRoot();
            var knoten = new List<SyntaxNode>();
            foreach (var s in datei.DistinctBy(s => (s.Start, s.Ende)))
            {
                var n = wurzel.DescendantNodes(new TextSpan(s.Start, s.Ende - s.Start))
                    .FirstOrDefault(x => x.Span.Start == s.Start && x.Span.End == s.Ende && x is MemberDeclarationSyntax or StatementSyntax);
                if (n == null) { nicht.Add($"{s.Was}: Ort {datei.Key}:{s.Start} passt nicht mehr zum Code — nicht gelöscht"); continue; }
                if (knoten.Any(k => k.Span.Contains(n.Span))) continue;   // schon mit dem Umschließenden weg
                knoten.RemoveAll(k => n.Span.Contains(k.Span));
                knoten.Add(n);
                geloescht.Add(s.Was);
            }
            var neuText = text;
            foreach (var n in knoten.OrderByDescending(k => k.SpanStart))
            {
                var (von, bis) = Bereich(n, neuText);
                neuText = neuText[..von] + neuText[bis..];
            }
            // Keine Typ-Deklaration mehr übrig → die Datei entfällt (nur usings/Namespace wären Rest).
            if (!CSharpSyntaxTree.ParseText(neuText).GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Any()
                && !CSharpSyntaxTree.ParseText(neuText).GetRoot().DescendantNodes().OfType<DelegateDeclarationSyntax>().Any())
                ws.Entferne(datei.Key);
            else ws.Setze(datei.Key, neuText);
        }
        return (geloescht.Distinct().ToList(), nicht);
    }

    /// <summary>
    /// Der zu löschende Textbereich eines Knotens: ab der Zeile seines ersten Doku-/Kommentar-Trivias (ohne Leerzeilen davor) bis zum Ende
    /// seiner Zeile; steht danach eine Leerzeile und davor auch, geht eine mit (kein doppelter Abstand).
    /// </summary>
    private static (int Von, int Bis) Bereich(SyntaxNode n, string text)
    {
        var vor = n.GetLeadingTrivia();
        var beginn = n.SpanStart;
        // Rückwärts über die Trivia: Kommentare/Doku hängen am Knoten, eine Leerzeile trennt ihn vom Vorgänger.
        for (var i = vor.Count - 1; i >= 0; i--)
        {
            var t = vor[i];
            if (t.IsKind(SyntaxKind.WhitespaceTrivia)) continue;
            if (t.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                // Zwei Zeilenenden hintereinander (nur Leerraum dazwischen) = Leerzeile → hier endet der Vorspann.
                var j = i - 1;
                while (j >= 0 && vor[j].IsKind(SyntaxKind.WhitespaceTrivia)) j--;
                if (j >= 0 && vor[j].IsKind(SyntaxKind.EndOfLineTrivia)) break;
                continue;
            }
            beginn = t.SpanStart;
        }
        var von = beginn;
        while (von > 0 && text[von - 1] is ' ' or '\t') von--;
        var bis = n.Span.End;
        while (bis < text.Length && text[bis] is ' ' or '\t') bis++;
        if (bis < text.Length && text[bis] == '\r') bis++;
        if (bis < text.Length && text[bis] == '\n') bis++;
        // Leerzeile davor und danach → eine davon gehört zum Gelöschten.
        bool LeerzeileAb(int p) { var q = p; while (q < text.Length && text[q] is ' ' or '\t') q++; return q < text.Length && text[q] is '\n' or '\r'; }
        bool LeerzeileVor(int p) { if (p < 1 || text[p - 1] != '\n') return false; var q = p - 2; if (q >= 0 && text[q] == '\r') q--; while (q >= 0 && text[q] is ' ' or '\t') q--; return q < 0 || text[q] == '\n'; }
        if (LeerzeileVor(von) && LeerzeileAb(bis))
        {
            while (bis < text.Length && text[bis] is ' ' or '\t') bis++;
            if (bis < text.Length && text[bis] == '\r') bis++;
            if (bis < text.Length && text[bis] == '\n') bis++;
        }
        return (von, bis);
    }
}
