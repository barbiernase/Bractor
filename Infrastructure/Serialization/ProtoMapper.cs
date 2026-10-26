using Abstractions;
using Microsoft.Extensions.Logging;
using Cqrs.Leitung;

namespace Infrastructure.Serialization;

/// <summary>
/// Mapper zwischen der Leitung (Cqrs.Leitung: Hülle + Nutzlast) und den Domain Envelopes. Die Domänen-Nachricht reist als Nutzlast
/// (Typname + Bytes des Anwendungs-Protos); die Tabellen dafür generiert der DtoMapperSourceGenerator.
/// </summary>
public partial class ProtoMessageMapper
{
    private readonly INutzlastTabelle _tabelle;
    private readonly ILogger<ProtoMessageMapper>? _logger;

    /// <param name="tabelle">Die Nutzlast-Tabelle der Anwendung (generiert im Leitungs-Projekt, <c>GeneratedNutzlast</c>).</param>
    public ProtoMessageMapper(INutzlastTabelle tabelle, ILogger<ProtoMessageMapper>? logger = null)
    {
        _tabelle = tabelle;
        _logger = logger;
    }

    // =========================================================================
    // COMMAND MAPPING
    // =========================================================================

    /// <summary>
    /// Mappt CommandEnvelopeDto zu CommandEnvelope
    /// </summary>
    public CommandEnvelope MapToDomain(CommandEnvelopeDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "CommandEnvelopeDto cannot be null");

