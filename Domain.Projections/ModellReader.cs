using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Reader der Modell-Projektion. Beantwortet <see cref="HoleModelle"/> (Liste + Aktiv-Markierung
/// per Join gegen den Singleton) und <see cref="HoleAktivesModell"/> (der Aktiv-Zeiger).
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class ModellReader : IReader<ModellProjektion>
{
    public async Task<ModellListe> Handle(
        HoleModelle query, IMessageEnvelope envelope, ReadContext ctx, IGetAlleModelle getAlleModelle, IGetAktivesModell getAktivesModell)
    {
        var modelle = await getAlleModelle.GetAlleAsync();
        var aktiv = await getAktivesModell.GetAktivesAsync();
        var aktivId = aktiv?.ModellId ?? Guid.Empty;

        var items = modelle.Select(m =>
        {
            ctx.Track(m.Id.ToString());
            return new ModellAntwort(
                m.Id, m.Name, m.Pfad, m.TrainingslaufId, m.DatensatzId, m.DatensatzVersion,
                m.Loss, m.Genauigkeit, m.Status, m.Id == aktivId, m.RegistriertAm);
        }).ToList();

        return new ModellListe(items);
    }

    public async Task<AktivesModellAntwort> Handle(
        HoleAktivesModell query, IMessageEnvelope envelope, ReadContext ctx, IGetAktivesModell getAktivesModell)
    {
        var aktiv = await getAktivesModell.GetAktivesAsync();
        if (aktiv is null)
            return new AktivesModellAntwort(Guid.Empty, null, null);

        ctx.Track(aktiv.ModellId.ToString());
        return new AktivesModellAntwort(aktiv.ModellId, aktiv.Name, aktiv.Pfad);
    }
}
