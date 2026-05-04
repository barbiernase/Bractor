using Abstractions;
using Core;
using Domain.Lagerartikel;
using Domain.Profil;

namespace Domain.Projections;

/// <summary>
/// Audit-Log das Events von mehreren Aggregaten sammelt.
/// 
/// Nutzt IAuditLogWriteStore für Persistenz.
/// Reader ist als eigenständige Top-Level-Klasse (AuditLogReader.cs)
/// mit IAuditLogReadStore — vollständige CQRS-Trennung.
///
/// Handler-Signatur: Handle(TEvent, IAggregateEnvelope, ProjectionWriter)
/// Events haben immer Aggregate-Kontext → IAggregateEnvelope statt IMessageEnvelope.
/// </summary>
public partial class AuditLog : ISubscriber
{
    private readonly IAuditLogWriteStore _store;

    public AuditLog(IAuditLogWriteStore store)
    {
        _store = store;
    }

    public string SubscriberId => "audit-log";

    // =========================================================================
    // WRITER — Events loggen (alle nicht-reaktiv → async Task)
    // =========================================================================

    public async Task Handle(
        LagerartikelErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Lagerartikel erstellt", $"Name: {evt.Name}");
    }

    public async Task Handle(
        WareneingangGebucht evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Wareneingang", $"Anzahl: {evt.Anzahl}");
    }

    public async Task Handle(
        WarenabgangGebucht evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Warenabgang", $"Anzahl: {evt.Anzahl}");
    }

    public async Task Handle(
        LagerartikelDeaktiviert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Lagerartikel deaktiviert", "");
    }

    public async Task Handle(
        NachbestellungAngefordert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Nachbestellung angefordert", $"ArtikelId: {evt.ArtikelId}, Menge: {evt.Menge}");
    }

    public async Task Handle(
        ProfilErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Profil erstellt", $"Name: {evt.Name}");
    }

    public async Task Handle(
        NameGeändert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await LogAsync(envelope, "Name geändert", $"Neuer Name: {evt.NeuerName}");
    }

    private async Task LogAsync(IAggregateEnvelope envelope, string action, string details)
    {
        await _store.AppendAsync(new AuditEntry
        {
            Timestamp = envelope.CreatedAtUtc,
            AggregateId = envelope.AggregateId,
            AggregateType = envelope.AggregateType,
            UserId = envelope.UserId,
            Action = action,
            Details = details
        });
    }
}

public record AuditEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}