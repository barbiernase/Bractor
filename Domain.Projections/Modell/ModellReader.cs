using Abstractions;

namespace Domain.Projections.Modell;

/// <summary>
/// Reader der Modell-Projektion. Beantwortet <see cref="HoleModelle"/> (Liste + Aktiv-Markierung
/// per Join gegen den Singleton) und <see cref="HoleAktivesModell"/> (der Aktiv-Zeiger).
/// </summary>
public sealed class ModellReader : IReader
{
    public async Task<ModellListe> Frage(
        HoleModelle query, IGetAlleModelle getAlleModelle, IGetAktivesModell getAktivesModell)
    {
        var modelle = await getAlleModelle.GetAlleAsync();
        var aktiv = await getAktivesModell.GetAktivesAsync();
        var aktivId = aktiv?.ModellId;

        var items = modelle.Select(m => new ModellAntwort(
            m.Id, m.Name, m.Pfad, m.TrainingslaufId,
            m.DatensatzId, m.DatensatzVersion,
            m.Loss, m.Genauigkeit, m.Status, m.Id == aktivId, m.RegistriertAm)).ToList();

        return new ModellListe(items);
    }

    public async Task<AktivesModellAntwort> Frage(
        HoleAktivesModell query, IGetAktivesModell getAktivesModell)
    {
        var aktiv = await getAktivesModell.GetAktivesAsync();
        if (aktiv is null)
            return new AktivesModellAntwort(default, null, null);

        return new AktivesModellAntwort(aktiv.ModellId, aktiv.Name, aktiv.Pfad);
    }
}