        try
        {
            _logger?.LogDebug("Mapping CommandEnvelopeDto to CommandEnvelope");
            return CommandAusHuelle(dto);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map CommandEnvelopeDto to domain");
            throw new MappingException("Failed to map CommandEnvelopeDto to domain", ex);
        }
    }

    /// <summary>
    /// Mappt CommandEnvelope zu CommandEnvelopeDto
    /// </summary>
    public CommandEnvelopeDto MapToDto(CommandEnvelope envelope)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        try
        {
            _logger?.LogDebug("Mapping CommandEnvelope to CommandEnvelopeDto");
            return CommandHuelle(envelope);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map CommandEnvelope to DTO");
            throw new MappingException("Failed to map CommandEnvelope to DTO", ex);
        }
    }

    /// <summary>
    /// Extrahiert Command aus CommandRequest
    /// </summary>
    public CommandEnvelope MapToDomain(CommandRequest request)
    {
        if (request?.Envelope == null)
            throw new ArgumentNullException(nameof(request), "CommandRequest or Envelope cannot be null");

        return MapToDomain(request.Envelope);
    }

    // =========================================================================
    // EVENT MAPPING
    // =========================================================================

    /// <summary>
    /// Mappt EventEnvelopeDto zu EventEnvelope
    /// </summary>
    public EventEnvelope MapToDomain(EventEnvelopeDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "EventEnvelopeDto cannot be null");

        try
        {
            _logger?.LogDebug("Mapping EventEnvelopeDto to EventEnvelope");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return EventAusHuelle(dto);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map EventEnvelopeDto to domain");
            throw new MappingException("Failed to map EventEnvelopeDto to domain", ex);
        }
    }

    /// <summary>
    /// Mappt EventEnvelope zu EventEnvelopeDto
    /// </summary>
    public EventEnvelopeDto MapToDto(EventEnvelope envelope)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        try
        {
            _logger?.LogDebug("Mapping EventEnvelope to EventEnvelopeDto");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return EventHuelle(envelope);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map EventEnvelope to DTO");
            throw new MappingException("Failed to map EventEnvelope to DTO", ex);
        }
    }

    /// <summary>
    /// Erstellt EventNotification aus EventEnvelope
    /// </summary>
    public EventNotification ToEventNotification(EventEnvelope envelope)
    {
        return new EventNotification { Envelope = MapToDto(envelope) };
    }

    // =========================================================================
    // QUERY MAPPING
    // =========================================================================

    /// <summary>Die Query aus einer Nutzlast.</summary>
    public IQuery QueryAus(Nutzlast n)
    {
        ArgumentNullException.ThrowIfNull(n);
        try { return _tabelle.Query(n); }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map query payload {Typ} to domain", n.Typ);
            throw new MappingException($"Failed to map query payload {n.Typ} to domain", ex);
        }
    }

    /// <summary>Eine Query als Nutzlast.</summary>
    public Nutzlast MapToDto(IQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        try { return _tabelle.PackeQuery(query); }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map IQuery to Nutzlast");
            throw new MappingException("Failed to map IQuery to Nutzlast", ex);
        }
    }

    /// <summary>
    /// Extrahiert Query aus QueryRequest
    /// </summary>
    public IQuery MapToDomain(QueryRequest request)
    {
        if (request?.Payload == null)
            throw new ArgumentNullException(nameof(request), "QueryRequest or Payload cannot be null");

        return QueryAus(request.Payload);
    }

    // =========================================================================
    // QUERY RESPONSE MAPPING
    // =========================================================================

    /// <summary>Die Antwort aus einer Nutzlast.</summary>
    public IQueryResponse AntwortAus(Nutzlast n)
    {
        ArgumentNullException.ThrowIfNull(n);
        try { return _tabelle.Antwort(n); }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map query response {Typ} to domain", n.Typ);
            throw new MappingException($"Failed to map query response {n.Typ} to domain", ex);
        }
    }

    /// <summary>Eine Antwort als Nutzlast.</summary>
    public Nutzlast MapToDto(IQueryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        try { return _tabelle.PackeAntwort(response); }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map IQueryResponse to Nutzlast");
            throw new MappingException("Failed to map IQueryResponse to Nutzlast", ex);
        }
    }

    /// <summary>
    /// Erstellt QueryResponse aus QueryResponse&lt;IQueryResponse&gt; (mit Deps)
    /// </summary>
    public QueryResponse ToQueryResponse(Abstractions.QueryResponse<IQueryResponse> response, string correlationId)
    {
        var dto = new QueryResponse
        {
            CorrelationId = correlationId,
            Payload = MapToDto(response.Data)
        };

        if (response.Deps != null)
        {
            foreach (var dep in response.Deps)
            {
                dto.Deps.Add(new AggregateMetaDto
                {
                    Id = dep.Id.ToString(),
                    AggregateType = dep.AggregateType,
                    Version = dep.Version
                });
            }
        }

        return dto;
    }

    /// <summary>
    /// Mappt Proto QueryResponse zurück zu Domain QueryResponse&lt;T&gt; (Client-Seite)
    /// </summary>
    public Abstractions.QueryResponse<TResponse> MapToQueryResponse<TResponse>(QueryResponse proto)
        where TResponse : IQueryResponse
    {
        var data = (TResponse)AntwortAus(proto.Payload);

        IReadOnlyList<AggregateMeta>? deps = null;
        if (proto.Deps.Count > 0)
        {
            deps = proto.Deps
                .Select(d => new AggregateMeta(Guid.Parse(d.Id), d.AggregateType, d.Version))
                .ToList();
        }

        return new Abstractions.QueryResponse<TResponse> { Data = data, Deps = deps };
    }

    // =========================================================================
    // HÜLLEN (Framework): Metadaten-Felder kopieren, die Nutzlast packt/entpackt die Tabelle
    // =========================================================================

    private CommandEnvelope CommandAusHuelle(CommandEnvelopeDto dto)
    {
        var payload = _tabelle.Command(dto.Payload);
        // Das Ziel steht im Command (ICommand<TAggregat>, Plan §2.5): Id und Aggregat-Typ der Hülle wiederholen es nur. Weichen sie ab, ist der
        //   Command an eine fremde Id bzw. ein fremdes Aggregat gerichtet — abgelehnt, bevor er irgendwohin geht (der Aufrufer meldet den Fehler).
        var aggregat = Infrastructure.Extensions.AggregateDispatcherExtensions.ResolveAggregateType(payload);
        if (!string.IsNullOrEmpty(dto.AggregateId) && (!Guid.TryParse(dto.AggregateId, out var id) || id != payload.Ziel))
            throw new InvalidOperationException(
                $"{payload.GetType().Name}: die Hülle nennt das Ziel {dto.AggregateId}, der Command {payload.Ziel} — abgelehnt");
        if (!string.IsNullOrEmpty(dto.AggregateType) && dto.AggregateType != aggregat)
            throw new InvalidOperationException(
                $"{payload.GetType().Name}: die Hülle nennt das Aggregat {dto.AggregateType}, der Command gehört zu {aggregat} — abgelehnt");
        return CommandAusHuelle(dto, payload, aggregat);
    }

    private static CommandEnvelope CommandAusHuelle(CommandEnvelopeDto dto, ICommand payload, string aggregat) => new()
    {
        CommandId = Guid.Parse(dto.CommandId),
        AggregateType = aggregat,
        // Über gRPC reisen NUR Client-Commands (OCC) — der interne Emittiert-Pfad kreuzt nie den Draht.
        Modus = new CommandModus.Client(dto.ExpectedVersion),
        CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(dto.CreatedAtUtc),
        CorrelationId = dto.CorrelationId,
        UserId = dto.UserId,
        OriginSessionId = string.IsNullOrEmpty(dto.OriginSessionId) ? null : dto.OriginSessionId,
        Payload = payload,
    };

    private CommandEnvelopeDto CommandHuelle(CommandEnvelope envelope) => new()
    {
        CommandId = envelope.CommandId.ToString(),
        AggregateId = envelope.AggregateId.ToString(),
        AggregateType = envelope.AggregateType ?? string.Empty,
        ExpectedVersion = envelope.Modus is CommandModus.Client c ? c.ExpectedVersion
            : throw new NotSupportedException("Nur Client-Commands passieren gRPC; Emittiert ist intern."),
        CreatedAtUtc = envelope.CreatedAtUtc.ToUnixTimeMilliseconds(),
        CorrelationId = envelope.CorrelationId,
        UserId = envelope.UserId,
        OriginSessionId = envelope.OriginSessionId ?? string.Empty,
        Payload = _tabelle.PackeCommand(envelope.Payload),
    };

    private EventEnvelope EventAusHuelle(EventEnvelopeDto dto) => new()
    {
        EventId = Guid.Parse(dto.EventId),
        AggregateId = Guid.Parse(dto.AggregateId),
        AggregateType = dto.AggregateType,
        AggregateVersion = dto.AggregateVersion,
        StreamHeadVersion = dto.StreamHeadVersion,
        CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(dto.CreatedAtUtc),
        CausationId = dto.CausationId,
        CorrelationId = dto.CorrelationId,
        UserId = dto.UserId,
        TargetSubscriberId = string.IsNullOrEmpty(dto.TargetSubscriberId) ? null : dto.TargetSubscriberId,
        Payload = _tabelle.Event(dto.Payload),
    };

    private EventEnvelopeDto EventHuelle(EventEnvelope envelope) => new()
    {
        EventId = envelope.EventId.ToString(),
        AggregateId = envelope.AggregateId.ToString(),
        AggregateType = envelope.AggregateType ?? string.Empty,
        AggregateVersion = envelope.AggregateVersion,
        StreamHeadVersion = envelope.StreamHeadVersion,
        CreatedAtUtc = envelope.CreatedAtUtc.ToUnixTimeMilliseconds(),
        CausationId = envelope.CausationId,
        CorrelationId = envelope.CorrelationId,
        UserId = envelope.UserId,
        TargetSubscriberId = envelope.TargetSubscriberId ?? string.Empty,
        Payload = _tabelle.PackeEvent(envelope.Payload),
    };

    // =========================================================================
    // PAYLOAD MAPPING (ohne Envelope)
    // =========================================================================

    /// <summary>
    /// Mappt nur den Command Payload (ohne Envelope)
    /// </summary>
    public ICommand MapCommandPayload(CommandEnvelopeDto dto)
    {
        try
        {
            _logger?.LogDebug("Mapping Command payload to domain");
            return _tabelle.Command(dto.Payload);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map Command payload");
            throw new MappingException("Failed to map Command payload", ex);
        }
    }

    /// <summary>
    /// Mappt nur den Event Payload (ohne Envelope)
    /// </summary>
    public IEvent MapEventPayload(EventEnvelopeDto dto)
    {
        try
        {
            _logger?.LogDebug("Mapping Event payload to domain");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return _tabelle.Event(dto.Payload);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map Event payload");
            throw new MappingException("Failed to map Event payload", ex);
        }
    }
}

/// <summary>
/// Exception für Mapping-Fehler
/// </summary>
public class MappingException : Exception
{
    public MappingException(string message) : base(message) { }
    public MappingException(string message, Exception innerException) : base(message, innerException) { }
}