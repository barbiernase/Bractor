using Abstractions;

namespace Infrastructure.Deadlines;

/// <summary>
/// Die Senke für <see cref="FristAuftrag"/> — was eine Pipeline als <c>Frist&lt;TCmd&gt;</c>/<c>FristStorno&lt;TCmd&gt;</c>
/// ausgibt, landet hier (der generierte Dispatch hat den Kontext schon als Konstante eingesetzt). Fällig = DB-Uhr + Dauer;
/// die Frist-Id ist deterministisch (Kontext + Ziel) → erneutes Planen überschreibt, Storno trifft dieselbe Frist.
/// </summary>
public sealed class FristPlaner
{
    private readonly IFristplan _plan;
    private readonly IDbClock _uhr;

    public FristPlaner(IFristplan plan, IDbClock uhr)
    {
        _plan = plan;
        _uhr = uhr;
    }

    public async Task PlaneAsync(FristAuftrag auftrag, CancellationToken ct = default)
    {
        if (auftrag.Dauer is { } dauer)
            await _plan.PlaneAsync(new Frist(auftrag.FristId, await _uhr.JetztAsync(ct) + dauer, auftrag.ZielAggregatId, auftrag.Kontext,
                ImAuftrag.IstAkteur(ImAuftrag.Akteur) ? ImAuftrag.Akteur : null), ct);
        else
            await _plan.EntferneAsync(auftrag.FristId, ct);   // folgenlos, falls schon gefeuert/entfernt
    }
}
