using Abstractions;

namespace Pruefwerk.Katalog.Import;

/// <summary>Nimmt einen Import an: die Id des Vorgangs aus dem Eingang abgeleitet (dieselbe bei jeder Wiederholung, R13).</summary>
public sealed class Annahme : IFunktion
{
    public Angenommen Auf(Anfordern a, Herkunft h) => new(h.NeueId("vorgang"), a.Menge);
}
