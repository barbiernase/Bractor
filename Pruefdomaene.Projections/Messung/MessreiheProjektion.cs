using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Messung;

/// <summary>
/// Die Messreihe je Stelle — in einen Store der Klasse 2 (Dateien): jeder Messpunkt unter der Identität seines Events, der letzte Wert bedingt.
/// Wiederholt der Batch-Leser einen Batch (Abbruch zwischen den Wirkungen und dem Fortschritt), ergibt das dasselbe.
/// </summary>
public sealed class MessreiheProjektion : IProjektion
{
    public Task Auf(MesswertErfasst e, Herkunft h, ISchreibeMesspunkt schreibe) => schreibe.SchreibeAsync(h.Id<Messstelle>(), e.Wert, h);
}
