using System.Text.Json.Nodes;

namespace Infrastructure.Umgebung;

/// <summary>
/// Ein Boot-Fehler mit Ort (docs/konzept-werkbank.md §5 Schritt 5): die Karte im Editor, an der er erscheint — im Wirker-Format
/// (<c>funktion:IBildVerkleinerung</c>, <c>quelle:DateiErkannt</c>) bzw. <c>host:&lt;Abschnitt&gt;</c> für den Host-Rahmen.
/// </summary>
public sealed class BootFehler(string text, string? karte) : InvalidOperationException(text)
{
    public string? Karte { get; } = karte;
}

/// <summary>Meldet einen Boot-Fehler als eine maschinenlesbare Zeile <c>CQRS-BOOT {json}</c> (stderr) — die Werkbank liest sie.</summary>
public static class CqrsBoot
{
    public static void Melde(Exception e)
    {
        var bf = e as BootFehler;
        for (var i = e.InnerException; bf == null && i != null; i = i.InnerException) bf = i as BootFehler;
        var zeile = new JsonObject { ["text"] = (bf ?? e).Message, ["karte"] = bf?.Karte, ["art"] = e.GetType().Name };
        Console.Error.WriteLine("CQRS-BOOT " + zeile.ToJsonString());
    }
}
