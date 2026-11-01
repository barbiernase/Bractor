using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine Funktion, die eine andere ruft (der Mahnversand ist Außenwelt), vor dem Aggregat: sie hört die Folge <see cref="Mahnfall"/>. Nichts wird
/// festgehalten — rechnet sie einen Eingang nach einem Absturz erneut, kann der Versand eine zweite Nummer liefern; wirksam wird nur der erste
/// Vermerk (dieselbe Command-Id aus Eingang und Stelle, die Inbox verwirft den zweiten). Der Versand selbst ist mindestens einmal.
/// </summary>
public sealed class Mahnwesen(IMahnversand versand) : IFunktion
{
    public async Task<OneOf<VermerkeMahnung, MahnungUnnoetig>> Auf(Mahnfall e, Herkunft h)
    {
        if (e.Betrag <= 0) return new MahnungUnnoetig(e.Rechnung);
        var nummer = await versand.VersendeAsync(e.Konto, e.Rechnung, e.Mahnstufe);
        return new VermerkeMahnung(e.Konto, e.Rechnung, nummer, e.Mahnstufe);
    }
}
