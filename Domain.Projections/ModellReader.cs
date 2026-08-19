using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Reader der Modell-Projektion. Beantwortet <see cref="HoleModelle"/> (Liste + Aktiv-Markierung
/// per Join gegen den Singleton) und <see cref="HoleAktivesModell"/> (der Aktiv-Zeiger).
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class ModellReader : IReader<ModellProjektion>
{
    private readonly IModellReadStore _store;

    public ModellReader(IModellReadStore store)
    {
        _store = store;
    }

    public async Task<ModellListe> Handle(
        HoleModelle query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var modelle = await _store.GetAlleAsync();
        var aktiv = await _store.GetAktivesAsync();
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
        HoleAktivesModell query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var aktiv = await _store.GetAktivesAsync();
        if (aktiv is null)
            return new AktivesModellAntwort(Guid.Empty, null, null);

        ctx.Track(aktiv.ModellId.ToString());
        return new AktivesModellAntwort(aktiv.ModellId, aktiv.Name, aktiv.Pfad);
    }
}
