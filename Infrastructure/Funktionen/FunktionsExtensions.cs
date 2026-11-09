using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

public static class GraphInstanzExtensions
{
    /// <summary>
    /// Bindet die Instanzen der Graphen aus der Umgebung (docs/04 §4.4, <c>umgebungen/&lt;name&gt;/graphen/*.json</c>), geprüft gegen die Graphen
    /// (<see cref="GraphInstanzen.Aus"/>). Ersetzt die Instanzen des Rahmens (ohne Umgebung je Graph eine). Danach bindet <see cref="TechnikExtensions.BindeTechnik(IServiceCollection, GraphInstanzen)"/>, was die Knoten im Host brauchen.
    /// </summary>
    public static GraphInstanzen BindeGraphen(this IServiceCollection services, UmgebungKonfig u)
    {
        var instanzen = GraphInstanzen.Aus(u.Graphen, Anwendung.Funktionen.Graphen, Anwendung.Akteure.Rechte,
            new Uebersetzungen(Anwendung.Funktionen.Uebersetzungen));
        services.Replace(ServiceDescriptor.Singleton(instanzen));
        Console.WriteLine($"[CQRS] Instanzen: {string.Join(", ", instanzen.Alle.Select(i => i.Name + " (" + i.Graph.Name + ")").DefaultIfEmpty("keine"))}");
        return instanzen;
    }
}
