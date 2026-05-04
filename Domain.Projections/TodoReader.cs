using Abstractions;
using Domain.Todo;

namespace Domain.Projections;

/// <summary>
/// Reader für Todo-Queries — eigenständige Top-Level-Klasse.
///
/// DI: ITodoReadStore — entkoppelt von der Write-Seite.
/// Zugehörigkeit zur Projektion über IReader&lt;TodoProjection&gt;.
/// [ProjectionReader(TrackDeps = true)] aktiviert Deps-Laden aus Redis.
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class TodoReader : IReader<TodoProjection>
{
    private readonly ITodoReadStore _store;

    public TodoReader(ITodoReadStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Einzelnes Todo abfragen.
    /// Rückgabe: TodoAntwort ODER TodoNichtGefundenAntwort.
    /// </summary>
    public async Task<OneOf<TodoAntwort, TodoNichtGefundenAntwort>>
        Handle(GetTodo query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.FindByIdAsync(query.TodoId);

        if (model != null)
        {
            ctx.Track(query.TodoId.ToString());
            return new TodoAntwort(
                model.Id, model.Titel, model.Beschreibung,
                model.Status, model.Prioritaet, model.Faelligkeit,
                model.ErstelltAm, model.ErledigtAm, model.Tags);
        }

        return new TodoNichtGefundenAntwort(query.TodoId);
    }

    /// <summary>
    /// Alle Todos abfragen.
    /// Nur ein mögliches Outcome → kein OneOf nötig.
    /// </summary>
    public async Task<TodoListe>
        Handle(GetAlleTodos query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var models = await _store.GetAllAsync();

        var items = models.Select(m =>
        {
            ctx.Track(m.Id.ToString());
            return new TodoAntwort(
                m.Id, m.Titel, m.Beschreibung,
                m.Status, m.Prioritaet, m.Faelligkeit,
                m.ErstelltAm, m.ErledigtAm, m.Tags);
        }).ToList();

        return new TodoListe(items);
    }
}