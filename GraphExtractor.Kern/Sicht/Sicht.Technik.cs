using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// TECHNIK (board.sicht.technik, docs/10 §10.9): je Technik — eine Schnittstelle ohne Marker, die ein Konstruktor nimmt — ihre Methoden mit Signatur, je
//   Umsetzung der Stand jeder Methode (geschrieben, leer, fehlt) und was ihr Konstruktor nimmt (Chips: Technik, Konfig, Zustand, Logger), dazu wer
//   die Technik nimmt. Daraus legt das Katalog-Modal die Stapel über dem Band einer Funktion: unten die Schnittstelle, darüber ihre Umsetzungen,
//   darüber, was eine Umsetzung selbst nimmt. Alles aus dem Board (Code-Fakten); der Editor zeigt nur an.
public sealed partial class Sicht
{
    private JsonObject TechnikJson()
    {
        var aus = new JsonObject();
        foreach (var g in _bausteinFunktionen.Where(IstGerufen))
        {
            var methoden = Alle(g["methoden"]).Select(m => S(m, "name")).ToList();
            var umsetzungen = new JsonArray();
            foreach (var u in Alle(g["umsetzungen"]))
            {
                var rumpfe = Alle(u["rumpfe"]).ToDictionary(r => S(r, "methode"), r => r, StringComparer.Ordinal);
                umsetzungen.Add(new JsonObject
                {
                    ["name"] = S(u, "name"),
                    ["methoden"] = new JsonArray(methoden.Select(m => (JsonNode)new JsonObject
                    {
                        ["name"] = m, ["stand"] = Stand(rumpfe.GetValueOrDefault(m) is { } r ? r["rumpf"]?.GetValue<string>() : null),
                    }).ToArray()),
                    ["braucht"] = new JsonArray(Alle(u["konstruktor"]).GroupBy(p => S(p, "typ")).Select(x => (JsonNode)KonstruktorChip(g, x.First())).ToArray()),
                });
            }
            aus[S(g, "name")] = new JsonObject
            {
                ["karte"] = "fb:" + S(g, "_id"),
                ["methoden"] = new JsonArray(Alle(g["methoden"]).Select(m => (JsonNode)new JsonObject
                {
                    ["name"] = S(m, "name"),
                    ["sig"] = S(m, "rueckgabe") + " " + S(m, "name") + "(" + string.Join(", ", Alle(m["parameter"]).Select(p => S(p, "typ") + " " + S(p, "name"))) + ")",
                }).ToArray()),
                ["umsetzungen"] = umsetzungen,
                ["genommenVon"] = Arr(GerufenNutzer(g).Select(f => S(f, "name")).Distinct()),
            };
        }
        return aus;
    }

    /// <summary>
    /// Der Stand eines Rumpfs: <c>fehlt</c> (keine Methode, oder sie wirft nur <c>NotImplementedException</c> — der Platzhalter, den eine Geste schreibt),
    /// <c>leer</c> (keine Anweisung) oder <c>geschrieben</c>.
    /// </summary>
    internal static string Stand(string? rumpf)
    {
        if (rumpf == null) return "fehlt";
        if (SyntaxFactory.ParseStatement("{" + rumpf + "\n}") is not BlockSyntax b) return "geschrieben";
        if (b.Statements.Count == 0) return "leer";
        bool Platzhalter(ExpressionSyntax? e) => e is ObjectCreationExpressionSyntax o && o.Type.ToString().EndsWith("NotImplementedException", StringComparison.Ordinal);
        return b.Statements.Count == 1 && (b.Statements[0] is ThrowStatementSyntax t && Platzhalter(t.Expression)
            || b.Statements[0] is ExpressionStatementSyntax { Expression: ThrowExpressionSyntax te } && Platzhalter(te.Expression)) ? "fehlt" : "geschrieben";
    }
}
