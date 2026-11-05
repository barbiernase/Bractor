using Abstractions;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Pipeline.Abrechnung;

namespace Pruefdomaene.Akteure;

// Die Akteure der Prüfdomäne — wer von draußen hineingibt (IDarf) und was zu ihm kommt (IHoert: ein Command ohne Ziel ist Arbeit).

/// <summary>Prüft große Rechnungen von Hand (Arbeit: <see cref="RechnungZuPruefen"/>) und gibt sie frei oder sperrt sie.</summary>
public sealed record Rechnungspruefer : IAkteur,
    IHoert<RechnungZuPruefen>,
    IDarf<GibRechnungFrei>, IDarf<SperreRechnung>;

/// <summary>Fordert Hochrechnungen für Messstellen an (ein Client-Eingang: über den Katalog Rechenwerk an die Messstelle, docs/04 §4.4).</summary>
public sealed record Messwart : IAkteur,
    IDarf<Pruefdomaene.Pipeline.Skalierung.HochrechnungAngefordert>;
