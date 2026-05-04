using Abstractions;
using Infrastructure.Mappers;
using Microsoft.Extensions.Logging;
using ProtoRepo;

namespace Infrastructure.Serialization;

/// <summary>
/// Mapper zwischen ProtoRepo DTOs und Domain Envelopes.
/// 
/// Arbeitet direkt mit CommandEnvelopeDto/EventEnvelopeDto.
/// Die alten CommandStream/EventStream Wrapper existieren nicht mehr.
/// </summary>
public partial class ProtoMessageMapper
{
    private readonly ILogger<ProtoMessageMapper>? _logger;

    public ProtoMessageMapper(ILogger<ProtoMessageMapper>? logger = null)
    {
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
            return ProtoCommandMappingHelpers.MapToDomain(dto);
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
            return ProtoCommandMappingHelpers.MapToDto(envelope);
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
            return ProtoEventMappingHelpers.MapToDomain(dto);
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
            return ProtoEventMappingHelpers.MapToDto(envelope);
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

    /// <summary>
    /// Mappt QueryPayloadDto zu IQuery
    /// </summary>
    public IQuery MapToDomain(QueryPayloadDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "QueryPayloadDto cannot be null");

        try
        {
            _logger?.LogDebug("Mapping QueryPayloadDto to domain");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return ProtoQueryMappingHelpers.MapToDomain(dto);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map QueryPayloadDto to domain");
            throw new MappingException("Failed to map QueryPayloadDto to domain", ex);
        }
    }

    /// <summary>
    /// Mappt IQuery zu QueryPayloadDto
    /// </summary>
    public QueryPayloadDto MapToDto(IQuery query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        try
        {
            _logger?.LogDebug("Mapping IQuery to QueryPayloadDto");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return ProtoQueryMappingHelpers.MapToDto(query);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map IQuery to DTO");
            throw new MappingException("Failed to map IQuery to DTO", ex);
        }
    }

    /// <summary>
    /// Extrahiert Query aus QueryRequest
    /// </summary>
    public IQuery MapToDomain(QueryRequest request)
    {
        if (request?.Payload == null)
            throw new ArgumentNullException(nameof(request), "QueryRequest or Payload cannot be null");

        return MapToDomain(request.Payload);
    }

    // =========================================================================
    // QUERY RESPONSE MAPPING
    // =========================================================================

    /// <summary>
    /// Mappt QueryResponsePayloadDto zu IQueryResponse
    /// </summary>
    public IQueryResponse MapToDomain(QueryResponsePayloadDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto), "QueryResponsePayloadDto cannot be null");

        try
        {
            _logger?.LogDebug("Mapping QueryResponsePayloadDto to domain");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return ProtoQueryResponseMappingHelpers.MapToDomain(dto);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map QueryResponsePayloadDto to domain");
            throw new MappingException("Failed to map QueryResponsePayloadDto to domain", ex);
        }
    }

    /// <summary>
    /// Mappt IQueryResponse zu QueryResponsePayloadDto
    /// </summary>
    public QueryResponsePayloadDto MapToDto(IQueryResponse response)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            _logger?.LogDebug("Mapping IQueryResponse to QueryResponsePayloadDto");
            // KORRIGIERT: Keine verschachtelte Klasse!
            return ProtoQueryResponseMappingHelpers.MapToDto(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map IQueryResponse to DTO");
            throw new MappingException("Failed to map IQueryResponse to DTO", ex);
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
        var data = (TResponse)MapToDomain(proto.Payload);

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
            var envelope = ProtoCommandMappingHelpers.MapToDomain(dto);
            return envelope.Payload;
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
            var envelope = ProtoEventMappingHelpers.MapToDomain(dto);
            return envelope.Payload;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to map Event payload");
            throw new MappingException("Failed to map Event payload", ex);
        }
    }
}

/// <summary>
/// Exception fÃ¼r Mapping-Fehler
/// </summary>
public class MappingException : Exception
{
    public MappingException(string message) : base(message) { }
    public MappingException(string message, Exception innerException) : base(message, innerException) { }
}