using System.Text;
using Infrastructure.SourceGeneration;
using Microsoft.CodeAnalysis;

namespace GraphExtractor;

/// <summary>
/// Ein Graph als Text (<c>dotnet run --project GraphExtractor -- --graph &lt;Typ&gt;</c>, docs/04 §4.4): der Record mit [Kopf] und [Von(…)], Knoten (aufgefaltet, mit Funktion), Köpfe,
/// Kanten mit ihrer Vielfachheit (Erzeuger: 1, 0..1 einer Wahl, 0..n eines Stroms; verlierbar oder gesichert), was den Graphen verlässt,
/// die Signatur und die Befunde der Graph-Lesart — dieselbe Lesart (<see cref="Graphform"/>) wie Generator und Analyzer. Schreibt nichts.
/// </summary>
public static class GraphText
{
    /// <summary>Sucht den Graphen unter den Domänen-Compilations (einfacher oder voller Name) und beschreibt ihn; null = kein Graph dieses Namens.</summary>
    public static string? Text(IEnumerable<Compilation> compilations, string name)
    {
        foreach (var c in compilations)
            foreach (var t in Alle(c.Assembly.GlobalNamespace))
                if ((t.Name == name || t.ToDisplayString() == name) && Graphform.IstGraph(t)) return Text(t);
        return null;
    }

    private static IEnumerable<INamedTypeSymbol> Alle(INamespaceSymbol ns) =>
        ns.GetTypeMembers().Concat(ns.GetNamespaceMembers().SelectMany(Alle));

    private static string N(ITypeSymbol t) => Funktionsform.Anzeigename(t);

    public static string Text(INamedTypeSymbol t)
    {
        var g = Graphform.Lies(t);
        var b = new StringBuilder();
        b.AppendLine($"Graph {t.Name} ({t.ContainingNamespace.ToDisplayString()})");
        // Der Record, wie er dasteht: je Parameter [Kopf] und [Von(…)] — daraus folgen Köpfe und Kanten.
        b.AppendLine("Record:");
        foreach (var x in g.Glieder)
            b.AppendLine($"  {(x.Kopf ? "[Kopf] " : "")}{(x.Von.Count > 0 ? "[Von(" + string.Join(", ", x.Von) + ")] " : "")}{N(x.Typ)} {x.Name}");
        b.AppendLine("Knoten:");
        var breite = g.Knoten.Select(k => k.Name.Length).DefaultIfEmpty(0).Max();
        foreach (var k in g.Knoten)
            b.AppendLine($"  {k.Name.PadRight(breite)}  {N(k.Typ)}{(k == g.Quelle ? "  (Quelle: lauscht, je Element ein Lauf)" : "")}");
        b.AppendLine("Köpfe:");
        if (g.Quelle != null) b.AppendLine($"  {g.Quelle.Name} (Quelle)");
        foreach (var (typ, knoten) in g.Koepfe) b.AppendLine($"  {N(typ)} → {string.Join(", ", knoten.Select(k => k.Name))}");
        b.AppendLine("Kanten:");
        foreach (var k in g.Kanten)
        {
            var aus = k.Strom ? "0..n" : k.VonOperation.Wahlen.Contains(Funktionsform.Voll(k.Typ)) ? "0..1" : "1";
            b.AppendLine($"  {k.Von.Name} ─{N(k.Typ)} [{aus}]─▶ {k.Nach.Name}.{k.Port.Name}{(k.Verlierbar ? "  (verlierbar)" : "")}");
        }
        b.AppendLine("Verlässt den Graphen:");
        foreach (var (von, typ) in g.Ausgaenge) b.AppendLine($"  {von.Name} ─{N(typ)}─▶");
        // Die Signatur: was der Graph nimmt (seine Köpfe; eine Quelle nimmt nichts, sie meldet) → alles, was ihn verlässt.
        var ein = g.Quelle != null ? g.Quelle.Funktion.Lausche?.Ausgaben.Select(N) ?? [] : g.Koepfe.Select(k => N(k.Typ));
        b.AppendLine($"Signatur: {(g.Quelle != null ? "Quelle " : "")}{string.Join(" | ", ein.DefaultIfEmpty("—"))} → "
                     + string.Join(" | ", g.Ausgaenge.Select(a => N(a.Typ)).Distinct().DefaultIfEmpty("—")));
        if (g.Fehler.Count > 0)
        {
            b.AppendLine("Befunde:");
            foreach (var (text, _) in g.Fehler) b.AppendLine("  ✗ " + text);
        }
        return b.ToString();
    }
}
