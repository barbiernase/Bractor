using Abstractions;
using Core;
using Domain.Todo;

namespace Domain.Projections;

/// <summary>
/// Projektion für Todos — Write-Seite.
///
/// Nutzt ITodoWriteStore für Persistenz.
/// Reader ist als eigenständige Top-Level-Klasse (TodoReader.cs)
/// mit ITodoReadStore — vollständige CQRS-Trennung.
///
/// Handler-Signatur: Handle(TEvent, IAggregateEnvelope, ProjectionWriter)
/// </summary>
public partial class TodoProjection : ISubscriber
{
    private readonly ITodoWriteStore _store;

    public TodoProjection(ITodoWriteStore store)
    {
        _store = store;
    }

    public string SubscriberId => "todo-projection";

    public async Task Handle(
        TodoErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.UpsertAsync(new TodoReadModel
            {
                Id = envelope.AggregateId,
                Titel = evt.Titel,
                Beschreibung = evt.Beschreibung,
                Status = TodoStatus.Offen,
                Prioritaet = evt.Prioritaet,
                Faelligkeit = evt.Faelligkeit,
                ErstelltAm = envelope.CreatedAtUtc,
                Tags = Array.Empty<string>()
            });
        });
    }

    public async Task Handle(
        TitelGeaendert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.UpdateTitelAsync(envelope.AggregateId, evt.NeuerTitel);
        });
    }

    public async Task Handle(
        PrioritaetGesetzt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.UpdatePrioritaetAsync(envelope.AggregateId, evt.NeuePrioritaet);
        });
    }

    public async Task Handle(
        AlsErledigtMarkiert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.UpdateStatusAsync(
                envelope.AggregateId, TodoStatus.Erledigt, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        WiederGeoeffnet evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.UpdateStatusAsync(
                envelope.AggregateId, TodoStatus.Offen, null);
        });
    }

    public async Task Handle(
        Archiviert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.UpdateStatusAsync(
                envelope.AggregateId, TodoStatus.Archiviert, null);
        });
    }

    public async Task Handle(
        TagHinzugefuegt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.AddTagAsync(envelope.AggregateId, evt.Tag);
        });
    }

    public async Task Handle(
        TagEntfernt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<TodoItem>(envelope.AggregateId);
            await _store.RemoveTagAsync(envelope.AggregateId, evt.Tag);
        });
    }
}