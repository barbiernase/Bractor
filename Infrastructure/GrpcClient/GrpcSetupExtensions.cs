using Infrastructure.Mapping;
using Infrastructure.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.GrpcClient;

/// <summary>
/// Extensions für die Registrierung des gRPC Client Services.
/// </summary>
public static class GrpcSetupExtensions
{
    /// <summary>
    /// Registriert alle Services die für den gRPC Client benötigt werden.
    /// </summary>
    public static IServiceCollection AddGrpcClientServices(this IServiceCollection services)
    {
        Console.WriteLine("[Setup] Registriere gRPC Client Services...");

        // ProtoMessageMapper
        services.AddSingleton<ProtoMessageMapper>();
        Console.WriteLine("  ✓ ProtoMessageMapper");

        // CqrsClientService - bekommt alle Dependencies via DI:
        // - ActorSystem
        // - ProtoMessageMapper
        // - IAggregateDispatcher
        // - ProjectionQueryService
        services.AddSingleton<CqrsClientServiceImpl>();
        Console.WriteLine("  ✓ CqrsClientServiceImpl");

        // MessageTypeMapping initialisieren
        // Ersetzt den alten EventTypeResolver und kennt zusätzlich Queries
        services.AddSingleton<IMessageTypeInitializer, MessageTypeInitializer>();
        Console.WriteLine("  ✓ MessageTypeMapping");

        // EventTypeResolver für Backwards-Kompatibilität
        // (wird von SubscriptionTracker und PubSubStartupService genutzt)
        services.AddSingleton<IEventTypeInitializer, EventTypeInitializer>();
        Console.WriteLine("  ✓ EventTypeResolver");

        return services;
    }

    /// <summary>
    /// Mappt den gRPC Endpoint für den Client Service.
    /// </summary>
    public static IEndpointRouteBuilder MapCqrsGrpcService(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<CqrsClientServiceImpl>();
        Console.WriteLine("[Setup] gRPC Endpoint gemappt: CqrsClientService");
        
        return endpoints;
    }
}

/// <summary>
/// Interface für MessageTypeMapping-Initialisierung beim Startup.
/// </summary>
public interface IMessageTypeInitializer
{
    void Initialize();
}

/// <summary>
/// Initialisiert das MessageTypeMapping beim ersten Aufruf.
/// Kennt Events, Commands, Queries und QueryResponses.
/// </summary>
public class MessageTypeInitializer : IMessageTypeInitializer
{
    public MessageTypeInitializer()
    {
        Initialize();
    }

    public void Initialize()
    {
        MessageTypeMapping.Initialize();
    }
}

/// <summary>
/// Interface für EventTypeResolver-Initialisierung beim Startup.
/// </summary>
public interface IEventTypeInitializer
{
    void Initialize();
}

/// <summary>
/// Initialisiert den EventTypeResolver beim ersten Aufruf.
/// Wird von SubscriptionTracker und PubSubStartupService genutzt.
/// </summary>
public class EventTypeInitializer : IEventTypeInitializer
{
    public EventTypeInitializer()
    {
        Initialize();
    }

    public void Initialize()
    {
        EventTypeResolver.Initialize();
    }
}