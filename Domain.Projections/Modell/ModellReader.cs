using Abstractions;

namespace Domain.Projections.Modell;

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
                new Id<Domain.Modell.Modell>(m.Id), m.Name, m.Pfad, new Id<Domain.Trainingslauf.Trainingslauf>(m.TrainingslaufId),
                new Id<Domain.Datensatz.Datensatz>(m.DatensatzId), m.DatensatzVersion,
                m.Loss, m.Genauigkeit, m.Status, m.Id == aktivId, m.RegistriertAm);
        }).ToList();

        return new ModellListe(items);
    }

    public async Task<AktivesModellAntwort> Handle(
        HoleAktivesModell query, IMessageEnvelope envelope, ReadContext ctx, IGetAktivesModell getAktivesModell)
    {
        var aktiv = await getAktivesModell.GetAktivesAsync();
        if (aktiv is null)
            return new AktivesModellAntwort(default, null, null);

        ctx.Track(aktiv.ModellId.ToString());
        return new AktivesModellAntwort(new Id<Domain.Modell.Modell>(aktiv.ModellId), aktiv.Name, aktiv.Pfad);
    }
}
