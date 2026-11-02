using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

public static class GraphInstanzExtensions
{
    /// <summary>
    /// Bindet die Instanzen der Graphen aus der Umgebung (docs/04 §4.4, <c>umgebungen/&lt;name&gt;/graphen/*.json</c>), geprüft gegen die Graphen
    /// (<see cref="GraphInstanzen.Aus"/>), dazu je Akteur mit Arbeit seine Instanz (<see cref="Arbeit"/>); nennt die Umgebung Akteure unter
    /// <c>Umgebung:Hand</c>, beantwortet die Werkbank deren Arbeit (<see cref="HandAusfuehrer"/>). Ersetzt die Instanzen des Rahmens (ohne Umgebung je
    /// Graph eine). Danach bindet <see cref="TechnikExtensions.BindeTechnik(IServiceCollection, GraphInstanzen)"/>, was die Knoten im Host brauchen.
    /// </summary>
    public static GraphInstanzen BindeGraphen(this IServiceCollection services, UmgebungKonfig u)
    {
        var instanzen = GraphInstanzen.Aus(u.Graphen, Anwendung.Funktionen.Graphen, Anwendung.Akteure.Rechte);
        services.Replace(ServiceDescriptor.Singleton(instanzen));
        foreach (var fremd in u.Umgebung.Hand.Where(a => instanzen.Von(a) is not { Graph.Typ: var t } || !typeof(IAkteur).IsAssignableFrom(t)))
            throw new Infrastructure.Umgebung.BootFehler($"Umgebung:Hand nennt '{fremd}' — kein Akteur, der Arbeit hört (IHoert eines Commands ohne Ziel).",
                "akteur:" + fremd);
        if (u.Umgebung.Hand.Length > 0) services.AddHandAusfuehrer(u.Umgebung.Hand);
        Console.WriteLine($"[CQRS] Instanzen: {string.Join(", ", instanzen.Alle.Select(i => i.Name + " (" + i.Graph.Name + ")").DefaultIfEmpty("keine"))}");
        return instanzen;
    }
}
