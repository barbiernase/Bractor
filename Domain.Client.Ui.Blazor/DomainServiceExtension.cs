using Domain.Client.ImagePair;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registriert die komplette Client-Domäne: Stores, Handler, ViewModels, WiringConfig.
/// Host.Blazor ruft nur diese eine Methode auf — kein Domain-Import nötig.
/// </summary>
public static class DomainServiceExtensions
{
    public static IServiceCollection AddImagePairDomain(this IServiceCollection services)
    {
        GeneratedWiring.AddClientDomain(services);
        return services;
    }
}