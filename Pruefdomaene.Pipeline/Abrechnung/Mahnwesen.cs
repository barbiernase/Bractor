using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine Funktion mit Dienst: der Mahnversand ist Außenwelt (nicht gleichbleibend), also hält das Framework die Ausgabe fest, bevor sie wirkt (A1, R12)
/// — eine zweite Aktivierung oder ein Absturz nach dem Versand vermerkt dieselbe Mahnung, nie eine zweite mit anderer Versandnummer.
/// </summary>
public sealed class Mahnwesen(IMahnversand versand) : IFunktion
{
    public async Task<OneOf<VermerkeMahnung, MahnungUnnoetig>> Auf(ZahlungUeberfaellig e, Herkunft h)
    {
        if (e.Betrag <= 0) return new MahnungUnnoetig(e.Rechnung);
        var nummer = await versand.VersendeAsync(e.Konto, e.Rechnung, e.Mahnstufe);
        return new VermerkeMahnung(e.Konto, e.Rechnung, nummer, e.Mahnstufe);
    }
}
