using Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Instanzen der Graphen der Beispieldomäne für die Hosts der Integrations-Tests (je Graph eine, wie <c>umgebungen/sim-main/graphen</c>): die
/// Bildaufbereitung (aus dem Katalog) mit Parallel und Wiederholungen ihrer drei Bild-Knoten und dem Ausgabe-Pfad — mit <paramref name="eingang"/> im
/// Linienimport unter einem Dateiwächter auf diesem Verzeichnis, sonst in der Bildaufnahme (Kopf DateiGefunden) —, die übrigen mit den Standards. Die Arbeit der Akteure
/// (Klassifizierer, TrainingsSystem) kommt ohne Datei dazu.
/// </summary>
internal static class Graphinstanzen
{
    public static Dictionary<string, GraphInstanzKonfig> Beispieldomaene(string? ausgabe, int parallel = 0, int wiederholungen = 0, string? eingang = null)
    {
        KnotenKonfig Bild(bool mitPfad)
        {
            var k = new KnotenKonfig { Parallel = parallel, Wiederholungen = wiederholungen };
            if (mitPfad && ausgabe is not null) k.Werte["AusgabePfad"] = ausgabe;
            return k;
        }
        const string vor = "Aufbereitung.";
        var bild = new GraphInstanzKonfig
        {
            Graph = eingang is null ? "Bildaufnahme" : "Linienimport",
            Akteur = eingang is null ? null : "KameraSystem",
            Knoten = { [vor + "Einlesen"] = Bild(false), [vor + "Verkleinern"] = Bild(true), [vor + "Kontrast"] = Bild(true) },
        };
        if (eingang is not null)
        {
            var w = new KnotenKonfig();
            w.Werte["Pfad"] = eingang;
            w.Werte["Intervall"] = "00:00:00.200";
            w.Werte["Fenster"] = "00:00:00.300";
            bild.Knoten["Eingang"] = w;
        }
        return new Dictionary<string, GraphInstanzKonfig>(StringComparer.Ordinal) { [eingang is null ? "bildaufnahme" : "linienimport"] = bild };
    }
}
