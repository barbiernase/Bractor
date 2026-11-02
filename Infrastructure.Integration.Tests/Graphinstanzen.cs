using Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Instanzen der Graphen der Beispieldomäne für die Hosts der Integrations-Tests (je Graph eine, wie <c>umgebungen/sim-main/graphen</c>): die
/// Bildaufbereitung mit dem Laufort und Parallel ihrer drei Bild-Knoten und dem Ausgabe-Pfad, die übrigen mit den Standards.
/// </summary>
internal static class Graphinstanzen
{
    public static Dictionary<string, GraphInstanzKonfig> Beispieldomaene(string? ausgabe, FunktionsLaufort laufort = FunktionsLaufort.Host, int parallel = 0,
        int wiederholungen = 0)
    {
        KnotenKonfig Bild(bool mitPfad)
        {
            var k = new KnotenKonfig { Laufort = laufort, Parallel = parallel, Wiederholungen = wiederholungen };
            if (mitPfad && ausgabe is not null) k.Werte["AusgabePfad"] = ausgabe;
            return k;
        }
        var aus = new Dictionary<string, GraphInstanzKonfig>(StringComparer.Ordinal)
        {
            ["bildaufbereitung"] = new()
            {
                Graph = "Bildaufbereitung",
                Knoten = { ["Einlesen"] = Bild(false), ["Verkleinern"] = Bild(laufort == FunktionsLaufort.Host), ["Kontrast"] = Bild(laufort == FunktionsLaufort.Host) },
            },
            ["paarbeurteilung"] = new() { Graph = "Paarbeurteilung" },
        };
        foreach (var g in new[] { "Datensatzpflege", "Teilmeldung", "Trainingsrechnung" })
            aus[g.ToLowerInvariant()] = new GraphInstanzKonfig { Graph = g };
        return aus;
    }
}
