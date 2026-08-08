using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.Projections;
namespace Domain.Client.Modules.Chart;

public partial class ChartRefreshHandler
{
    private readonly ChartStore _chart;
    public ChartRefreshHandler(ChartStore chart) => _chart = chart;

    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx)
    { yield return new GetProduktionsTage(); }

    IEnumerable<object> Handle(ProduktionsTageAntwort antwort, MessageContext ctx)
    {
        if (_chart.GewaehlterTag != null || antwort.Tage.Count == 0) yield break;
        var tag = antwort.Tage[0].Datum;
        yield return new TagGewaehlt(tag);
        var von = new DateTimeOffset(tag.Date, TimeSpan.Zero);
        yield return new GetProduktionsStrip(von, von.AddDays(1));
    }
}
