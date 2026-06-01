// ── Zielverzeichnis: Domain.Client/ImagePair/ViewModels/ChartViewModel.cs ──

using Client.Infrastructure.Abstractions;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// User-Intents für Chart-Interaktion.
///
/// Generator-managed:
///   _ladeProduktionsTage → GetProduktionsTage (IQuery)
///
/// Manuell:
///   LadeStripFuerTag → TagGewaehlt (IClientEvent) + GetProduktionsStrip (IQuery)
///
/// Liest: nichts (Tag-Datum kommt als Parameter)
/// </summary>
public partial class ChartViewModel : IViewModel
{
    /// <summary>
    /// Tagesliste vom Server laden.
    /// Generator erzeugt: public void LadeProduktionsTage()
    /// </summary>
    private GetProduktionsTage _ladeProduktionsTage() => new();

    /// <summary>
    /// Tag im Chart wählen + Strip laden.
    /// Multi-publish: TagGewaehlt (ChartStore setzt State) + GetProduktionsStrip (Server-Query).
    /// </summary>
    public void LadeStripFuerTag(DateTimeOffset datum)
    {
        _publish(new TagGewaehlt(datum));

        var von = new DateTimeOffset(datum.Date, TimeSpan.Zero);
        _publish(new GetProduktionsStrip(von, von.AddDays(1)));
    }
}
