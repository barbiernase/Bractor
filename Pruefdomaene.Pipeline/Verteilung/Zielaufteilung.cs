using Abstractions;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>JE: die reservierte Verteilung bucht jedes Ziel für sich — ein Strom, je Ziel ein Unterlauf.</summary>
public sealed class Zielaufteilung : IFunktion
{
    public IEnumerable<ZielZubuchen> Auf(Zubuchen c) => c.Ziele.Select(z => new ZielZubuchen(c.VerteilungId, z, c.MengeJeZiel));
}
