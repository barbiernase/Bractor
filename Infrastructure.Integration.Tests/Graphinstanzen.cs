using Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Instanzen der Graphen der Beispieldomäne für die Hosts der Integrations-Tests (je Graph eine, wie <c>umgebungen/sim-main/graphen</c>): die
/// Bildaufbereitung mit dem Laufort und Parallel ihrer drei Bild-Knoten und dem Ausgabe-Pfad — mit <paramref name="eingang"/> als Linienimport unter
/// einem Dateiwächter auf diesem Verzeichnis, sonst für sich (Kopf DateiGefunden) —, die übrigen mit den Standards.
/// </summary>
internal static class Graphinstanzen
{
    public static Dictionary<string, GraphInstanzKonfig> Beispieldomaene(string? ausgabe, FunktionsLaufort laufort = FunktionsLaufort.Host, int parallel = 0,
        int wiederholungen = 0, string? eingang = null)
    {
        KnotenKonfig Bild(bool mitPfad)
        {
            var k = new KnotenKonfig { Laufort = laufort, Parallel = parallel, Wiederholungen = wiederholungen };
            if (mitPfad && ausgabe is not null) k.Werte["AusgabePfad"] = ausgabe;
            return k;
        }
        var vor = eingang is null ? "" : "Aufbereitung.";
        var bild = new GraphInstanzKonfig
        {
            Graph = eingang is null ? "Bildaufbereitung" : "Linienimport",
            Akteur = eingang is null ? null : "KameraSystem",
            Knoten =
            {
                [vor + "Einlesen"] = Bild(false), [vor + "Verkleinern"] = Bild(laufort == FunktionsLaufort.Host),
                [vor + "Kontrast"] = Bild(laufort == FunktionsLaufort.Host),
            },
        };
        if (eingang is not null)
        {
            var w = new KnotenKonfig();
            w.Werte["Pfad"] = eingang;
            w.Werte["Intervall"] = "00:00:00.200";
            w.Werte["Fenster"] = "00:00:00.300";
            bild.Knoten["Eingang"] = w;
        }
        var aus = new Dictionary<string, GraphInstanzKonfig>(StringComparer.Ordinal)
        {
            [eingang is null ? "bildaufbereitung" : "linienimport"] = bild,
            ["paarbeurteilung"] = new() { Graph = "Paarbeurteilung" },
        };
        foreach (var g in new[] { "Datensatzpflege", "Teilmeldung", "Trainingsrechnung" })
            aus[g.ToLowerInvariant()] = new GraphInstanzKonfig { Graph = g };
        return aus;
    }
}
