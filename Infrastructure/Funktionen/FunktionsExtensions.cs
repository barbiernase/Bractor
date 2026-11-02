using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

public static class GraphInstanzExtensions
{
    /// <summary>
    /// Bindet die Instanzen der Graphen aus der Umgebung (docs/04 §4.4, <c>umgebungen/&lt;name&gt;/graphen/*.json</c>): geprüft gegen die Graphen
    /// (<see cref="GraphInstanzen.Aus"/>) und je Knoten seine Bindung (Laufort, Umsetzung, Lese-Fähigkeit nur im Host,
    /// <see cref="FunktionsBausteinWiring.BindungVon"/>); ein Knoten mit Laufort Hand meldet den Hand-Ausführer an. Ersetzt die Instanzen des Rahmens
    /// (ohne Umgebung je Graph eine). Danach bindet <see cref="TechnikExtensions.BindeTechnik(IServiceCollection, GraphInstanzen)"/>, was die Knoten im
    /// Host brauchen.
    /// </summary>
    public static GraphInstanzen BindeGraphen(this IServiceCollection services, UmgebungKonfig u)
    {
        var instanzen = GraphInstanzen.Aus(u.Graphen, Anwendung.Funktionen.Graphen, Anwendung.Akteure.Rechte);
        services.Replace(ServiceDescriptor.Singleton(instanzen));
        foreach (var (instanz, k, b, kk) in FunktionsBausteinWiring.Knoten(instanzen))
            if (FunktionsBausteinWiring.BindungVon(b, kk, instanz.Ort(k.Name)).Laufort == FunktionsLaufort.Hand) services.AddHandAusfuehrer();
        Console.WriteLine($"[CQRS] Instanzen: {string.Join(", ", instanzen.Alle.Select(i => i.Name + " (" + i.Graph.Name + ")").DefaultIfEmpty("keine"))}");
        return instanzen;
    }
}
