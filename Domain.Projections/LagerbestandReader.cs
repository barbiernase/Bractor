using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Reader für Lagerbestand-Queries — eigenständige Top-Level-Klasse.
///
/// DI: ILagerbestandReadStore — entkoppelt von der Write-Seite.
/// In-Memory teilen sich Read- und Write-Store dieselbe Datenquelle.
/// In Produktion: Read-Store kann Caching, materialisierte Views, 
/// oder eine Read-Replica nutzen — ohne den Writer zu berühren.
///
/// Zugehörigkeit zur Projektion über IReader&lt;LagerbestandProjection&gt;.
/// [ProjectionReader(TrackDeps = true)] aktiviert Deps-Laden aus Redis.
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class LagerbestandReader : IReader<LagerbestandProjection>
{
    private readonly ILagerbestandReadStore _store;

    public LagerbestandReader(ILagerbestandReadStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Einzelnen Lagerbestand abfragen.
    /// Rückgabe: LagerbestandAntwort ODER LagerbestandNichtGefunden — in der Signatur sichtbar.
    /// </summary>
    public async Task<OneOf<LagerbestandAntwort, LagerbestandNichtGefunden>>
        Handle(GetLagerbestand query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.FindByIdAsync(query.ArtikelId);

        if (model != null)
        {
            ctx.Track(query.ArtikelId.ToString());
            return new LagerbestandAntwort(
                model.Id, model.Name, model.Anzahl, model.LetzteAktualisierung);
        }

        return new LagerbestandNichtGefunden(query.ArtikelId);
    }

    /// <summary>
    /// Alle Lagerbestände abfragen.
    /// Nur ein mögliches Outcome → kein OneOf nötig, direkt Task&lt;T&gt;.
    /// </summary>
    public async Task<LagerbestandListe>
        Handle(GetAllLagerbestaende query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var models = await _store.GetAllAsync();

        var items = models.Select(m =>
        {
            ctx.Track(m.Id.ToString());
            return new LagerbestandAntwort(
                m.Id, m.Name, m.Anzahl, m.LetzteAktualisierung);
        }).ToList();

        return new LagerbestandListe(items);
    }
}