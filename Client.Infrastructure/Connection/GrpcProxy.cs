using System.Collections.Concurrent;
using System.Threading.Channels;
using Abstractions;
using Grpc.Core;
using Grpc.Net.Client;
using Infrastructure.Serialization;
using ProtoRepo;

namespace Client.Infrastructure.Connection;

/// <summary>
/// Verwaltet die bidirektionale gRPC-Verbindung zum Server.
/// 
/// Verwendet Channels für Event-Streaming (kein Rx.NET).
/// Implementiert IGrpcProxy für Testbarkeit.
/// </summary>
public class GrpcProxy : IGrpcProxy, IAsyncDisposable
{
    private readonly ProtoMessageMapper _mapper = new();
    
    private GrpcChannel? _channel;
    private CqrsClientService.CqrsClientServiceClient? _client;
    private AsyncDuplexStreamingCall<ClientMessage, ServerMessage>? _stream;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    
    // Channels für ausgehende Daten
    private readonly Channel<EventEnvelope> _events = Channel.CreateUnbounded<EventEnvelope>();
    private readonly Channel<ConnectionState> _stateChanges = Channel.CreateUnbounded<ConnectionState>();
    
    // FIX: Dictionary statt Channel für Query-Responses
    // Ermöglicht parallele Queries mit korrektem Matching
    private readonly ConcurrentDictionary<string, TaskCompletionSource<QueryResponse>> _pendingQueries = new();
    
    // State
    private ConnectionState _currentState = ConnectionState.Disconnected;
    public ConnectionState CurrentState => _currentState;
    public string? SessionId { get; private set; }
    public bool IsConnected => _currentState == ConnectionState.Connected;
    
    // Channel Readers (für Consumer)
    public ChannelReader<EventEnvelope> Events => _events.Reader;
    public ChannelReader<ConnectionState> StateChanges => _stateChanges.Reader;

