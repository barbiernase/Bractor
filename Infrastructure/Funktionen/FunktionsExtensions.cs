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
        var instanzen = GraphInstanzen.Aus(u.Graphen, Anwendung.Funktionen.Graphen);
        services.Replace(ServiceDescriptor.Singleton(instanzen));
        Console.WriteLine($"[CQRS] Instanzen: {string.Join(", ", instanzen.Alle.Select(i => i.Name + " (" + i.Graph.Name + ")").DefaultIfEmpty("keine"))}");
        return instanzen;
    }

    /// <summary>
    /// Bindet die Eingänge aus Quellen der Clients (docs/04 §4.7, <c>Clients:&lt;Name&gt;:Eingaenge</c>), geprüft gegen Quellarten, Akteure, Clients und die
    /// Übersetzungen der Akteur-Seite (<see cref="Infrastructure.Quellen.Eingaenge.Aus"/>). Danach bindet <c>BindeTechnik</c> auch ihre Werte und Technik.
    /// </summary>
    public static Infrastructure.Quellen.Eingaenge BindeEingaenge(this IServiceCollection services, UmgebungKonfig u)
    {
        var eingaenge = Infrastructure.Quellen.Eingaenge.Aus(u.Clients, Anwendung.Funktionen.Bausteine, new Uebersetzungen(Anwendung.Funktionen.Uebersetzungen),
            Anwendung.Akteure.Rechte, Anwendung.Akteure.ClientVertraege, u.Cluster.Art == ClusterArt.Einzelknoten);
        services.Replace(ServiceDescriptor.Singleton(eingaenge));
        // Die Annahme (docs/04 §4.7): Eingänge aus annehmenden Quellarten nimmt jeder Host auf dem Einblick-Port an — ohne ihn gäbe es keinen Weg hinein.
        if (eingaenge.Annehmende.FirstOrDefault() is { } annahme && u.Transport.EinblickPort <= 0)
            throw new Infrastructure.Umgebung.BootFehler($"Clients:{annahme.Client}:Eingaenge:{annahme.Name}: {annahme.Art.Name} nimmt an "
                + $"(POST {Annahmevertrag.Route}/{annahme.Pfad}) — dafür braucht der Host den Einblick-Port (Transport:EinblickPort).",
                WirkerId.FuerEingang(annahme.Client, annahme.Name));
        services.Replace(ServiceDescriptor.Singleton(sp => Infrastructure.Quellen.Annahmen.Aus(eingaenge, sp)));
        if (eingaenge.Alle.Count > 0)
            Console.WriteLine($"[CQRS] Eingänge: {string.Join(", ", eingaenge.Alle.Select(e => e.Ort + " (" + e.Art.Name + " für " + e.Akteur + ")"))}");
        return eingaenge;
    }
}
