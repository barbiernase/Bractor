using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Datensaetze;
using Domain.Datensatz;
using Domain.Projections;

namespace Domain.Client.Modules.DatensatzKomposition;

/// <summary>
/// Lädt die Kopf-Daten des aktiven Datensatzes bei Auswahl und hält sie nach jeder
/// Mutation frisch (nur für den aktiven Datensatz — <c>ctx.AggregateId</c> vergleichen).
/// Nach dem Einfrieren zusätzlich die Samples ziehen (für die live gerechnete Balance).
/// </summary>
public partial class DatensatzKompositionRefreshHandler
{
    private readonly DatensatzKompositionStore _store;
    public DatensatzKompositionRefreshHandler(DatensatzKompositionStore store) => _store = store;

    IEnumerable<object> Handle(DatensatzAusgewaehlt evt, MessageContext ctx)
    {
        yield return new HoleDatensatz(evt.DatensatzId);
    }

    IEnumerable<object> Handle(PaareAufgenommen evt, MessageContext ctx) => RefreshWennAktiv(ctx);
    IEnumerable<object> Handle(PaarAufgenommen evt, MessageContext ctx)  => RefreshWennAktiv(ctx);
    IEnumerable<object> Handle(PaarEntfernt evt, MessageContext ctx)     => RefreshWennAktiv(ctx);
    IEnumerable<object> Handle(SplitGesetzt evt, MessageContext ctx)     => RefreshWennAktiv(ctx);

    IEnumerable<object> Handle(DatensatzEingefroren evt, MessageContext ctx)
    {
        if (_store.AktuelleDatensatzId != ctx.AggregateId) yield break;
        yield return new HoleDatensatz(ctx.AggregateId);
        // Balance/Split live aus dem eingefrorenen Snapshot rechnen.
        yield return new HoleDatensatzSamples(ctx.AggregateId, evt.Version, 1, 500);
    }

    private IEnumerable<object> RefreshWennAktiv(MessageContext ctx)
    {
        if (_store.AktuelleDatensatzId == ctx.AggregateId)
            yield return new HoleDatensatz(ctx.AggregateId);
    }
}