    /// <summary>
    /// Verbindet zum Server und führt Capabilities-Handshake durch.
    /// </summary>
    public async Task<ClientCapabilities> ConnectAsync(
        string serverAddress, 
        IEnumerable<string> eventTypes,
        CancellationToken ct = default)
    {
        if (_currentState == ConnectionState.Connected)
            throw new InvalidOperationException("Already connected");
        
        await SetStateAsync(ConnectionState.Connecting);
        
        try
        {
            // 1. gRPC Channel erstellen
            _channel = GrpcChannel.ForAddress(serverAddress);
            _client = new CqrsClientService.CqrsClientServiceClient(_channel);
            
            // 2. Bidirektionalen Stream öffnen
            _stream = _client.Connect(cancellationToken: ct);
            
            // 3. Capabilities Request senden
            var capabilitiesRequest = new ClientMessage
            {
                Capabilities = new CapabilitiesRequest
                {
                    EventTypes = { eventTypes }
                }
            };
            
            await _stream.RequestStream.WriteAsync(capabilitiesRequest, ct);
            
            // 4. Auf Capabilities Response warten
            if (!await _stream.ResponseStream.MoveNext(ct))
                throw new InvalidOperationException("Server closed stream before sending capabilities");
            
            var response = _stream.ResponseStream.Current;
            if (response.MessageCase != ServerMessage.MessageOneofCase.CapabilitiesResponse)
                throw new InvalidOperationException($"Expected CapabilitiesResponse, got {response.MessageCase}");
            
            var capabilities = ClientCapabilities.FromResponse(response.CapabilitiesResponse);
            SessionId = capabilities.SessionId;
            
            // 5. Read-Loop starten
            _readCts = new CancellationTokenSource();
            _readTask = ReadLoopAsync(_readCts.Token);
            
            await SetStateAsync(ConnectionState.Connected);
            
            Console.WriteLine($"[GrpcProxy] Connected. SessionId: {SessionId}");
            
            return capabilities;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrpcProxy] Connect failed: {ex.Message}");
            await SetStateAsync(ConnectionState.Failed);
            throw;
        }
    }

    /// <summary>
    /// Sendet einen Command (Fire-and-Forget).
    /// Feedback kommt über Events Channel.
    /// </summary>
    public async Task SendCommandAsync(CommandEnvelope envelope, CancellationToken ct = default)
    {
        EnsureConnected();
        
        var dto = _mapper.MapToDto(envelope);
        var message = new ClientMessage
        {
            Command = new CommandRequest { Envelope = dto }
        };
        
        await _stream!.RequestStream.WriteAsync(message, ct);
        
        Console.WriteLine($"[GrpcProxy] Sent command: {envelope.Payload.GetType().Name}, CorrelationId: {envelope.CorrelationId}");
    }

    /// <summary>
    /// Sendet eine Query und wartet auf Response.
    /// Thread-safe: Mehrere Queries können parallel laufen.
    /// </summary>
    public async Task<QueryResponse<TResponse>> QueryAsync<TResponse>(
        IQuery query, 
        string correlationId,
        CancellationToken ct = default) where TResponse : IQueryResponse
    {
        EnsureConnected();
        
        // TaskCompletionSource VOR dem Senden registrieren
        var tcs = new TaskCompletionSource<QueryResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        if (!_pendingQueries.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"Query with CorrelationId '{correlationId}' already pending");
        }
        
        try
        {
            // Query senden
            var queryDto = _mapper.MapToDto(query);
            var message = new ClientMessage
            {
                Query = new QueryRequest
                {
                    CorrelationId = correlationId,
                    Payload = queryDto
                }
            };
            
            await _stream!.RequestStream.WriteAsync(message, ct);
            
            Console.WriteLine($"[GrpcProxy] Sent query: {query.GetType().Name}, CorrelationId: {correlationId}");
            
            // Auf Response warten (mit Cancellation-Support)
            using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
            
            var response = await tcs.Task;
            return _mapper.MapToQueryResponse<TResponse>(response);
        }
        finally
        {
            // Cleanup: Immer aus Dictionary entfernen
            _pendingQueries.TryRemove(correlationId, out _);
        }
    }

    /// <summary>
    /// Subscribes für einen Event-Typ.
    /// </summary>
    public async Task SubscribeAsync(string eventType, CancellationToken ct = default)
    {
        EnsureConnected();
        
        var message = new ClientMessage
        {
            Subscribe = new SubscribeRequest { EventType = eventType }
        };
        
        await _stream!.RequestStream.WriteAsync(message, ct);
        
        Console.WriteLine($"[GrpcProxy] Subscribed to: {eventType}");
    }

    /// <summary>
    /// Unsubscribes von einem Event-Typ.
    /// </summary>
    public async Task UnsubscribeAsync(string eventType, CancellationToken ct = default)
    {
        EnsureConnected();
        
        var message = new ClientMessage
        {
            Unsubscribe = new UnsubscribeRequest { EventType = eventType }
        };
        
        await _stream!.RequestStream.WriteAsync(message, ct);
        
        Console.WriteLine($"[GrpcProxy] Unsubscribed from: {eventType}");
    }

    /// <summary>
    /// Trennt die Verbindung.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_currentState == ConnectionState.Disconnected)
            return;
        
        Console.WriteLine("[GrpcProxy] Disconnecting...");
        
        // Pending Queries abbrechen
        foreach (var kvp in _pendingQueries)
        {
            kvp.Value.TrySetCanceled();
        }
        _pendingQueries.Clear();
        
        // Read-Loop stoppen
        _readCts?.Cancel();
        if (_readTask != null)
        {
            try { await _readTask; } catch { /* ignore */ }
        }
        
        // Stream schließen
        if (_stream != null)
        {
            try
            {
                await _stream.RequestStream.CompleteAsync();
            }
            catch { /* ignore */ }
            _stream.Dispose();
            _stream = null;
        }
        
        // Channel schließen
        _channel?.Dispose();
        _channel = null;
        _client = null;
        
        SessionId = null;
        await SetStateAsync(ConnectionState.Disconnected);
        
        Console.WriteLine("[GrpcProxy] Disconnected");
    }

    /// <summary>
    /// Liest kontinuierlich Server-Messages.
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && 
                   await _stream!.ResponseStream.MoveNext(ct))
            {
                var message = _stream.ResponseStream.Current;
                await HandleServerMessageAsync(message);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine("[GrpcProxy] Read loop cancelled");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[GrpcProxy] Read loop cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrpcProxy] Read loop error: {ex.Message}");
            await SetStateAsync(ConnectionState.Failed);
        }
    }

    private async Task HandleServerMessageAsync(ServerMessage message)
    {
        switch (message.MessageCase)
        {
            case ServerMessage.MessageOneofCase.Event:
                var envelope = _mapper.MapToDomain(message.Event.Envelope);
                await _events.Writer.WriteAsync(envelope);
                Console.WriteLine($"[GrpcProxy] Received event: {envelope.Payload.GetType().Name}, CorrelationId: {envelope.CorrelationId}");
                break;
                
            case ServerMessage.MessageOneofCase.QueryResponse:
                // FIX: Response an den richtigen Waiter dispatchen
                var correlationId = message.QueryResponse.CorrelationId;
                if (_pendingQueries.TryGetValue(correlationId, out var tcs))
                {
                    tcs.TrySetResult(message.QueryResponse);
                    Console.WriteLine($"[GrpcProxy] Received query response, CorrelationId: {correlationId}");
                }
                else
                {
                    Console.WriteLine($"[GrpcProxy] WARNING: Received query response for unknown CorrelationId: {correlationId}");
                }
                break;
                
            case ServerMessage.MessageOneofCase.SubscriptionConfirmed:
                Console.WriteLine($"[GrpcProxy] Subscription confirmed: {message.SubscriptionConfirmed.EventType}");
                break;
                
            case ServerMessage.MessageOneofCase.UnsubscriptionConfirmed:
                Console.WriteLine($"[GrpcProxy] Unsubscription confirmed: {message.UnsubscriptionConfirmed.EventType}");
                break;
                
            case ServerMessage.MessageOneofCase.Error:
                // FIX: Fehler an Query weiterleiten falls CorrelationId passt
                var errorCorrelationId = message.Error.CorrelationId;
                if (!string.IsNullOrEmpty(errorCorrelationId) && 
                    _pendingQueries.TryGetValue(errorCorrelationId, out var errorTcs))
                {
                    errorTcs.TrySetException(new QueryException(message.Error.Code, message.Error.Message));
                }
                Console.WriteLine($"[GrpcProxy] Error from server: {message.Error.Code} - {message.Error.Message}");
                break;
                
            case ServerMessage.MessageOneofCase.CommandAccepted:
                // Ignorieren - wir nutzen Fire-and-Forget mit Events
                Console.WriteLine($"[GrpcProxy] Command accepted (ignored): {message.CommandAccepted.CorrelationId}");
                break;
                
            default:
                Console.WriteLine($"[GrpcProxy] Unknown message type: {message.MessageCase}");
                break;
        }
    }

    private void EnsureConnected()
    {
        if (_currentState != ConnectionState.Connected)
            throw new InvalidOperationException($"Not connected. Current state: {_currentState}");
    }

    private async Task SetStateAsync(ConnectionState newState)
    {
        if (_currentState == newState) return;
        
        _currentState = newState;
        await _stateChanges.Writer.WriteAsync(newState);
        
        Console.WriteLine($"[GrpcProxy] State: {newState}");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        
        _events.Writer.Complete();
        _stateChanges.Writer.Complete();
        
        _readCts?.Dispose();
    }
}

/// <summary>
/// Exception für Query-Fehler vom Server.
/// </summary>
public class QueryException : Exception
{
    public string Code { get; }
    
    public QueryException(string code, string message) : base(message)
    {
        Code = code;
    }
}