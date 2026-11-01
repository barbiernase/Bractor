using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>Die Freigabe im Host: bis 10 000 frei, darüber gesperrt.</summary>
public sealed class Freigabeautomat : IFreigabe
{
    public OneOf<GibRechnungFrei, SperreRechnung> Auf(Freizugeben f) =>
        f.Betrag > 10_000 ? new SperreRechnung(f.Rechnung, "über 10 000") : new GibRechnungFrei(f.Rechnung);
}
