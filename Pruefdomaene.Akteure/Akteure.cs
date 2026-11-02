using Abstractions;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Pipeline.Abrechnung;

namespace Pruefdomaene.Akteure;

// Die Akteure der Prüfdomäne — wer von draußen hineingibt (IDarf) und was zu ihm kommt (IHoert: ein Command ohne Ziel ist Arbeit).

/// <summary>Prüft große Rechnungen von Hand (Arbeit: <see cref="RechnungZuPruefen"/>) und gibt sie frei oder sperrt sie.</summary>
public sealed record Rechnungspruefer : IAkteur,
    IHoert<RechnungZuPruefen>,
    IDarf<GibRechnungFrei>, IDarf<SperreRechnung>;
