using Abstractions;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Abrechnung;

/// <summary>
/// Das Rechnungsbild aus BÜNDELN (docs/04 §4.3.1) — je Schlüsselart eines: das Auf wird gerufen, sobald zum Schlüssel von jedem Typ ein Event im Log
/// steht, und bei jedem weiteren erneut (je Typ mit dem neuesten). Den Schlüssel leitet der Generator aus den Events ab.
/// </summary>
public sealed class RechnungsbildProjektion : IProjektion
{
    /// <summary>STREAM: beide Events entscheidet die Rechnung — der Schlüssel ist ihr Stream.</summary>
    public Task Auf(Buendel<RechnungGestellt, RechnungFreigegeben> b, Herkunft h, ISchreibeFreigabe schreibe)
        => schreibe.FreigegebenAsync(b.Erstes.Rechnung, b.Erstes.Betrag, h);

    /// <summary>
    /// FELD: die Gutschrift entscheidet das Kundenkonto und nennt die Rechnung im Feld. Beide Events nennen Rechnung und Konto — <c>[Ueber]</c> wählt
    /// die Rechnung (für die gestellte Rechnung ihr Stream, für die Gutschrift ihr Feld).
    /// </summary>
    public Task Auf([Ueber<Rechnung>] Buendel<RechnungGestellt, GutschriftGebucht> b, Herkunft h, ISchreibeGutschrift schreibe)
        => schreibe.GutgeschriebenAsync(b.Erstes.Rechnung, b.Erstes.Betrag, b.Zweites.Betrag, h);

    /// <summary>GLOBAL: Sperre und Ausreißer haben keine Id gemeinsam — je Typ das neueste Event.</summary>
    public Task Auf(Buendel<RechnungGesperrt, AusreisserGemeldet> b, Herkunft h, ISchreibeLage schreibe)
        => schreibe.LageAsync(b.Erstes.Grund, b.Zweites.Wert, h);
}
