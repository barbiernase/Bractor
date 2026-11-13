using Abstractions;

namespace Pruefwerk.Katalog;

/// <summary>Mahnt über die Technik (der Versand ist eine Ressource draußen) — oder meldet, dass nichts anzumahnen ist.</summary>
public sealed class Mahnlauf(IMahnversand versand) : IFunktion
{
    public async Task<OneOf<Gemahnt, NichtZuMahnen>> Auf(Mahne m)
    {
        if (m.Betrag <= 0) return new NichtZuMahnen(m.Vorgang);
        var nummer = await versand.VersendeAsync(m.Empfaenger, m.Vorgang, m.Stufe);
        return new Gemahnt(m.Vorgang, m.Empfaenger, nummer, m.Stufe);
    }
}
