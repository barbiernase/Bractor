namespace Pruefwerk.Katalog.Messung;

/// <summary>Der Zählstand je Stelle — gesichert je Instanz und Knoten (IZustand).</summary>
public sealed class Zaehlstand
{
    public Dictionary<Guid, int> Je { get; set; } = new();
}
