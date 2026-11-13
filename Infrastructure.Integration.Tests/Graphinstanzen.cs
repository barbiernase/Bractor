using Abstractions;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Die Instanzen der Graphen der Beispieldomäne für die Hosts der Integrations-Tests (je Graph eine, wie <c>umgebungen/sim-main/graphen</c>): die
/// Bildaufbereitung (aus dem Katalog, Kopf <c>LiesBild</c>) mit Parallel und Wiederholungen ihrer drei Bild-Knoten und dem Ausgabe-Pfad, die übrigen
/// mit den Standards. Das gedeutete Bild übersetzt die Domäne (Domain.Uebersetzungen); was ein Dateiwächter findet, gibt ein Eingang eines Clients
/// hinein (Clients:Linienrechner:Eingaenge), nicht die Instanz.
/// </summary>
internal static class Graphinstanzen
{
    public static Dictionary<string, GraphInstanzKonfig> Beispieldomaene(string? ausgabe, int parallel = 0, int wiederholungen = 0)
    {
        KnotenKonfig Bild(bool mitPfad)
        {
            var k = new KnotenKonfig { Parallel = parallel, Wiederholungen = wiederholungen };
            if (mitPfad && ausgabe is not null) k.Werte["AusgabePfad"] = ausgabe;
            return k;
        }
        var bild = new GraphInstanzKonfig
        {
            Graph = "Bildaufbereitung",
            Knoten = { ["Einlesen"] = Bild(false), ["Verkleinern"] = Bild(true), ["Kontrast"] = Bild(true) },
        };
        return new Dictionary<string, GraphInstanzKonfig>(StringComparer.Ordinal) { ["bildaufbereitung"] = bild };
    }
}
